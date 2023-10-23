using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ActivityMonitor.ScreenGrap;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.MediaFoundation;

namespace DBB.DesktopPusher
{
    internal class Program
    {
        static void Main(string[] args)
        {
         
          
            Console.WriteLine("Hello, World!");
            var demo = new ScreenPusher();
        }


          }

    public class BitmapPusher: CallbackBase, IMFAsyncCallback
    {
        RtspServer rtspServer = null;
        int port = 10100;
        private int fps = 30;
        string username = "";      // or use NUL if there is no username
        string password = "";  // or use NUL if there is no password
        private int VIDEO_BIT_RATE = 4 * 1024 * 1024;
        byte[] raw_sps = null;
        byte[] raw_pps = null;
        private IMFTransform encoder = null;
        private BitmapGrap bitmapGrap;
        private OutputDataBuffer _encoderOutbuff;
        private IMFMediaEventGenerator _eventGenerator;
        public BitmapPusher()
        {
            rtspServer = new RtspServer(port, username, password);
            try
            {
                rtspServer.StartListen();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                return;
            }
            Trace.WriteLine("RTSP URL is rtsp://" + username + ":" + password + "@" + "hostname:" + port);

          var device=  CreateDevice(1);
          
             bitmapGrap = new BitmapGrap();
            //编码器
            encoder = CreateEncoder(VideoFormatGuids.NV12, VideoFormatGuids.H265, (uint)bitmapGrap.Width , (uint)bitmapGrap.Height , (uint)fps, VIDEO_BIT_RATE);
         
        
       
          
         
       
        
          
            var outInfo = encoder.GetOutputStreamInfo(0);
            _encoderOutbuff = new OutputDataBuffer();
            var outpBuffer1 = MediaFactory.MFCreateMemoryBuffer(1920*1080*3);
            var outpSample1 = MediaFactory.MFCreateSample();
            outpSample1.AddBuffer(outpBuffer1);
            _encoderOutbuff.Sample = outpSample1;
         var type=   encoder.GetOutputAvailableType(0, 0);
            var mediaty = encoder.GetOutputCurrentType(0);
            var obg = mediaty.Get<byte[]>(MediaTypeAttributeKeys.MpegSequenceHeader);
           raw_sps = obg.AsSpan(4, obg.Length - 12).ToArray();
           raw_pps = obg.AsSpan(obg.Length - 4, 4).ToArray();
            encoder.ProcessMessage(TMessageType.MessageNotifyStartOfStream, (UIntPtr)null);
            Console.Read();
        }
        public ID3D11Device CreateDevice(int index)
        {
            DXGI.CreateDXGIFactory1<IDXGIFactory1>(out var factory);
            factory.EnumAdapters1(index, out var adapterOut);
            var desc = adapterOut.Description1;
            var res = D3D11.D3D11CreateDevice(adapterOut, DriverType.Unknown, (0)|DeviceCreationFlags.VideoSupport,
                null, out var device, out var  _featureLevel, out var _id3D11DeviceContext);
            if (res.Success)
            {
                return device;
                // break;
            }

            return null;
        }
        private void Encode(IMFSample sample, long timeSamp)
        {
            encoder.ProcessInput(0, sample, 0);
            Result res = new Result();
            byte[] comBytes = null;
            do
            {
                res = encoder.ProcessOutput(ProcessOutputFlags.None, sample.BufferCount, ref _encoderOutbuff,
                    out var status);
                if (res.Success)
                {
                    var isKey = MediaFactory.MFGetAttributeUInt32(_encoderOutbuff.Sample, SampleAttributeKeys.CleanPoint) > 0;
                    var buff = _encoderOutbuff.Sample.GetBufferByIndex(0);
                    buff.Lock(out var ppbBuffer, out var pcbMaxLength1, out var pcbCurrentLength1);
                    comBytes = new byte[pcbCurrentLength1];
                    Marshal.Copy(ppbBuffer, comBytes, 0, comBytes.Length);
                    buff.Unlock();
                    SendData(comBytes, isKey, timeSamp);

                }

            } while (res.Success);

        }
        private void SendData(byte[] comBytes, bool isKey, long timestamp_ms)
        {
            List<byte[]> nal_array = new List<byte[]>();
            Boolean add_sps_pps_to_keyframe = true;
            if (add_sps_pps_to_keyframe && isKey)
            {
                nal_array.Add(raw_sps);
                nal_array.Add(raw_pps);
            }
            // add the rest of the NALs
            nal_array.Add(comBytes);

            // Pass the NAL array into the RTSP Server
            rtspServer.FeedInRawSPSandPPS(raw_sps, raw_pps);
            rtspServer.FeedInRawNAL((uint)timestamp_ms, nal_array);
        }
      
