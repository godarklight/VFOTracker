using System;
using System.Numerics;

namespace VFOTracker
{
    public class MorletWavelet
    {
        private Complex[] wavelet;
        public MorletWavelet(double frequency, double fullWidthHalfMaximum, int length)
        {
            frequency = (double)length * frequency;
            wavelet = new Complex[length];
            Complex add = Complex.Zero;
            Complex total = 0;
            for (int i = 0; i < length; i++)
            {
                double lengthM1 = length - 1.0;
                double time = (i - length / 2) / lengthM1;
                //Use alternative formula to generate complex morlet wavelet.
                //https://youtu.be/LMqTM7EYlqY?t=144
                //Full width at half maximum parameter

                Complex sineWave = Complex.ImaginaryOne * Math.Tau * frequency * time;
                Complex gaussianWindow = ((4 * Math.Log(2) * time * time) / (fullWidthHalfMaximum * fullWidthHalfMaximum));
                Complex topPart = sineWave - gaussianWindow;
                wavelet[i] = Complex.Exp(topPart);
                total += wavelet[i];
            }
            //Debug.PrintComplexArray("wavelet.csv", wavelet);
            if (total.Magnitude > 0.001)
            {
                Console.WriteLine($"Window error: {total}");
                //throw new ArgumentException("Gaussian window does not have enough samples");
            }
        }

        public Complex[] CalcFFT(int length)
        {
            Complex[] input = new Complex[length];
            Array.Copy(wavelet, 0, input, 0, wavelet.Length);
            return FFT.CalcFFT(input);
        }

        public Complex[] Convolute(Complex[] input)
        {
            int halfKernel = wavelet.Length / 2;
            Complex[] filtered = new Complex[input.Length];
            //Slide kernel along
            for (int i = 0; i < input.Length; i++)
            {
                //Dot product
                Complex total = 0;
                for (int j = 0; j < wavelet.Length; j++)
                {
                    int readIndex = i + j + 1 - wavelet.Length;
                    if (readIndex < 0 || readIndex >= input.Length)
                    {
                        continue;
                    }
                    //Flip kernel
                    total += input[readIndex] * wavelet[wavelet.Length - j - 1];
                }
                filtered[i] = total;
            }
            //Normalise
            for (int i = 0; i < filtered.Length; i++)
            {
                filtered[i] = new Complex(filtered[i].Real / wavelet.Length, filtered[i].Imaginary / wavelet.Length);
            }
            return filtered;
        }
    }
}