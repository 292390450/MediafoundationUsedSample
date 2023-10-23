using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using FeatureLevel = Vortice.Direct3D.FeatureLevel;

namespace AvaloniaApplication1
{
    class D3D11Swapchain
    {
        private List<D3D11SwapchainImage> _pendingImages = new();
        protected ICompositionGpuInterop Interop { get; }
        protected CompositionDrawingSurface Target { get; }
        private readonly ID3D11Device _device;
        public D3D11Swapchain(ID3D11Device device, ICompositionGpuInterop interop, CompositionDrawingSurface target)
        {
            Interop = interop;
            Target = target;
            _device = device;
        }

        protected  D3D11SwapchainImage CreateImage(PixelSize size) => new(_device, size, Interop, Target);
        static bool IsBroken(D3D11SwapchainImage image) => image.LastPresent?.IsFaulted == true;
        static bool IsReady(D3D11SwapchainImage image) => image.LastPresent == null || image.LastPresent.Status == TaskStatus.RanToCompletion;
        public IDisposable BeginDraw(PixelSize size, out ID2D1RenderTarget view)
        {
            var rv = BeginDrawCore(size, out var image);
            view = image.RenderTargetView;
            return rv;
        }
        D3D11SwapchainImage? CleanupAndFindNextImage(PixelSize size)
        {
            D3D11SwapchainImage? firstFound = null;
            var foundMultiple = false;

            for (var c = _pendingImages.Count - 1; c > -1; c--)
            {
                var image = _pendingImages[c];
                var ready = IsReady(image);
                var matches = image.Size == size;
                if (IsBroken(image) || (!matches && ready))
                {
                    image.DisposeAsync();
                    _pendingImages.RemoveAt(c);
                }

                if (matches && ready)
                {
                    if (firstFound == null)
                        firstFound = image;
                    else
                        foundMultiple = true;
                }

            }

            // We are making sure that there was at least one image of the same size in flight
            // Otherwise we might encounter UI thread lockups
            return foundMultiple ? firstFound : null;
        }
        protected IDisposable BeginDrawCore(PixelSize size, out D3D11SwapchainImage image)
        {
            var img = CleanupAndFindNextImage(size) ?? CreateImage(size);

            img.BeginDraw();
            _pendingImages.Remove(img);
            image = img;
            return Disposable.Create(() =>
            {
                img.Present();
                _pendingImages.Add(img);
            });
        }

    }
    public class D3D11SwapchainImage 
    {
        public PixelSize Size { get; }
        private readonly ICompositionGpuInterop _interop;
        private readonly CompositionDrawingSurface _target;
        private readonly ID3D11Texture2D _texture;
        private readonly IDXGIKeyedMutex _mutex;
        private readonly IntPtr _handle;
        private PlatformGraphicsExternalImageProperties _properties;
        private ICompositionImportedGpuImage? _imported;
        public Task? LastPresent { get; private set; }
        public ID2D1RenderTarget RenderTargetView { get; }

        public D3D11SwapchainImage(ID3D11Device device, PixelSize size,
            ICompositionGpuInterop interop,
            CompositionDrawingSurface target)
        {
            Size = size;
            _interop = interop;
            _target = target;
            _texture = device.CreateTexture2D(new Texture2DDescription
            {
                Format = Format.R8G8B8A8_UNorm,
                Width = size.Width,
                Height = size.Height,
                ArraySize = 1,
                MipLevels = 1,
                SampleDescription = new SampleDescription { Count = 1, Quality = 0 },
                CPUAccessFlags = default,
                MiscFlags = ResourceOptionFlags.SharedKeyedMutex,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource
            });
            _mutex = _texture.QueryInterface<IDXGIKeyedMutex>();
            using (var res = _texture.QueryInterface<IDXGIResource>())
                _handle = res.SharedHandle;
            _properties = new PlatformGraphicsExternalImageProperties
            {
                Width = size.Width,
                Height = size.Height,
                Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm
            };
           // RenderTargetView = device.CreateRenderTargetView(_texture);
           using (var dxgisurface = _texture.QueryInterface<IDXGISurface>())
           {
               var d2dfFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>();
               var renderTargetProperties = new RenderTargetProperties(Vortice.DCommon.PixelFormat.Premultiplied);
               var d2drendertarget = d2dfFactory.CreateDxgiSurfaceRenderTarget(dxgisurface, renderTargetProperties);
               IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>();
               ID2D1Device d2dDevice = d2dfFactory.CreateDevice(dxgiDevice);
               ID2D1DeviceContext d2dDeviceContext = d2dDevice.CreateDeviceContext();
               ID2D1Bitmap1 d2dBitmap = d2dDeviceContext.CreateBitmapFromDxgiSurface(dxgisurface);
               d2dDeviceContext.Target = d2dBitmap;

               RenderTargetView = d2drendertarget;
            }
          
        
        }

        public void BeginDraw()
        {
            _mutex.AcquireSync(0, int.MaxValue);
        }

        public void Present()
        {
            _mutex.Release();
            _imported ??= _interop.ImportImage(
                new PlatformHandle(_handle, KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle),
                _properties);
            LastPresent = _target.UpdateWithKeyedMutexAsync(_imported, 1, 0);
        }


        public async ValueTask DisposeAsync()
        {
            if (LastPresent != null)
                try
                {
                    await LastPresent;
                }
                catch
                {
                    // Ignore
                }

            RenderTargetView.Dispose();
            _mutex.Dispose();
            _texture.Dispose();
        }
    }


    internal class D3D11DemoControl: DrawingSurfaceBase
    {
        private ID3D11Device _device;
        private D3D11Swapchain _swapchain;
        private ID3D11DeviceContext _context;
        protected override (bool success, string info) InitializeGraphicsResources(Compositor compositor,
            CompositionDrawingSurface surface, ICompositionGpuInterop interop)
        {
            
            if (interop?.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes
                    .D3D11TextureGlobalSharedHandle) != true)
                return (false, "DXGI shared handle import is not supported by the current graphics backend");

            DXGI.CreateDXGIFactory1<IDXGIFactory1>(out var factory);
            factory.EnumAdapters1(0, out var adapter);
           var result = D3D11.D3D11CreateDevice(adapter,DriverType.Unknown, DeviceCreationFlags.None, new[]
            {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1,
            },out _device);
           
            _swapchain = new D3D11Swapchain(_device, interop, surface);
            _context = _device.ImmediateContext;
            return (true, $"D3D11 ({_device.FeatureLevel}) {adapter.Description1.Description}");
        }

        protected override void FreeGraphicsResources()
        {
            throw new NotImplementedException();
        }

        protected override void RenderFrame(PixelSize pixelSize)
        {
            using (_swapchain.BeginDraw(pixelSize, out var dId2D1RenderTarget))
            {
                dId2D1RenderTarget.BeginDraw();
                dId2D1RenderTarget.Clear(new Color4(0xFF, 0xFF, 0xFF));

                // 随意创建颜色
                var color = new Color4((byte)Random.Shared.Next(255), (byte)Random.Shared.Next(255),
                    (byte)Random.Shared.Next(255));
                using var brush = dId2D1RenderTarget.CreateSolidColorBrush(color);
                dId2D1RenderTarget.FillEllipse(new Ellipse(new System.Numerics.Vector2(200, 200), 100, 100), brush);

                dId2D1RenderTarget.EndDraw();
                _context.Flush();
            }
        }
    }
}