        public IMFTransform CreateEncoder(Guid input, Guid outPut, uint width, uint height, uint framerate, int VIDEO_BIT_RATE = 8 * 1024 * 1024)
        {
            var res = MediaFactory.MFStartup();
            uint flagsss = (uint)(EnumFlag.EnumFlagSyncmft | EnumFlag.EnumFlagLocalmft | EnumFlag.EnumFlagSortandfilter);
            uint unFlags = (uint)( EnumFlag.EnumFlagAsyncmft | EnumFlag.EnumFlagLocalmft | EnumFlag.EnumFlagHardware | EnumFlag.EnumFlagSortandfilter);
            MediaFactory.MFTEnumEx(TransformCategoryGuids.VideoEncoder, unFlags, new RegisterTypeInfo()
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = input
            }, new RegisterTypeInfo()
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = outPut
            }, out var ppp, out var pnumMftActivate);

            IMFActivate[] transform = new IMFActivate[pnumMftActivate];
            unsafe
            {
                IntPtr* numPtr = (IntPtr*)(void*)ppp;
                for (int index = 0; index < pnumMftActivate; ++index)
                    transform[index] = new IMFActivate(numPtr[index]);
                Marshal.FreeCoTaskMem(ppp);
            }
            var trans = transform[0].ActivateObject<IMFTransform>();
            var res11 = transform[0].GetString(TransformAttributeKeys.MftFriendlyNameAttribute);
            trans.Attributes.SetUInt32(TransformAttributeKeys.TransformAsyncUnlock, 1);
          var ee=  trans.Attributes.Get(TransformAttributeKeys.TransformAsync);
            trans.Attributes.SetUInt32(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, 1);
            using (var mediaTypeOut = MediaFactory.MFCreateMediaType())
            {

                mediaTypeOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                mediaTypeOut.Set(MediaTypeAttributeKeys.Subtype, outPut);
                MediaFactory.MFSetAttributeSize(mediaTypeOut, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
                MediaFactory.MFSetAttributeRatio(mediaTypeOut, MediaTypeAttributeKeys.FrameRate, (uint)framerate, 1);
                mediaTypeOut.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                mediaTypeOut.Set(MediaTypeAttributeKeys.AvgBitrate, VIDEO_BIT_RATE);


                trans.SetOutputType(0, mediaTypeOut, 0);

                //var strmm=     MediaFactory.MFCreateFile(FileAccessMode.MfAccessModeReadwrite, FileOpenMode.MfOpenModeDeleteIfExist,
                //         FileFlags.None, "temp.mp4");

                //     MediaFactory.MFCreateMPEG4MediaSink(strmm, mediaTypeOut,null,out  var iMediaSink);
                //     sink= MediaFactory.MFCreateSinkWriterFromMediaSink(iMediaSink, null);
            }
            using (var mediaTypeIn = MediaFactory.MFCreateMediaType())
            {
                mediaTypeIn.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                mediaTypeIn.Set(MediaTypeAttributeKeys.Subtype, input);
                MediaFactory.MFSetAttributeSize(mediaTypeIn, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
                MediaFactory.MFSetAttributeRatio(mediaTypeIn, MediaTypeAttributeKeys.FrameRate, (uint)framerate, 1);
                mediaTypeIn.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                trans.SetInputType(0, mediaTypeIn, 0);
            }
            _eventGenerator = trans.QueryInterface<IMFMediaEventGenerator>();
            _eventGenerator.BeginGetEvent(this, null);
            
            return trans;
        }

        public Result GetParameters(out AsyncCallbackFlags pdwFlags, out int pdwQueue)
        {
            pdwFlags = AsyncCallbackFlags.None;
            pdwQueue = 0;
            return new Result(0);
        }

        public Result Invoke(IMFAsyncResult asyncResult)
        {
            var eventMediaEvent = _eventGenerator.EndGetEvent(result: asyncResult);
           
            if (eventMediaEvent.Status.Success)
            {
                switch (eventMediaEvent.Type)
                {
                    case MediaEventTypes.TransformNeedInput:
                        //获取一张图
                       var imfSmaple=  bitmapGrap.GetImage();
                      encoder.ProcessInput(0,imfSmaple,0);
                        // imfSmaple.Release();
                        break;
                    case MediaEventTypes.TransformHaveOutput:
                       var res= encoder.ProcessOutput(ProcessOutputFlags.None, 1, ref _encoderOutbuff,
                            out var odOutputStatus);
                       if (res.Success)
                       {
                           var isKey = MediaFactory.MFGetAttributeUInt32(_encoderOutbuff.Sample, SampleAttributeKeys.CleanPoint) > 0;
                           var buff = _encoderOutbuff.Sample.GetBufferByIndex(0);
                           buff.Lock(out var ppbBuffer, out var pcbMaxLength1, out var pcbCurrentLength1);
                           var  comBytes = new byte[pcbCurrentLength1];
                           Marshal.Copy(ppbBuffer, comBytes, 0, comBytes.Length);
                           buff.Unlock();
                           buff.Release();
                           SendData(comBytes, isKey, _encoderOutbuff.Sample.SampleTime/(10*1000));
                        }
                       _encoderOutbuff.Sample.Release();
                        break;
                    case MediaEventTypes.TransformDrainComplete:
                        break;
                    case MediaEventTypes.Error:
                        break;

                }
            }
            _eventGenerator.BeginGetEvent(this, null);
            eventMediaEvent.Release();
          
            return new Result(0);
        }
    }


