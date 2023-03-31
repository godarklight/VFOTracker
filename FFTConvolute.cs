using System;
using System.Numerics;

namespace VFOTracker
{
    class FFTConvolute
    {
        //https://youtu.be/hj7j4Q8T3Ck?t=978
        public static Complex[] Convolute(Complex[] signalFFT, Complex[] kernelFFT, int lengthOfFilter)
        {
            if (signalFFT.Length != kernelFFT.Length)
            {
                throw new ArgumentException("Signal and kernel lengths are not equal");
            }
            Complex[] multiplied = new Complex[signalFFT.Length];
            for (int i = 0; i < signalFFT.Length; i++)
            {
                multiplied[i] = signalFFT[i] * kernelFFT[i];
            }
            Complex[] ifft = FFT.CalcIFFT(multiplied);
            //Normalise
            for (int i = 0; i < signalFFT.Length; i++)
            {
                ifft[i] = ifft[i] / (double)(lengthOfFilter);
            }
            return ifft;
        }

        public static Complex[] ConvoluteShift(Complex[] signalFFT, Complex[] kernelFFT, int lengthOfFilter)
        {
            if (signalFFT.Length != kernelFFT.Length)
            {
                throw new ArgumentException("Signal and kernel lengths are not equal");
            }
            Complex[] multiplied = new Complex[signalFFT.Length];
            for (int i = 0; i < signalFFT.Length; i++)
            {
                multiplied[i] = signalFFT[i] * kernelFFT[i];
            }
            Complex[] ifft = FFT.CalcIFFT(multiplied);
            Complex[] shifted = new Complex[signalFFT.Length];
            //Shift and normalise
            for (int i = 0; i < signalFFT.Length - (lengthOfFilter / 2); i++)
            {
                shifted[i] = ifft[i + (lengthOfFilter / 2)] / (double)(lengthOfFilter);
            }
            return shifted;
        }
    }
}