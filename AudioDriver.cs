using System;
using PortAudioSharp;
using System.Runtime.InteropServices;
using System.Threading;

namespace VFOTracker
{
    class AudioDriver : ISource
    {
        Stream audioStream;
        double[] outputBuffer;
        double[] outputBuffer2;
        int outputBufferWritePos = 0;
        AutoResetEvent are = new AutoResetEvent(false);
        public AudioDriver(string input, int chunk_size)
        {
            PortAudio.Initialize();
            DeviceInfo di = PortAudio.GetDeviceInfo(PortAudio.DefaultInputDevice);
            Console.WriteLine($"Reading from {di.name}");
            StreamParameters inParam = new StreamParameters();
            inParam.channelCount = 2;
            inParam.device = PortAudio.DefaultInputDevice;
            inParam.sampleFormat = SampleFormat.Float32;
            inParam.suggestedLatency = 0.01;
            StreamParameters outParam = new StreamParameters();
            outParam.channelCount = 2;
            outParam.device = PortAudio.DefaultOutputDevice;
            outParam.sampleFormat = SampleFormat.Float32;
            outParam.suggestedLatency = 0.01;
            audioStream = new Stream(inParam, outParam, 48000, 0, StreamFlags.NoFlag, AudioCallback, null);
            audioStream.Start();
            int defaultDevice = PortAudio.DefaultInputDevice;
            outputBuffer = new double[chunk_size];
            outputBuffer2 = new double[chunk_size];
        }

        public StreamCallbackResult AudioCallback(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userDataPtr)
        {
            unsafe
            {
                float* floatptr = (float*)input.ToPointer();
                for (int i = 0; i < frameCount; i++)
                {
                    outputBuffer2[outputBufferWritePos] = *floatptr;
                    floatptr++;
                    floatptr++;
                    outputBufferWritePos++;
                    if (outputBufferWritePos == outputBuffer2.Length)
                    {
                        double[] temp = outputBuffer;
                        outputBuffer = outputBuffer2;
                        outputBuffer2 = temp;
                        Array.Copy(outputBuffer, outputBuffer.Length / 4, outputBuffer2, 0, 3 * outputBuffer.Length / 4);
                        outputBufferWritePos = 3 * outputBuffer.Length / 4;
                        are.Set();
                        //outputBufferWritePos = 0;

                    }
                }
            }
            return StreamCallbackResult.Continue;
        }


        public double[] GetSamples()
        {
            are.WaitOne();
            return outputBuffer;
        }

        public void Stop()
        {
            audioStream.Stop();
            PortAudio.Terminate();
        }
    }
}