    public class ScreenPusher
    {
        RtspServer rtspServer = null;
        int port = 10100;
        private int fps = 30;
        string username = "";      // or use NUL if there is no username
        string password = "";  // or use NUL if there is no password
        private int VIDEO_BIT_RATE = 4 * 1024 * 1024;
        byte[] raw_sps = null;
        byte[] raw_pps = null;
        private IMFTransform encoder = null;
        private IMFTransform transform = null;
        private OutputDataBuffer _encoderOutbuff;
        private OutputDataBuffer _transformOutbuff;
        public ScreenPusher()
        {
            rtspServer = new RtspServer(port, username, password);
            try
            {
                rtspServer.StartListen();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                return;
            }
            Trace.WriteLine("RTSP URL is rtsp://" + username + ":" + password + "@" + "hostname:" + port);
            var screenGrap = new DXGIScreenGrap(((nint,length, l, arg3) =>
            {
                //
                IMFMediaBuffer pBuffer = null;
                pBuffer = MediaFactory.MFCreateMemoryBuffer(length);
                IntPtr pData;
                pBuffer.Lock(out pData, out var pcbMaxLength, out var pcbCurrentLength);
                unsafe
                {
                    Unsafe.CopyBlock((void*)pData, (void*)nint, (uint)length);
                }
                pBuffer.Unlock();
                Marshal.FreeHGlobal(nint);
                pBuffer.CurrentLength = (length);
                var pSample = MediaFactory.MFCreateSample();
                pSample.AddBuffer(pBuffer);
                pSample.SampleTime = arg3 * 10 * 1000;
                pSample.SampleDuration = 10 * 1000 * 1000 / 60;
                //pSample.SampleDuration = frameDuration;
                transform.ProcessInput(0, pSample, 0);
                Result res = new Result();
              
                do
                {
                    res = transform.ProcessOutput(ProcessOutputFlags.None, pSample.BufferCount, ref _transformOutbuff,
                        out var status);
                    if (res.Success)
                    {
                        Encode(_transformOutbuff.Sample,arg3);
                    }

                } while (res.Success);
                pSample.Release();
                pBuffer.Release();

            }),((pSample, arg3) =>
            {
                transform.ProcessInput(0, pSample, 0);
                Result res = new Result();

                do
                {
                    res = transform.ProcessOutput(ProcessOutputFlags.None, pSample.BufferCount, ref _transformOutbuff,
                        out var status);
                    if (res.Success)
                    {
                        Encode(_transformOutbuff.Sample, arg3);
                    }

                } while (res.Success);
                pSample.Release();
            }), fps);

            transform = CreateTransform(VideoFormatGuids.Argb32, VideoFormatGuids.NV12, screenGrap.width,
                screenGrap.height, fps);
          var d3d11a=  transform.Attributes.GetUInt32(TransformAttributeKeys.D3D11Aware);
          

            var streamInfo = transform.GetOutputStreamInfo(0);
            _transformOutbuff = new OutputDataBuffer();
            var outpBuffer = MediaFactory.MFCreateMemoryBuffer(streamInfo.Size);
            var outpSample = MediaFactory.MFCreateSample();
            outpSample.AddBuffer(outpBuffer);
            _transformOutbuff.Sample = outpSample;
            //编码器
            encoder = CreateEncoder(VideoFormatGuids.NV12, VideoFormatGuids.H264, (uint)screenGrap.width/2, (uint)screenGrap.height/2, (uint)fps, VIDEO_BIT_RATE);
            var outInfo = encoder.GetOutputStreamInfo(0);
            _encoderOutbuff = new OutputDataBuffer();
            var outpBuffer1 = MediaFactory.MFCreateMemoryBuffer(outInfo.Size);
            var outpSample1 = MediaFactory.MFCreateSample();
            outpSample1.AddBuffer(outpBuffer1);
            _encoderOutbuff.Sample = outpSample1;
            var mediaty = encoder.GetOutputCurrentType(0);
            var obg = mediaty.Get<byte[]>(MediaTypeAttributeKeys.MpegSequenceHeader);
            raw_sps=obg.AsSpan(4, obg.Length - 12).ToArray();
            raw_pps= obg.AsSpan(obg.Length - 4, 4).ToArray();
            screenGrap.Start();
            Console.Read();
        }

