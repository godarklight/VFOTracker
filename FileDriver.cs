using System;
using System.IO;

namespace VFOTracker
{
    class FileDriver : ISource
    {
        byte[] fileBuffer;
        double[] outputBuffer;
        FileStream fs;
        public FileDriver(string input, int chunk_size)
        {
            fs = new FileStream(input, FileMode.Open);
            outputBuffer = new double[chunk_size];
            fileBuffer = new byte[chunk_size * 2];
        }
        public double[] GetSamples()
        {
            if (fs == null)
            {
                return null;
            }
            Array.Clear(fileBuffer);
            int readBytes = fs.Read(fileBuffer, 0, fileBuffer.Length);
            //Convert from S16LE to double
            for (int i = 0; i < outputBuffer.Length; i++)
            {
                int value = 0;
                value |= fileBuffer[i * 2];
                value |= fileBuffer[i * 2 + 1] << 8;
                outputBuffer[i] = (short)value / (double)short.MaxValue;
            }
            if (readBytes < outputBuffer.Length)
            {
                //fs.Seek(0, SeekOrigin.Begin);
                //fs.Read(fileBuffer, 0, fileBuffer.Length);
                Stop();
            }
            System.Threading.Thread.Sleep(outputBuffer.Length / 48);
            return outputBuffer;
        }

        public void Stop()
        {
            fs.Close();
            fs = null;
        }
    }
}