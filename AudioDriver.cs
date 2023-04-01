using System;
using System.Collections.Concurrent;
using PortAudioSharp;
using System.Runtime.InteropServices;
using System.Threading;

namespace VFOTracker
{
    class AudioDriver : ISource
    {
        Stream audioStream;
        ConcurrentQueue<double[]> outputs = new ConcurrentQueue<double[]>();
        int chunk_size;
        double[] outputBuffer;
        int outputBufferWritePos = 0;
        AutoResetEvent okRead = new AutoResetEvent(false);
        int sampleBalance = 0;

        public AudioDriver(string input, int chunk_size)
        {
            this.chunk_size = chunk_size;
            PortAudio.Initialize();
            DeviceInfo di = PortAudio.GetDeviceInfo(PortAudio.DefaultInputDevice);
            Console.WriteLine($"Reading from {di.name}");
            StreamParameters inParam = new StreamParameters();
            inParam.channelCount = 1;
            inParam.device = PortAudio.DefaultInputDevice;
            inParam.sampleFormat = SampleFormat.Float32;
            inParam.suggestedLatency = 0.01;
            StreamParameters outParam = new StreamParameters();
            outParam.channelCount = 1;
            outParam.device = PortAudio.DefaultOutputDevice;
            outParam.sampleFormat = SampleFormat.Float32;
            outParam.suggestedLatency = 0.01;
            audioStream = new Stream(inParam, outParam, 48000, 0, StreamFlags.NoFlag, AudioCallback, null);
            audioStream.Start();
            int defaultDevice = PortAudio.DefaultInputDevice;
            outputBuffer = new double[chunk_size];
        }

        public StreamCallbackResult AudioCallback(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userDataPtr)
        {
            unsafe
            {
                float* floatptr = (float*)input.ToPointer();
                for (int i = 0; i < frameCount; i++)
                {
                    outputBuffer[outputBufferWritePos] = *floatptr;
                    floatptr++;
                    outputBufferWritePos++;
                    if (outputBufferWritePos == outputBuffer.Length)
                    {
                        outputs.Enqueue(outputBuffer);
                        outputBuffer = new double[chunk_size];
                        outputBufferWritePos = 0;
                        sampleBalance++;
                        okRead.Set();

                    }
                }
            }
            return StreamCallbackResult.Continue;
        }


        public double[] GetSamples()
        {
            double[] retVal = null;
            while (retVal == null)
            {
                if (!outputs.TryDequeue(out retVal))
                {
                    okRead.WaitOne();
                }
            }
            sampleBalance--;
            return retVal;
        }

        public void Stop()
        {
            audioStream.Stop();
            PortAudio.Terminate();
        }
    }
}