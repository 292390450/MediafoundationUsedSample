using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SharpGen.Runtime;
using Vortice.MediaFoundation;

namespace MfPlay
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            MediaFactory.MFStartup();
           var config = MediaFactory.MFCreateAttributes(1);
            config.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVidcap);
          var   videos = MediaFactory.MFEnumDeviceSources(config);
       var    SelectVideo = videos.First() as IMFActivate;
     
           var link = SelectVideo.GetString(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink);
    var reader=   CreateVideoReader(link, 1920, 1080, 30);

   
            List<Tuple<Guid, string>> Formats = new List<Tuple<Guid, string>>();
            var pros = typeof(VideoFormatGuids).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
            foreach (var fieldInfo in pros)
            {
                var sfsds = (Guid)fieldInfo.GetValue(null);
                Formats.Add(new Tuple<Guid, string>(sfsds,fieldInfo.Name));
            }
            var resolver = MediaFactory.MFCreateSourceResolver();
            var source = resolver.CreateObjectFromURL(
                  @"C:\Users\zzp\Pictures\下载.mp4",
                  SourceResolverFlags.MediaSource, null);

            var pres = source.CreatePresentationDescriptor();

            var size = MediaFactory.MFGetAttributeUInt64(pres, PresentationDescriptionAttributeKeys.Duration, 0);


            var pReader = MediaFactory.MFCreateSourceReaderFromMediaSource(source, null);
            for (int i = 0; i < pres.StreamDescriptorCount; i++)
            {
                pres.GetStreamDescriptorByIndex(i,out var pfSelected,out var descriptor);
               var type =   descriptor.MediaTypeHandler.CurrentMediaType;
                pReader.SetStreamSelection(i, true);
                // 读取媒体类型
             var nativeType=   pReader.GetNativeMediaType(i, 0);
                var typeGuid = type.Get<Guid>(MediaTypeAttributeKeys.MajorType);

                if (typeGuid == MediaTypeGuids.Video)
                {
                   
                    MediaFactory.MFGetAttributeSize(type, MediaTypeAttributeKeys.FrameSize, out var width,
                        out var height);
                    MediaFactory.MFGetAttributeRatio(type, MediaTypeAttributeKeys.FrameRate, out var numerator,
                        out var denominator);
                    using (var mediaTypeOut = MediaFactory.MFCreateMediaType())
                    {
                        mediaTypeOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                        mediaTypeOut.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.NV12);
                        //MediaFactory.MFSetAttributeSize(mediaTypeOut, MediaTypeAttributeKeys.FrameSize, width, height);
                        //MediaFactory.MFSetAttributeRatio(mediaTypeOut, MediaTypeAttributeKeys.FrameRate, numerator, denominator);
                        // mediaTypeOut.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                        pReader.SetCurrentMediaType(i, mediaTypeOut);
                    }
                   
                    var formatGuid = type.Get<Guid>(MediaTypeAttributeKeys.Subtype);
                  var fo=  Formats.Find(x => x.Item1 == formatGuid);

                     var count = 0;
                    bool isStreamChange;
                    while (true)
                    {
                        pReader.ReadSample(i, 0, out var index, out var flags, out var timestamp, out var sample);
                        if (sample != null)
                        {
                            for (int j = 0; j < sample.BufferCount; j++)
                            {
                                var buff = sample.GetBufferByIndex(j);
                                buff.Lock(out var ppbBuffer,out var pcbMaxLength,out var pcbCurrentLength);
                                buff.Unlock();
                            }
                            count++;
                            sample.Release();
                        }
                    }
                }
                else
                {
                    if (typeGuid == MediaTypeGuids.Audio)
                    { 
                        continue;
                        using (var mediaTypeOut = MediaFactory.MFCreateMediaType())
                        {
                            mediaTypeOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
                            mediaTypeOut.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
                            // mediaTypeOut.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                            pReader.SetCurrentMediaType(i, mediaTypeOut);
                        }
                        while (true)
                        {
                            pReader.ReadSample(i, 0, out var index, out var flags, out var timestamp, out var sample);
                            if (sample != null)
                            {
                                for (int j = 0; j < sample.BufferCount; j++)
                                {
                                    var buff = sample.GetBufferByIndex(j);
                                    buff.Lock(out var ppbBuffer, out var pcbMaxLength, out var pcbCurrentLength);
                                    buff.Unlock();
                                }
                                sample.Release();
                            }
                        }
                    }
                }

            }
        }


        static void ExportToFile()
        {
            var _recordStopwatch = new Stopwatch();
            _recordStopwatch.Reset();
            _recordStopwatch.Start();
            int videoIndex, audioIndex = 0;
            //加载系统的硬件设置
            var config = MediaFactory.MFCreateAttributes(1);
            config.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeAudcap);
            var audios = MediaFactory.MFEnumDeviceSources(config);
            var SelectMic = audios.FirstOrDefault();
            config.Dispose();
            config = MediaFactory.MFCreateAttributes(1);
            config.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVidcap);
            var videos = MediaFactory.MFEnumDeviceSources(config);
            var SelectVideo = videos.FirstOrDefault();
            config.Dispose();
            var recordPath = System.IO.Path.Combine(AppContext.BaseDirectory, "FileCache", "MaterialSource",
                "录制视频temp.mp4");
            var writer = MediaFactory.MFCreateSinkWriterFromURL(recordPath, null, null);
            //语音输出配置
            IMFSourceReader audioReader = null;
            if (SelectMic != null)
            {
                var link = SelectMic.GetString(CaptureDeviceAttributeKeys.SourceTypeAudcapEndpointId);
                audioReader = CreateAudioReader(link);
                var first = audioReader.GetCurrentMediaType(0);
                using (var outType = MediaFactory.MFCreateMediaType())
                {
                    outType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
                    outType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Aac);

                    //outType.Set(MediaTypeAttributeKeys.AvgBitrate, 44100);

                    // outType.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                    audioIndex = writer.AddStream(outType);
                }

                writer.SetInputMediaType(audioIndex, first, null);
                if (SelectVideo == null)
                {
                    writer.BeginWriting();
                    Task.Run((() =>
                    {
                        while (true)
                        {
                            audioReader.ReadSample(0, 0, out var index, out var flags, out var timestamp,
                                out var sample);
                            if (sample != null)
                            {
                                sample.SampleTime = _recordStopwatch.ElapsedMilliseconds * 10000;
                                writer.WriteSample(audioIndex, sample);
                                sample.Release();
                            }
                        }

                        audioReader.Dispose();
                    }));
                }
            }

            if (SelectVideo != null)
            {
                var link = SelectVideo.GetString(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink);
                //选择摄像头配置
                var reader = CreateVideoReader(link, 1920, 1080, 30);

                using (var outType = MediaFactory.MFCreateMediaType())
                {
                    outType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                    outType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);

                    outType.Set(MediaTypeAttributeKeys.AvgBitrate, 8 * 1024 * 1024);
                    MediaFactory.MFSetAttributeSize(outType, MediaTypeAttributeKeys.FrameSize, (uint)reader.Item2.Item3,
                        (uint)reader.Item2.Item4);
                    MediaFactory.MFSetAttributeRatio(outType, MediaTypeAttributeKeys.FrameRate, reader.Item2.Item2, 1);
                    outType.Set(MediaTypeAttributeKeys.InterlaceMode, VideoInterlaceMode.Progressive);
                    videoIndex = writer.AddStream(outType);
                }

                writer.SetInputMediaType(videoIndex, reader.Item2.Item1, null);
                writer.BeginWriting();
                if (audioReader != null)
                {
                    Task.Run((() =>
                    {
                        while (true)
                        {
                            audioReader.ReadSample(0, 0, out var index, out var flags, out var timestamp,
                                out var sample);
                            if (sample != null)
                            {
                                sample.SampleTime = _recordStopwatch.ElapsedMilliseconds * 10000;
                                writer.WriteSample(audioIndex, sample);
                                sample.Release();
                            }
                        }

                        audioReader.Dispose();
                    }));
                }
                //转换器
                var typ = reader.Item2.Item1.Get<Guid>(MediaTypeAttributeKeys.Subtype);
                var transform = CreateTransform(typ, VideoFormatGuids.Argb32, reader.Item2.Item3, reader.Item2.Item4,
                    reader.Item2.Item2);
                Task.Run((() =>
                {
                    var streamInfo = transform.GetOutputStreamInfo(0);
                    var _transformOutbuff = new OutputDataBuffer();
                    var outpBuffer = MediaFactory.MFCreateMemoryBuffer(streamInfo.Size);
                    var outpSample = MediaFactory.MFCreateSample();
                    outpSample.AddBuffer(outpBuffer);
                    _transformOutbuff.Sample = outpSample;
                    while (true)
                    {
                        reader.Item1.ReadSample(0, 0, out var index, out var flags, out var timestamp, out var sample);
                        if (sample != null)
                        {
                            transform.ProcessInput(0, sample, 0);
                            Result res = new Result();
                            do
                            {
                                res = transform.ProcessOutput(ProcessOutputFlags.None, sample.BufferCount, ref _transformOutbuff,
                                    out var status);
                                if (res.Success)
                                {
                                    //编码数据
                                    //_transformOutbuff.Sample.SampleTime = timesamp;
                                    //_transformOutbuff.Sample.SampleDuration = frameDuration;
                                    var buff = _transformOutbuff.Sample.GetBufferByIndex(0);
                                    buff.Lock(out var ppbBuffer, out var pMaxLength, out var pCurrentLength);
                                    
                                    {
                                       // RecordWriteableBitmap.Lock();
                                        //并行编程还慢些，可能超过了cpu核心数就不行了
                                       // IntPtr dst = RecordWriteableBitmap.BackBuffer + stried * (RecordWriteableBitmap.PixelHeight - 1);
                                       // IntPtr sour = ppbBuffer;
                                        //unsafe
                                        //{
                                        //    for (int i = 0; i < RecordWriteableBitmap.PixelHeight; ++i)
                                        //    {
                                        //        Unsafe.CopyBlock(dst.ToPointer(), sour.ToPointer(), (uint)stried);
                                        //        dst -= stried;
                                        //        sour += stried;
                                        //    }
                                        //}

                                        //RecordWriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, RecordWriteableBitmap.PixelWidth, RecordWriteableBitmap.PixelHeight));
                                        //RecordWriteableBitmap.Unlock();

                                    }
                                    buff.Unlock();
                                    buff.Release();
                                }

                            } while (res.Success);
                            //写入文件
                            sample.SampleTime = _recordStopwatch.ElapsedMilliseconds * 10000;
                            writer.WriteSample(videoIndex, sample);
                            sample.Release();
                        }
                    }
                    //释放相关
                    outpBuffer.Dispose();
                    outpSample.Dispose();
                    transform.Dispose();
                    reader.Item1.Dispose();
                }));
            }
        }

        public static IMFTransform CreateTransform(Guid input, Guid outPut, uint width, uint height, uint framerate)
        {
            MediaFactory.MFStartup();
            uint flagsss = (uint)(EnumFlag.EnumFlagSyncmft | EnumFlag.EnumFlagLocalmft | EnumFlag.EnumFlagSortandfilter);
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
        public static IMFSourceReader CreateAudioReader(string audioId)
        {
            var config = MediaFactory.MFCreateAttributes(2);
            config.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeAudcap);
            config.Set(CaptureDeviceAttributeKeys.SourceTypeAudcapEndpointId, audioId);
            var res = MediaFactory.MFCreateDeviceSource(config, out var mediaSource);
            var des = mediaSource.CreatePresentationDescriptor();
            IMFMediaType first = null;
            for (int i = 0; i < des.StreamDescriptorCount; i++)
            {
                des.GetStreamDescriptorByIndex(i, out var selected, out var descriptor);
                var mediaHandler = descriptor.MediaTypeHandler;
                for (int j = 0; j < mediaHandler.MediaTypeCount; j++)
                {
                    first = mediaHandler.GetMediaTypeByIndex(j);
                    break;
                }

                descriptor.Release();
            }

            des.Release();
            config.Release();
            if (first == null)
            {
                throw new Exception("没有可用的媒体类型");
            }

            var reader = MediaFactory.MFCreateSourceReaderFromMediaSource(mediaSource, null);
            reader.SetCurrentMediaType(0, first);
            mediaSource.Release();
            first.Release();
            return reader;
        }
        public static (IMFSourceReader, Tuple<IMFMediaType, uint, uint, uint>) CreateVideoReader(string link, uint defaultWidth, uint defaultHeight, uint defaultFrameRate)
        {
            var config = MediaFactory.MFCreateAttributes(2);
            config.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVidcap);
            config.Set(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink, link);
            var res = MediaFactory.MFCreateDeviceSource(config, out var mediaSource);
            var des = mediaSource.CreatePresentationDescriptor();
            IMFMediaType first = null;
            Tuple<IMFMediaType, uint, uint, uint> select = null;
            for (int i = 0; i < des.StreamDescriptorCount; i++)
            {
                des.GetStreamDescriptorByIndex(i, out var selected, out var descriptor);
                var mediaHandler = descriptor.MediaTypeHandler;
                List<Tuple<IMFMediaType, uint, uint, uint>> medias = new List<Tuple<IMFMediaType, uint, uint, uint>>();
                //找到尺寸最大的
                for (int j = 0; j < mediaHandler.MediaTypeCount; j++)
                {
                    first = mediaHandler.GetMediaTypeByIndex(j);
                    MediaFactory.MFGetAttributeRatio(first, MediaTypeAttributeKeys.FrameRate, out var de, out var fenm);
                    MediaFactory.MFGetAttributeSize(first, MediaTypeAttributeKeys.FrameSize, out var width, out var height);
                    Tuple<IMFMediaType, uint, uint, uint> size =
                        new Tuple<IMFMediaType, uint, uint, uint>(first, de / fenm, width, height);
                    medias.Add(size);
                }

                var search = medias.Find(x =>
                    x.Item2 == defaultFrameRate && x.Item3 == defaultWidth && x.Item4 == defaultHeight);
                if (search == null)
                {
                    select = medias.MaxBy(x => x.Item2 * x.Item3 * x.Item4);
                    first = select?.Item1;
                }
                else
                {
                    select = search;
                    first = search.Item1;
                }

                if (select != null)
                {
                    break;
                }
                descriptor.Release();
            }

            des.Release();
            config.Release();
            if (first == null)
            {
                throw new Exception("没有可用的媒体类型");
            }
            var reader = MediaFactory.MFCreateSourceReaderFromMediaSource(mediaSource, null);
            reader.SetCurrentMediaType(0, first);
            mediaSource.Release();
            first.Release();
            return new(reader, select);
        }
    }

    public class A : CallbackBase, IMFAsyncCallback
    {
        public Result GetParameters(out AsyncCallbackFlags pdwFlags, out int pdwQueue) => throw new NotImplementedException();

        public Result Invoke(IMFAsyncResult asyncResult) => throw new NotImplementedException();
    }
}
