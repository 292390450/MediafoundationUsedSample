using SharpGen.Runtime;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Timers;
using Vortice.DXGI;
using Vortice.MediaFoundation;
using Timer = System.Timers.Timer;

namespace DBB.DesktopPusher;

public class BitmapGrap
{
    private Bitmap bitmap;
    private int BitLength = 0;
    private IntPtr _bitIntPtr;
    private Stopwatch stopwatch = new Stopwatch();
    private IMFTransform transform = null;
    private OutputDataBuffer _transformOutbuff;
    private int fps = 30;

    public int Width;
    public int Height;
    private IMFSample _sample;
    public BitmapGrap()
    {
        //定时器
        LoadBit();
        stopwatch.Start();

    }

 

    private void LoadBit()
    {
        bitmap = (Bitmap)System.Drawing.Bitmap.FromFile(Path.Combine(AppContext.BaseDirectory, "interval.jpeg"));
        BitLength = bitmap.Width * bitmap.Height * 3;
        Width = bitmap.Width;
        Height = bitmap.Height;
        _bitIntPtr = Marshal.AllocHGlobal(BitLength);
        var res = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
            bitmap.PixelFormat);
        unsafe
        {
            IntPtr dst = _bitIntPtr + res.Stride * (bitmap.Height - 1);
            IntPtr sour = res.Scan0;
            for (int i = 0; i < bitmap.Height; ++i)
            {

                Unsafe.CopyBlock(dst.ToPointer(), sour.ToPointer(), (uint)res.Stride);


                //  ImageHelper.Memcpy(dst, sour, grap.Stride);
                dst -= res.Stride;
                sour += res.Stride;
            }
        }
        bitmap.UnlockBits(res);
        transform = ScreenPusher.CreateTransform(VideoFormatGuids.Rgb24, VideoFormatGuids.NV12, bitmap.Width,
            bitmap.Height, fps);
        var streamInfo = transform.GetOutputStreamInfo(0);
        _transformOutbuff = new OutputDataBuffer();
        var outpBuffer = MediaFactory.MFCreateMemoryBuffer(streamInfo.Size);
        var outpSample = MediaFactory.MFCreateSample();
        outpSample.AddBuffer(outpBuffer);
        _transformOutbuff.Sample = outpSample;
        var pBuffer = MediaFactory.MFCreateMemoryBuffer(BitLength);

        IntPtr pData;
        pBuffer.Lock(out pData, out var pcbMaxLength, out var pcbCurrentLength);
        unsafe
        {
            Unsafe.CopyBlock((void*)pData, (void*)_bitIntPtr, (uint)BitLength);
        }
        pBuffer.Unlock();
        pBuffer.CurrentLength = (BitLength);
        var pSample = MediaFactory.MFCreateSample();
        pSample.AddBuffer(pBuffer);
        pSample.SampleTime = stopwatch.ElapsedMilliseconds * 10 * 1000;
        pSample.SampleDuration = 10 * 1000 * 1000 / fps;
        transform.ProcessInput(0, pSample, 0);
        Result res1 = new Result();

        do
        {
            res1 = transform.ProcessOutput(ProcessOutputFlags.None, pSample.BufferCount, ref _transformOutbuff,
                out var status);
            if (res1.Success)
            {
                _sample = _transformOutbuff.Sample;
            }

        } while (res1.Success);
        pSample.Release();
        pBuffer.Release();
    }
    public IMFSample GetImage()
    {
        _sample.SampleTime = stopwatch.ElapsedMilliseconds * 10 * 1000;
        return _sample;
        var pBuffer = MediaFactory.MFCreateMemoryBuffer(BitLength);
      
        IntPtr pData;
        pBuffer.Lock(out pData, out var pcbMaxLength, out var pcbCurrentLength);
        unsafe
        {
            Unsafe.CopyBlock((void*)pData, (void*)_bitIntPtr, (uint)BitLength);
        }
        pBuffer.Unlock();
        pBuffer.CurrentLength = (BitLength);
        var pSample = MediaFactory.MFCreateSample();
        pSample.AddBuffer(pBuffer);
        pSample.SampleTime = stopwatch.ElapsedMilliseconds * 10 * 1000;
        pSample.SampleDuration = 10 * 1000 * 1000 / fps;
        try
        {
            transform.ProcessInput(0, pSample, 0);
            Result res = new Result();

            do
            {
                res = transform.ProcessOutput(ProcessOutputFlags.None, pSample.BufferCount, ref _transformOutbuff,
                    out var status);
                if (res.Success)
                {
                   
                }

            } while (res.Success);

            return _transformOutbuff.Sample;
        }
        catch (Exception e)
        {

        }
        finally
        {
           // pSample.Release();
          //  pBuffer.Release();
        }
       
      

        return null;

    }
    
}