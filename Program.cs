using System;
using Gtk;
using Cairo;
using System.Threading;
using System.Numerics;
using System.IO;

namespace VFOTracker
{
    class Program
    {
        public const int DISPLAY_SIZE = 400;
        private const int PROCESS_LENGTH = 8192;
        private const int FILTER_LENGTH = 1001;
        private static bool running = true;
        private static Thread processThread;
        private static ISource audioSource;
        private static Action<StatusUpdate> updateFrame;
        public static double vfoFreq = 1000;

        [STAThread]
        public static void Main(string[] args)
        {
            //audioSource = new FileDriver("bradtone.raw", PROCESS_LENGTH);
            audioSource = new AudioDriver("default", PROCESS_LENGTH);



            processThread = new Thread(new ThreadStart(ProcessLoop));
            processThread.Start();

            Application.Init();

            var app = new Application("org.VFOTracker.VFOTracker", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            app.AddWindow(win);

            win.Show();
            updateFrame = win.UpdateFrame;
            Application.Run();

            running = false;
            processThread.Join();
        }

        private static void ProcessLoop()
        {
            StatusUpdate su = new StatusUpdate();
            su.status = "Starting";
            su.complexData = new byte[DISPLAY_SIZE * DISPLAY_SIZE * 3];
            su.fftData = new byte[DISPLAY_SIZE * DISPLAY_SIZE * 3];
            tempBytesFFT = new byte[DISPLAY_SIZE * DISPLAY_SIZE * 3];
            tempBytesComplex = new byte[DISPLAY_SIZE * DISPLAY_SIZE * 3];
            double lastFilterFreq = 1000;
            MorletWavelet wavelet = new MorletWavelet(1000.0 / 48000.0, 0.2, FILTER_LENGTH);
            //We must use an array that is at least PROCESS_LENGTH + the kernel length - 1.
            Complex[] processSamples = new Complex[PROCESS_LENGTH * 2];
            //Setup wavlet FFT
            Complex[] processKernelFFT = wavelet.CalcFFT(processSamples.Length);
            //Debug.PrintComplexArray("waveletfft.csv", processKernelFFT);
            Complex[] lastConvolution = null;

            int frameID = 0;
            double phase = 0;
            Complex[] downmix = new Complex[PROCESS_LENGTH * 2];
            while (running)
            {
                if (lastFilterFreq != vfoFreq)
                {
                    lastFilterFreq = vfoFreq;
                    wavelet = new MorletWavelet(vfoFreq / 48000.0, 0.2, FILTER_LENGTH);
                }

                //Add samples to end of the array
                double[] samples = audioSource.GetSamples();
                if (samples == null)
                {
                    Console.WriteLine("Done");
                    running = false;
                    return;
                }
                for (int i = 0; i < samples.Length; i++)
                {
                    processSamples[i] = samples[i];
                }

                //Bandpass filter
                //double[] filtered = bandpass.Filter(processSamples);
                //Debug.SaveRaw("bradfilter.raw", filtered);

                //Morlet
                Complex[] processSamplesFFT = FFT.CalcFFT(processSamples);
                //Complex[] morlet = wavelet.Convolute(processSamples);
                Complex[] morlet = FFTConvolute.Convolute(processSamplesFFT, processKernelFFT, FILTER_LENGTH);

                //Save the edge effect
                if (lastConvolution != null)
                {
                    for (int i = 0; i < morlet.Length / 2; i++)
                    {
                        morlet[i] = morlet[i] + lastConvolution[i + (morlet.Length / 2)];
                    }
                }
                lastConvolution = morlet;

                for (int i = 0; i < morlet.Length / 2; i++)
                {
                    Complex orig = morlet[i];
                    Complex carrier = new Complex(Math.Cos(phase), Math.Sin(phase));
                    phase -= (Math.Tau * vfoFreq) / 48000.0;
                    if (phase > Math.Tau)
                    {
                        phase -= Math.Tau;
                    }
                    downmix[i] = orig * carrier;
                }

                /*
                for (int i = 0; i < morlet.Length / 2; i++)
                {
                    short s = (short)(morlet[i].Real * short.MaxValue);
                    fs.WriteByte((byte)(s & 0xFF));
                    fs.WriteByte((byte)((s >> 8) & 0xFF));
                    s = (short)(downmix[i].Real * short.MaxValue);
                    fs.WriteByte((byte)(s & 0xFF));
                    fs.WriteByte((byte)((s >> 8) & 0xFF));
                }
                */


                bool wasNegative = morlet[0].Real < 0;
                int crossings = 0;
                int start = -1;
                int end = -1;
                for (int i = 1; i < morlet.Length / 2; i++)
                {
                    bool isNegative = morlet[i].Real < 0;
                    if (isNegative != wasNegative)
                    {
                        if (start == -1)
                        {
                            start = i;
                        }
                        else
                        {
                            end = i;
                            crossings++;
                        }
                        wasNegative = isNegative;
                    }
                }

                double freq = 0.5 * (double)crossings * (48000.0 / (end - start));

                if (updateFrame != null && freq != Double.NaN)
                {
                    double dbFS = 10 * Math.Log(downmix[PROCESS_LENGTH / 2].Magnitude);
                    if (dbFS < -100)
                    {
                        dbFS = -100;
                    }
                    su.status = $"Frequency {freq.ToString("N2")} Amplitude: {dbFS.ToString("N2")}";
                    DrawData(su, processSamplesFFT, downmix);
                    updateFrame(su);
                }
                frameID++;
            }
        }

        private static byte[] tempBytesFFT = null;
        private static byte[] tempBytesComplex = null;
        private static void DrawData(StatusUpdate su, Complex[] samplesFFT, Complex[] downmix)
        {
            int rowLength = DISPLAY_SIZE * 3;
            double hzPerBin = 48000.0 / (double)(PROCESS_LENGTH * 2);

            Array.Copy(su.fftData, 0, tempBytesFFT, rowLength, tempBytesFFT.Length - rowLength);
            int startBin = (int)(100 / hzPerBin);
            int endBin = (int)(3000 / hzPerBin);
            int delta = endBin - startBin;
            double scaling = delta / (double)DISPLAY_SIZE;
            for (int i = 0; i < DISPLAY_SIZE; i++)
            {
                double magVal = samplesFFT[startBin + (int)(i * scaling)].Magnitude / 1001.0;
                double dbFS = 10 * Math.Log(magVal);
                if (dbFS > 0)
                {
                    dbFS = 0;
                }
                if (dbFS < -120)
                {
                    dbFS = -120;
                }
                //Scale -80 = black, -60 blue, -40 green, 0 = red
                byte rcol = 0;
                byte gcol = 0;
                byte bcol = 0;
                if (dbFS < -120)
                {
                    double scale = ((dbFS + 120) / 40.0);
                    bcol = (byte)(scale * 255);
                }
                if (dbFS > -80 && dbFS <= -40)
                {
                    double scale = ((dbFS + 80) / 40.0);
                    bcol = (byte)((1.0 - scale) * 255);
                    gcol = (byte)(scale * 255);
                }
                if (dbFS > -40 && dbFS <= 0)
                {
                    double scale = ((dbFS + 40) / 40.0);
                    gcol = (byte)((1.0 - scale) * 255);
                    rcol = (byte)(scale * 255);
                }
                byte pixelValue = (byte)(255 + 2.0 * dbFS);
                tempBytesFFT[i * 3] = rcol;
                tempBytesFFT[i * 3 + 1] = gcol;
                tempBytesFFT[i * 3 + 2] = bcol;
            }
            Array.Clear(su.complexData);
            for (int i = 0; i < downmix.Length / 2; i++)
            {
                double dbFS = 10 * Math.Log(downmix[i].Magnitude);
                if (dbFS < -120)
                {
                    dbFS = -120;
                }
                double length = (DISPLAY_SIZE * 0.45) * (1.0 - (-dbFS / 120.0));
                double angle = downmix[i].Phase;
                int pixelX = (int)(Math.Cos(angle) * length);
                int pixelY = (int)(Math.Sin(angle) * length);
                pixelX += DISPLAY_SIZE / 2;
                pixelY += DISPLAY_SIZE / 2;
                int pixelID = pixelY * DISPLAY_SIZE + pixelX;
                int byteID = pixelID * 3 + 1;
                //Light up in green
                int currentValue = su.complexData[byteID];
                currentValue += 4;
                if (currentValue > 255)
                {
                    currentValue = 255;
                }
                su.complexData[byteID] = (byte)currentValue;
            }
            //Draw center cross
            for (int i = 0; i <= 10; i++)
            {
                su.complexData[GetPixelID(-5 + i + DISPLAY_SIZE / 2, DISPLAY_SIZE / 2)] = 128;
                su.complexData[GetPixelID(DISPLAY_SIZE / 2, -5 + i + DISPLAY_SIZE / 2)] = 128;

            }

            //Buffer flip
            byte[] temp = su.fftData;
            su.fftData = tempBytesFFT;
            tempBytesFFT = temp;
            temp = su.complexData;
            su.complexData = tempBytesComplex;
            tempBytesComplex = temp;
        }

        private static int GetPixelID(int x, int y)
        {
            return (y * DISPLAY_SIZE + x) * 3;
        }
    }
}
