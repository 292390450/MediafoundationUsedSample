using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.MediaFoundation;


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
          private ID3D11Texture2D screenTexture;
        public int width;
        public int height;
        private long frameIndex = 0;
        private Action<IntPtr, int,long, long> frameCallback;
        private Action<IMFSample, long> _frameCallback1;
        private Stopwatch stopwatch = new Stopwatch();
   
 
        public bool IsStop { get; set; }
        //2:3
        public DXGIScreenGrap( Action<IntPtr,int, long, long> frameCallback, Action<IMFSample, long> frameCallback1,int frameRate=30)
        {
            _frameCallback1 = frameCallback1;
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
                                null, out device,out _featureLevel,out _id3D11DeviceContext);
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
            screenTexture= device.CreateTexture2D(textureDesc);
            duplicatedOutput = seleOutput1.DuplicateOutput(device);
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
                        if (frameIndex > 0)
                        {
                            var elapse = stopwatch.ElapsedMilliseconds;
                            // copy resource into memory that can be accessed by the CPU
                            using (var screenTexture2D = screenResource.QueryInterface<ID3D11Texture2D>())
                            {
                                //_frameCallback1?.Invoke(samp,elapse);
                                device.ImmediateContext.CopyResource(screenTexture, screenTexture2D);
                            }
                            //MediaFactory.MFCreateVideoSampleFromSurface(screenTexture, out var samp);
                          //  _frameCallback1?.Invoke(samp, elapse);
                            if (true)
                            {
                                // Get the desktop capture texture
                                var mapSource = device.ImmediateContext.Map(screenTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

                                // Create Drawing.Bitmap
                                IntPtr destPtr = Marshal.AllocHGlobal(width * 4 * height);
                                IntPtr dst = destPtr + mapSource.RowPitch * (height - 1);
                                IntPtr sour = mapSource.DataPointer;
                                unsafe
                                {
                                    for (int i = 0; i < height; ++i)
                                    {

                                        Unsafe.CopyBlock(dst.ToPointer(), sour.ToPointer(), (uint)mapSource.RowPitch);


                                        //  ImageHelper.Memcpy(dst, sour, grap.Stride);
                                        dst -= mapSource.RowPitch;
                                        sour += mapSource.RowPitch;
                                    }
                                }
                                frameCallback?.Invoke(destPtr, mapSource.DepthPitch, frameIndex, elapse);
                                //frameCallback?.Invoke(destPtr, frameIndex, elapse);

                                device.ImmediateContext.Unmap(screenTexture, 0);
                            }
                          
                          

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
            screenTexture.Dispose();
            device.Dispose();
            factory.Dispose();
            stopwatch.Stop();
            duplicatedOutput = null;
            screenTexture = null;
            device = null;
            factory = null;
        }


    }
}
