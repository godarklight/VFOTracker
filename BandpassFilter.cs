using System;
using System.Numerics;

namespace VFOTracker
{
    public class BandpassFilter
    {
        double[] lowpass;
        double[] highpass;
        double[] kernel;
        public BandpassFilter(double filterFreq, int points)
        {
            lowpass = GenerateWindowedSinc(filterFreq * 1.1, false, points);
            Complex[] lowpassComplex = new Complex[lowpass.Length];
            for (int i = 0; i < lowpass.Length; i++)
            {
                lowpassComplex[i] = lowpass[i];
            }
            Complex[] lowpassFFT = FFT.CalcFFT(lowpassComplex);
            //Debug.PrintDoubleArray("lowpass.csv", lowpass);
            //Debug.PrintComplexArray("lowpassfft.csv", lowpassComplex);
            highpass = GenerateWindowedSinc(filterFreq * 0.9, true, points);
            kernel = new double[points];
            //Convolve the two filters
            for (int i = 0; i < points; i++)
            {
                double total = 0;
                for (int j = 0; j < points; j++)
                {
                    int readIndex = i + j - (points / 2);
                    if (readIndex < 0 || readIndex >= points)
                    {
                        continue;
                    }
                    total += lowpass[i] * highpass[readIndex];
                }
                kernel[i] = total;
            }
            //Debug.PrintDoubleArray("kernel.csv", kernel);
        }

        public double[] GenerateWindowedSinc(double frequency, bool highpass, int points)
        {
            //Odd length
            double[] generateKernel = new double[points];

            for (int i = 0; i < generateKernel.Length; i++)
            {
                int adjustI = i - (generateKernel.Length / 2);
                //Hamming window
                double window = 0.54 - (0.46 * Math.Cos(Math.Tau * i / (double)generateKernel.Length));
                //Blackman
                //double window = 0.42 - (0.5 * Math.Cos(Math.Tau * i / (double)points)) + (0.08 * Math.Cos(2 * Math.Tau * i / (double)points));

                //Mid point
                if (adjustI == 0)
                {
                    generateKernel[i] = Math.Tau * frequency;
                }
                else
                {
                    double filterValue = Math.Sin(Math.Tau * frequency * adjustI) / adjustI;
                    generateKernel[i] = window * filterValue;
                }
            }

            //Normalise to unity gain
            Normalise(generateKernel);

            //Flip all the signs and add 1 to DC for spectral inversion
            if (highpass)
            {
                for (int i = 0; i < generateKernel.Length; i++)
                {
                    generateKernel[i] = -generateKernel[i];
                    if (i == generateKernel.Length / 2)
                    {
                        generateKernel[i] = generateKernel[i] + 1.0;
                    }
                }
            }
            return generateKernel;
        }

        private void Normalise(double[] kernel)
        {
            double sum = 0;
            for (int i = 0; i < kernel.Length; i++)
            {
                sum += kernel[i];
            }
            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] = kernel[i] / sum;
            }
        }

        public double[] Filter(double[] input)
        {
            int halfKernel = kernel.Length / 2;
            double[] filtered = new double[input.Length];
            //Slide kernel along
            for (int i = 0; i < input.Length; i++)
            {
                //Dot product
                double total = 0;
                for (int j = 0; j < lowpass.Length; j++)
                {
                    int readIndex = i + j - halfKernel;
                    if (readIndex < 0 || readIndex >= input.Length)
                    {
                        continue;
                    }
                    total += input[readIndex] * lowpass[j];
                }
                filtered[i] = total;
            }
            double[] filtered2 = new double[input.Length];
            //Slide kernel along
            for (int i = 0; i < input.Length; i++)
            {
                //Dot product
                double total = 0;
                for (int j = 0; j < highpass.Length; j++)
                {
                    int readIndex = i + j - halfKernel;
                    if (readIndex < 0 || readIndex >= input.Length)
                    {
                        continue;
                    }
                    total += filtered[readIndex] * highpass[j];
                }
                filtered2[i] = total;
            }
            return filtered2;
        }
    }
}