        private void Encode(IMFSample sample,long timeSamp)
        {
            encoder.ProcessInput(0, sample, 0);
            Result res = new Result();
            byte[] comBytes = null;
            do
            {
                res = encoder.ProcessOutput(ProcessOutputFlags.None, sample.BufferCount, ref _encoderOutbuff,
                    out var status);
                if (res.Success)
                {
                    var isKey = MediaFactory.MFGetAttributeUInt32(_encoderOutbuff.Sample, SampleAttributeKeys.CleanPoint) > 0;
                    var buff = _encoderOutbuff.Sample.GetBufferByIndex(0);
                    buff.Lock(out var ppbBuffer, out var pcbMaxLength1, out var pcbCurrentLength1);
                    comBytes = new byte[pcbCurrentLength1];
                    Marshal.Copy(ppbBuffer, comBytes, 0, comBytes.Length);
                    buff.Unlock();
                    SendData(comBytes, isKey, timeSamp);
                   
                }

            } while (res.Success);
         
        }
        private void SendData(byte[] comBytes, bool isKey,long timestamp_ms)
        {
            List<byte[]> nal_array = new List<byte[]>();
            Boolean add_sps_pps_to_keyframe = true;
            if (add_sps_pps_to_keyframe && isKey)
            {
                nal_array.Add(raw_sps);
                nal_array.Add(raw_pps);
            }
            // add the rest of the NALs
            nal_array.Add(comBytes);

            // Pass the NAL array into the RTSP Server
            rtspServer.FeedInRawSPSandPPS(raw_sps, raw_pps);
            rtspServer.FeedInRawNAL((uint)timestamp_ms, nal_array);
        }
        public static IMFTransform CreateTransform(Guid input, Guid outPut, int width, int height, int framerate)
        {
            MediaFactory.MFStartup();
            uint flagsss = (uint)(EnumFlag.EnumFlagSyncmft | EnumFlag.EnumFlagLocalmft | EnumFlag.EnumFlagSortandfilter|EnumFlag.EnumFlagHardware);
            MediaFactory.MFTEnumEx(TransformCategoryGuids.VideoProcessor, 0, new RegisterTypeInfo()
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = input
            }, new RegisterTypeInfo()
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = outPut
            }, out var ppp, out var pnumMftActivate);
            IMFActivate[] transform = new IMFActivate[pnumMftActivate];
            unsafe
            {
                IntPtr* numPtr = (IntPtr*)(void*)ppp;
                for (int index = 0; index < pnumMftActivate; ++index)
                    transform[index] = new IMFActivate(numPtr[index]);
                Marshal.FreeCoTaskMem(ppp);
            }
            var trans = transform[0].ActivateObject<IMFTransform>();
            using (var mediaTypeOut = MediaFactory.MFCreateMediaType())
            {
                mediaTypeOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                mediaTypeOut.Set(MediaTypeAttributeKeys.Subtype, outPut);
                MediaFactory.MFSetAttributeSize(mediaTypeOut, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
                MediaFactory.MFSetAttributeRatio(mediaTypeOut, MediaTypeAttributeKeys.FrameRate, (uint)framerate, 1);
                mediaTypeOut.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                trans.SetOutputType(0, mediaTypeOut, 0);
                //var strmm=     MediaFactory.MFCreateFile(FileAccessMode.MfAccessModeReadwrite, FileOpenMode.MfOpenModeDeleteIfExist,
                //         FileFlags.None, "temp.mp4");

                //     MediaFactory.MFCreateMPEG4MediaSink(strmm, mediaTypeOut,null,out  var iMediaSink);
                //     sink= MediaFactory.MFCreateSinkWriterFromMediaSink(iMediaSink, null);
            }
            using (var mediaTypeIn = MediaFactory.MFCreateMediaType())
            {
                mediaTypeIn.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                mediaTypeIn.Set(MediaTypeAttributeKeys.Subtype, input);
                MediaFactory.MFSetAttributeSize(mediaTypeIn, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
                MediaFactory.MFSetAttributeRatio(mediaTypeIn, MediaTypeAttributeKeys.FrameRate, (uint)framerate, 1);
                mediaTypeIn.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                trans.SetInputType(0, mediaTypeIn, 0);
            }

            return trans;
        }
        public  IMFTransform CreateEncoder(Guid input, Guid outPut, uint width, uint height, uint framerate, int VIDEO_BIT_RATE = 8 * 1024 * 1024)
        {
            var res = MediaFactory.MFStartup();
            uint flagsss = (uint)(EnumFlag.EnumFlagSyncmft | EnumFlag.EnumFlagLocalmft | EnumFlag.EnumFlagSortandfilter);
            MediaFactory.MFTEnumEx(TransformCategoryGuids.VideoEncoder, 0, new RegisterTypeInfo()
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = input
            }, new RegisterTypeInfo()
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = outPut
            }, out var ppp, out var pnumMftActivate);

