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
        public const int FULL_FRAME_INTERVAL = 3;
        public const int NEW_SAMPLES_PER_FRAME = 2048;
        public const int DISPLAY_FFT = 4096;
        public const int DISPLAY_SIZE = 768;
        private const int PROCESS_LENGTH = 16384;
        private const int FILTER_LENGTH = 4001;
        private static bool running = true;
        private static Thread processThread;
        private static ISource audioSource;
        private static Action<StatusUpdate> updateFrame;
        private static Action<StatusUpdate> partialUpdateFrame;
        public static double vfoFreq = 1000;
        private static byte[] tempBytesFFT = null;
        private static byte[] tempBytesFFTPassband = null;

        private static byte[] tempBytesComplex = null;

        [STAThread]
        public static void Main(string[] args)
        {
            //audioSource = new FileDriver("bradtone.raw", PROCESS_LENGTH);
            audioSource = new AudioDriver("default", NEW_SAMPLES_PER_FRAME);

            Application.Init();

            var app = new Application("org.VFOTracker.VFOTracker", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            app.AddWindow(win);

            win.Show();
            updateFrame = win.UpdateFrame;
            partialUpdateFrame = win.PartialUpdateFrame;

            processThread = new Thread(new ThreadStart(ProcessLoop));
            processThread.Start();
            Application.Run();

            running = false;
            processThread.Join();
        }

        private static void ProcessLoop()
        {
            StatusUpdate su = new StatusUpdate();
            su.status = "Starting";
            su.complexData = new byte[DISPLAY_SIZE * DISPLAY_SIZE * 3];
            su.fftData = new byte[DISPLAY_SIZE * (DISPLAY_SIZE / 2) * 3];
            su.fftPassbandData = new byte[DISPLAY_SIZE * (DISPLAY_SIZE / 2) * 3];
            tempBytesFFT = new byte[DISPLAY_SIZE * (DISPLAY_SIZE / 2) * 3];
            tempBytesFFTPassband = new byte[DISPLAY_SIZE * (DISPLAY_SIZE / 2) * 3];
            tempBytesComplex = new byte[DISPLAY_SIZE * DISPLAY_SIZE * 3];
            double lastFilterFreq = 1000;
            //Setup wavlet and FFT
            MorletWavelet wavelet = new MorletWavelet(1000.0 / 48000.0, 0.2, FILTER_LENGTH);
            Complex[] processKernelFFT = wavelet.CalcFFT(PROCESS_LENGTH * 2);
            //We must use an array that is at least PROCESS_LENGTH + the kernel length - 1.
            Complex[] processSamples = new Complex[PROCESS_LENGTH * 2];
            //Complex[] processSamples2 = new Complex[PROCESS_LENGTH * 2];
            Complex[] shortSamples = new Complex[DISPLAY_FFT];
            Complex[] shortSamplesWindow = new Complex[DISPLAY_FFT * 2];
            //Display FFT
            Complex[] blackmanWindow = new Complex[DISPLAY_FFT];
            for (int i = 0; i < blackmanWindow.Length; i++)
            {
                double iOverN = i / ((double)blackmanWindow.Length);
                //Blackmann-nuttall
                blackmanWindow[i] = 0.3635819 - (0.4891775 * Math.Cos(Math.Tau * iOverN)) + (0.1365995 * Math.Cos(2.0 * Math.Tau * iOverN)) - (0.0106411 * Math.Cos(3.0 * Math.Tau * iOverN));
                //Blackmann
                //blackmanWindow[i] = 0.42 - (0.5 * Math.Cos(Math.Tau * iOverN)) + (0.08 * Math.Cos(2.0 * Math.Tau * iOverN));
            }

            int frameID = 0;
            double phase = 0;
            Complex[] downmix = new Complex[PROCESS_LENGTH * 2];
            while (running)
            {
                bool isFullFrame = frameID % FULL_FRAME_INTERVAL == 0;
                if (lastFilterFreq != vfoFreq)
                {
                    lastFilterFreq = vfoFreq;
                    wavelet = new MorletWavelet(vfoFreq / 48000.0, 0.2, FILTER_LENGTH);
                    processKernelFFT = wavelet.CalcFFT(processSamples.Length);
                }

                //Add samples to end of the array
                double[] samples = audioSource.GetSamples();
                if (samples == null)
                {
                    Console.WriteLine("Done");
                    running = false;
                    return;
                }

                //Display FFT
                //Save samples
                for (int i = 0; i < shortSamples.Length - NEW_SAMPLES_PER_FRAME; i++)
                {
                    shortSamples[i] = shortSamples[NEW_SAMPLES_PER_FRAME + i];
                }
                //Add new samples
                for (int i = 0; i < samples.Length; i++)
                {
                    shortSamples[shortSamples.Length - NEW_SAMPLES_PER_FRAME + i] = samples[i];
                }
                //Apply blackmann window
                for (int i = 0; i < shortSamples.Length; i++)
                {
                    shortSamplesWindow[i] = shortSamples[i] * blackmanWindow[i];
                }
                Complex[] shortSamplesFFT = FFT.CalcFFT(shortSamplesWindow);

                //Save previous samples
                //Array.Copy(processSamples, NEW_SAMPLES_PER_FRAME, processSamples2, 0, processSamples2.Length - NEW_SAMPLES_PER_FRAME);
                for (int i = 0; i < processSamples.Length - NEW_SAMPLES_PER_FRAME; i++)
                {
                    processSamples[i] = processSamples[i + NEW_SAMPLES_PER_FRAME];
                }
                //Add new samples
                for (int i = 0; i < samples.Length; i++)
                {
                    processSamples[processSamples.Length - NEW_SAMPLES_PER_FRAME + i] = samples[i];
                }
                //Buffer flip
                /*
                Complex[] temp = processSamples;
                processSamples = processSamples2;
                processSamples2 = temp;
                */

                //Bandpass filter
                //double[] filtered = bandpass.Filter(processSamples);
                //Debug.SaveRaw("bradfilter.raw", filtered);

                //Morlet convolution
                //Complex[] morlet = wavelet.Convolute(processSamples);
                if (isFullFrame)
                {
                    Complex[] processSamplesFFT = FFT.CalcFFT(processSamples);
                    Complex[] morlet = FFTConvolute.Convolute(processSamplesFFT, processKernelFFT, out Complex[] passbandFFT, FILTER_LENGTH);

                    //Save the edge effect (overlap add, doesn't work for non overlapping FFTs)
                    /*
                    if (lastConvolution != null)
                    {
                        for (int i = 0; i < PROCESS_LENGTH; i++)
                        {
                            morlet[i] = morlet[i] + lastConvolution[i + PROCESS_LENGTH];
                        }
                    }
                    lastConvolution = morlet;
                    */

                    for (int i = 0; i < PROCESS_LENGTH; i++)
                    {
                        Complex orig = morlet[i + PROCESS_LENGTH / 2];
                        Complex carrier = new Complex(Math.Cos(phase), Math.Sin(phase));
                        phase -= (Math.Tau * vfoFreq) / 48000.0;
                        if (phase > Math.Tau || phase < Math.Tau)
                        {
                            phase = phase % Math.Tau;
                        }
                        downmix[i] = orig * carrier;
                    }


                    if (frameID == 99)
                    {
                        using (FileStream fs = new FileStream("test.raw", FileMode.Create))
                        {
                            Console.WriteLine("TEST WRITE");
                            for (int i = 0; i < morlet.Length; i++)
                            {
                                short s = (short)(processSamples[i].Real * short.MaxValue);
                                fs.WriteByte((byte)(s & 0xFF));
                                fs.WriteByte((byte)((s >> 8) & 0xFF));
                                s = (short)(morlet[i].Real * short.MaxValue);
                                fs.WriteByte((byte)(s & 0xFF));
                                fs.WriteByte((byte)((s >> 8) & 0xFF));
                            }
                        }
                    }

                    bool wasNegative = morlet[PROCESS_LENGTH / 2].Real < 0;
                    int crossings = 0;
                    int start = -1;
                    int end = -1;
                    for (int i = 0; i < PROCESS_LENGTH; i++)
                    {
                        bool isNegative = morlet[i + PROCESS_LENGTH / 2].Real < 0;
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
                        if (dbFS < -120)
                        {
                            dbFS = -120;
                        }
                        su.status = $"Frequency {freq.ToString("N2")} Amplitude: {dbFS.ToString("N2")}";
                        DrawData(su, shortSamplesFFT, passbandFFT, downmix);
                        updateFrame(su);
                    }
                }
                else
                {
                    phase += NEW_SAMPLES_PER_FRAME * Math.Tau * vfoFreq / 48000.0;
                    DrawDisplayWaterfall(su, shortSamplesFFT);
                    partialUpdateFrame(su);
                }
                frameID++;
            }
        }

        private static void DrawData(StatusUpdate su, Complex[] samplesFFT, Complex[] passbandFFT, Complex[] downmix)
        {
            int rowLength = DISPLAY_SIZE * 3;
            //Full FFT
            DrawDisplayWaterfall(su, samplesFFT);
            //Passband FFT
            Array.Copy(su.fftPassbandData, rowLength, tempBytesFFTPassband, 0, tempBytesFFTPassband.Length - rowLength);
            double passHzPerBin = 48000.0 / (double)(passbandFFT.Length);
            int passStartBin = (int)(100 / passHzPerBin);
            int passEndBin = (int)(4000 / passHzPerBin);
            int passDelta = passEndBin - passStartBin;
            double passScaling = passDelta / (double)DISPLAY_SIZE;
            int lastRow = rowLength * (DISPLAY_SIZE / 2 - 1);
            for (int i = 0; i < DISPLAY_SIZE; i++)
            {
                int readIndex = passStartBin + (int)(i * passScaling);
                //Complex valued FFT - we only have the negative frequencies
                double magVal = passbandFFT[passbandFFT.Length - readIndex].Magnitude / ((double)FILTER_LENGTH * (double)FILTER_LENGTH);
                double dbFS = 10 * Math.Log(magVal) + 40;
                if (dbFS > 0)
                {
                    dbFS = 0;
                }
                if (dbFS < -120)
                {
                    dbFS = -120;
                };
                Color c = GetDbColor(dbFS);
                tempBytesFFTPassband[lastRow + i * 3] = (byte)(c.R * 255);
                tempBytesFFTPassband[lastRow + i * 3 + 1] = (byte)(c.G * 255);
                tempBytesFFTPassband[lastRow + i * 3 + 2] = (byte)(c.B * 255);
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
            byte[] temp = su.fftPassbandData;
            su.fftPassbandData = tempBytesFFTPassband;
            tempBytesFFTPassband = temp;
            temp = su.complexData;
            su.complexData = tempBytesComplex;
            tempBytesComplex = temp;
        }

        private static void DrawDisplayWaterfall(StatusUpdate su, Complex[] samplesFFT)
        {
            int rowLength = DISPLAY_SIZE * 3;
            double hzPerBin = 48000.0 / (double)(samplesFFT.Length);
            //Full FFT
            Array.Copy(su.fftData, 0, tempBytesFFT, rowLength, tempBytesFFT.Length - rowLength);
            int startBin = (int)(100 / hzPerBin);
            int endBin = (int)(4000 / hzPerBin);
            int delta = endBin - startBin;
            double scaling = delta / (double)DISPLAY_SIZE;
            for (int i = 0; i < DISPLAY_SIZE; i++)
            {
                int readIndex = startBin + (int)(i * scaling);
                double magVal = samplesFFT[readIndex].Magnitude / (double)FILTER_LENGTH;
                double dbFS = 10 * Math.Log(magVal) + 40;
                if (dbFS > 0)
                {
                    dbFS = 0;
                }
                if (dbFS < -120)
                {
                    dbFS = -120;
                };
                Color c = GetDbColor(dbFS);
                tempBytesFFT[i * 3] = (byte)(c.R * 255);
                tempBytesFFT[i * 3 + 1] = (byte)(c.G * 255);
                tempBytesFFT[i * 3 + 2] = (byte)(c.B * 255);
            }
            byte[] temp = su.fftData;
            su.fftData = tempBytesFFT;
            tempBytesFFT = temp;
        }

        private static int GetPixelID(int x, int y)
        {
            return (y * DISPLAY_SIZE + x) * 3;
        }

        private static Color GetDbColor(double dbFS)
        {
            //Scale -80 = black, -60 blue, -40 green, 0 = red
            double rcol = 0;
            double gcol = 0;
            double bcol = 0;
            if (dbFS > -60 && dbFS <= -30)
            {
                double scale = ((dbFS + 60) / 30.0);
                bcol = scale;
            }
            if (dbFS > -30 && dbFS <= -20)
            {
                double scale = ((dbFS + 30) / 10.0);
                bcol = 1.0 - scale;
                gcol = scale;
            }
            if (dbFS > -20 && dbFS <= 0)
            {
                double scale = ((dbFS + 20) / 20.0);
                gcol = 1.0 - scale;
                rcol = scale;
            }
            return new Color(rcol, gcol, bcol);
        }
    }
}
