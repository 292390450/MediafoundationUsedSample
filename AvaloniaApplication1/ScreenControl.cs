using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ActivityMonitor.ScreenGrap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Vortice.Direct2D1.Effects;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace AvaloniaApplication1
{
    public class ScreenControl:Control
    {
        private Compositor? _compositor;
        private DXGIScreenGrap grap;
        private CompositionSurfaceVisual? _visual;
        private ICompositionImportedGpuImage? _imported;
        private ICompositionGpuInterop interop;
        protected CompositionDrawingSurface Surface { get; private set; }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Initialize();
        }

        private async void Initialize()
        {
            
          
           
            var selfVisual = ElementComposition.GetElementVisual(this)!;
            _compositor = selfVisual.Compositor;
            Surface = _compositor.CreateDrawingSurface();
           
            interop = await _compositor.TryGetCompositionGpuInterop();
            grap = new DXGIScreenGrap( CallBack,60);
            grap.Start();
        }
        private PixelSize _currenPixelSize;
        private void CallBack(ID3D11Texture2D texture2D)
        {
            if (this.Bounds.Width>0&&Bounds.Height>0)
            {
                
                Dispatcher.UIThread.Invoke((async () =>
                {
                  
                    if (_visual == null)
                    {
                        var root = this.GetVisualRoot();
                        _visual = _compositor.CreateSurfaceVisual();
                        _visual.Size = new(this.Bounds.Width, this.Bounds.Height);
                        _visual.Surface = Surface;

                        ElementComposition.SetElementChildVisual(this, _visual);
                         _currenPixelSize = PixelSize.FromSize(Bounds.Size, root.RenderScaling);
                        grap.SetCurrentSize(_currenPixelSize);
                    }
                    else
                    {
                        //尺寸不一样释放之前的
                    }
                    if (_imported == null)
                    {
                        using (var res = texture2D.QueryInterface<IDXGIResource>())
                        {
                            try
                            {
                                var _properties = new PlatformGraphicsExternalImageProperties
                                {
                                    Width = _currenPixelSize.Width,
                                    Height = _currenPixelSize.Height,
                                    Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm
                                };
                                _imported = interop.ImportImage(
                                    new PlatformHandle(res.SharedHandle,
                                        KnownPlatformGraphicsExternalImageHandleTypes
                                            .D3D11TextureGlobalSharedHandle),
                                    _properties);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }

                    }
                   
                    await Surface.UpdateWithKeyedMutexAsync(_imported, 1, 0);
                }));
            }
          
        }
    }
}