            IMFActivate[] transform = new IMFActivate[pnumMftActivate];
            unsafe
            {
                IntPtr* numPtr = (IntPtr*)(void*)ppp;
                for (int index = 0; index < pnumMftActivate; ++index)
                    transform[index] = new IMFActivate(numPtr[index]);
                Marshal.FreeCoTaskMem(ppp);
            }
            var trans = transform[0].ActivateObject<IMFTransform>();

            using (var mediaTypeOut = MediaFactory.MFCreateMediaType())
            {

                mediaTypeOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                mediaTypeOut.Set(MediaTypeAttributeKeys.Subtype, outPut);
                MediaFactory.MFSetAttributeSize(mediaTypeOut, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
                MediaFactory.MFSetAttributeRatio(mediaTypeOut, MediaTypeAttributeKeys.FrameRate, (uint)framerate, 1);
                mediaTypeOut.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                mediaTypeOut.Set(MediaTypeAttributeKeys.AvgBitrate, VIDEO_BIT_RATE);


                trans.SetOutputType(0, mediaTypeOut, 0);

                //var strmm=     MediaFactory.MFCreateFile(FileAccessMode.MfAccessModeReadwrite, FileOpenMode.MfOpenModeDeleteIfExist,
                //         FileFlags.None, "temp.mp4");

                //     MediaFactory.MFCreateMPEG4MediaSink(strmm, mediaTypeOut,null,out  var iMediaSink);
                //     sink= MediaFactory.MFCreateSinkWriterFromMediaSink(iMediaSink, null);
            }
            using (var mediaTypeIn = MediaFactory.MFCreateMediaType())
            {
                mediaTypeIn.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                mediaTypeIn.Set(MediaTypeAttributeKeys.Subtype, input);
                MediaFactory.MFSetAttributeSize(mediaTypeIn, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
                MediaFactory.MFSetAttributeRatio(mediaTypeIn, MediaTypeAttributeKeys.FrameRate, (uint)framerate, 1);
                mediaTypeIn.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                trans.SetInputType(0, mediaTypeIn, 0);
            }

            return trans;
        }

    }
}