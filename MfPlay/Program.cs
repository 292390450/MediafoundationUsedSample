using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
