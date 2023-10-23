using Avalonia;
using SharpDX.D3DCompiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Mathematics.Interop;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;


namespace ActivityMonitor.ScreenGrap
{
    public class DXGIScreenGrap : IDisposable
    {
        // # of graphics card adapter
        const int numAdapter = 0;
        /// <summary>
        /// DXGI Factory1
        /// </summary>
        private IDXGIFactory1 factory;

        private int _frameRate = 40;
        private double _frameDura;
        private long _lastTime;
        public ID3D11Device device;

        private FeatureLevel _featureLevel;

        private ID3D11DeviceContext _id3D11DeviceContext;
          private IDXGIOutputDuplication duplicatedOutput;
        public int width;
        public int height;
        private long frameIndex = 0;
        private Action<IntPtr, int,long, long> frameCallback;
        private ID3D11Texture2D screenTexture;
        private ID3D11Texture2D showTexture;
        private IDXGIKeyedMutex _mutex;
        private Action<ID3D11Texture2D> _frameAction;
        private Stopwatch stopwatch = new Stopwatch();
        private ID3D11VertexShader vertexShade;
        private ID3D11RenderTargetView renderTargetView;
        public bool IsStop { get; set; }

        private Matrix4x4 _view;
        //2:3
        public DXGIScreenGrap(Action<ID3D11Texture2D> frameAction,int frameRate=30)
        {
            _frameAction = frameAction;
             _frameRate = frameRate;
            _frameDura = 1000.0 / _frameRate;
            stopwatch.Start();
            this.frameCallback = frameCallback;
            IDXGIOutput1 seleOutput1=null;
            DXGI.CreateDXGIFactory1<IDXGIFactory1>(out factory);
            int i = 0;
            while (factory.EnumAdapters1(i,out var adapter1).Success)
            {
                if (device != null)
                {
                    break;
                }
                int j = 0;
                
                while (adapter1.EnumOutputs(j,out var output).Success)
                {
                    if (device!=null)
                    {
                        break;
                    }
                    if (output != null)
                    {
                        seleOutput1 = output.QueryInterface<IDXGIOutput1>();
                       
                            var res = D3D11.D3D11CreateDevice(adapter1, DriverType.Unknown, DeviceCreationFlags.None,
                                new[]
                                {
                                    FeatureLevel.Level_12_1,
                                    FeatureLevel.Level_12_0,
                                    FeatureLevel.Level_11_1,
                                    FeatureLevel.Level_11_0,
                                    FeatureLevel.Level_10_0,
                                    FeatureLevel.Level_9_3,
                                    FeatureLevel.Level_9_2,
                                    FeatureLevel.Level_9_1,
                                }, out device,out _featureLevel,out _id3D11DeviceContext);
                            if (res.Success)
                            {
                                break;
                            }
                        
                     

                        //device = new Device(adapter1);
                        //  break;
                    }
                    j++;
                }
                i++;
            }
            width = seleOutput1.Description.DesktopCoordinates.Right - seleOutput1.Description.DesktopCoordinates.Left;
            height = seleOutput1.Description.DesktopCoordinates.Bottom - seleOutput1.Description.DesktopCoordinates.Top;
            // Create device from Adapter
            // Get DXGI.Output
            var textureDesc = new Texture2DDescription
            {
                CPUAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                MiscFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            screenTexture = device.CreateTexture2D(textureDesc);

            var showtextureDesc = new Texture2DDescription
            {
                CPUAccessFlags = default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                MiscFlags = ResourceOptionFlags.SharedKeyedMutex,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
            };
            showTexture = device.CreateTexture2D(showtextureDesc);
            _mutex = showTexture.QueryInterface<IDXGIKeyedMutex>();
            duplicatedOutput = seleOutput1.DuplicateOutput(device);
            var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Models\\MiniCube.fx", "VS", "vs_4_0");
            var rest= (Path.Combine(AppContext.BaseDirectory, "Models\\MiniCube.fx"));
          //var readOnlyMemory= Vortice.D3DCompiler.Compiler.CompileFromFile(rest, "VS", "vs_4_0");
          vertexShade=  device.CreateVertexShader(vertexShaderByteCode);
          _view = Matrix4x4.CreateLookAt(new Vector3(0, 0, -5), new Vector3(0, 0, 0), Vector3.UnitY);
          _currenPixelSize = new PixelSize(width, height);
        }


        private PixelSize _currenPixelSize;
        public void SetCurrentSize(PixelSize currenPixelSize)
        {
            _currenPixelSize = currenPixelSize;
        }
        public void Start()
        {
            Task.Run((() =>
            {

                while (!IsStop)
                {
                    try
                    {
                        var startTime = stopwatch.ElapsedMilliseconds;
                       IDXGIResource screenResource = null;
                        OutduplFrameInfo duplicateFrameInformation;

                        // Try to get duplicated frame within given time
                      var res=  duplicatedOutput.AcquireNextFrame(10000, out duplicateFrameInformation, out screenResource);
                        if (res.Success)
                        {
                            var elapse = stopwatch.ElapsedMilliseconds;
                            // copy resource into memory that can be accessed by the CPU
                            using (var screenTexture2D = screenResource.QueryInterface<ID3D11Texture2D>())
                            {
                                _mutex.AcquireSync(0, int.MaxValue);
                                if (renderTargetView==null)
                                {
                                    var des = new RenderTargetViewDescription()
                                    {
                                        ViewDimension = RenderTargetViewDimension.Texture2D,
                                    };
                                    des.Texture2D.MipSlice = 0;
                                    renderTargetView = device.CreateRenderTargetView(showTexture, des);
                                    device.ImmediateContext.OMSetRenderTargets(renderTargetView);
                                }

                                for (int i = 0; i < screenTexture2D.Description.Height; ++i)
                                {
                                    Box bo = new Box(0, screenTexture2D.Description.Height-i, 0, width, screenTexture2D.Description.Height-i + 1, 1);
                                    device.ImmediateContext.CopySubresourceRegion(showTexture, 0, 0, i, 0, screenTexture2D, 0, bo);
                                }
                               // device.ImmediateContext.CopyResource(showTexture, screenTexture2D);
                                  var colorOff = (float)(Math.Sin(2) + 1) / 2 * 5;

                                // device.ImmediateContext.VSSetShader(vertexShade);
                                //cpu中来做颠倒性能影响比较大
                                // device.ImmediateContext.CopyResource(screenTexture, screenTexture2D);
                                #region 内存颠倒

                                //var mapSource =   device.ImmediateContext.Map(screenTexture, 0);
                                //IntPtr destPtr = Marshal.AllocHGlobal(width * 4 * height);
                                //IntPtr dst = destPtr + mapSource.RowPitch * (height - 1);
                                //IntPtr sour = mapSource.DataPointer;
                                //unsafe
                                //{
                                //    for (int i = 0; i < height; ++i)
                                //    {

                                //        Unsafe.CopyBlock(dst.ToPointer(), sour.ToPointer(), (uint)mapSource.RowPitch);


                                //        //  ImageHelper.Memcpy(dst, sour, grap.Stride);
                                //        dst -= mapSource.RowPitch;
                                //        sour += mapSource.RowPitch;
                                //    }
                                //}
                                //device.ImmediateContext.Unmap(screenTexture, 0);
                                //device.ImmediateContext.UpdateSubresource(showTexture,0,new Box(0,0,0,width,height,1), destPtr,
                                //    mapSource.RowPitch,0);
                                //Marshal.FreeHGlobal(destPtr);

                                #endregion
                                float scaleX = 2.0f;  // x方向缩放因子
                                float scaleY = 2.0f;  // y方向缩放因子
                              
                              var sd= Matrix4x4.CreateScale(scaleX, scaleY, 1.0f);
                             // device.ImmediateContext.PSSetShaderResources(0,1, showTexture);
                              var _proj = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, (float)(_currenPixelSize.Width / _currenPixelSize.Height), 0.1f, 100.0f);
                                var viewProj = Matrix4x4.Multiply(_view, _proj);
                             
                                device.ImmediateContext.RSSetViewport(0,0,_currenPixelSize.Width,_currenPixelSize.Height);
                                var constantBuffer = device.CreateBuffer( Utilities.SizeOf<Matrix4x4>(),
                                    BindFlags.ConstantBuffer, ResourceUsage.Default,CpuAccessFlags.None, ResourceOptionFlags.None, 0);
                                device.ImmediateContext.VSSetConstantBuffer(0, constantBuffer);
                                device.ImmediateContext.UpdateSubresource(viewProj, constantBuffer);
                                // device.ImmediateContext.setvi
                                // device.ImmediateContext.ClearRenderTargetView(renderTargetView, new Color4(1 - colorOff, colorOff, (float)0.5 + colorOff / 2, 1));
                                device.ImmediateContext.Flush();
                                _mutex.ReleaseSync(1);
                                _frameAction.Invoke(showTexture);
                               
                            }
                            //MediaFactory.MFCreateVideoSampleFromSurface(screenTexture, out var samp);
                          //  _frameCallback1?.Invoke(samp, elapse);
                           
                          

                        }

                        screenResource.Dispose();
                        duplicatedOutput.ReleaseFrame();
                        frameIndex++;
                        var endTime = stopwatch.ElapsedMilliseconds;
                        var interval = endTime - startTime;

                        if (interval < _frameDura)
                        {
                            Thread.Sleep((int)(_frameDura - interval));
                        }

                    }
                    catch (Exception e)
                    {
                       
                    }
                }
            }));


        }
        public async void Stop()
        {
            IsStop = true;
            await Task.Delay(1000);
            Dispose();
        }

     
        public void Dispose()
        {
            _id3D11DeviceContext.Dispose();
            duplicatedOutput.Dispose();
         
            device.Dispose();
            factory.Dispose();
            stopwatch.Stop();
            duplicatedOutput = null;
            device = null;
            factory = null;
        }


    }
}
