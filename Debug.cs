using System;
using System.Numerics;
using System.IO;

namespace VFOTracker
{
    class Debug
    {
        public static void PrintDoubleArray(string fileName, double[] input)
        {
            File.Delete(fileName);
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                for (int i = 0; i < input.Length; i++)
                {
                    double d = input[i];
                    sw.WriteLine($"{i},{d}");
                }
            }
        }

        public static void PrintComplexArray(string fileName, Complex[] input)
        {
            File.Delete(fileName);
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                for (int i = 0; i < input.Length; i++)
                {
                    Complex c = input[i];
                    sw.WriteLine($"{i},{c.Real},{c.Imaginary},{c.Magnitude},{c.Magnitude * c.Magnitude},{c.Phase}");
                }
            }
        }

        public static void SaveRaw(string fileName, double[] input)
        {
            File.Delete(fileName);
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                for (int i = 0; i < input.Length; i++)
                {
                    double d = input[i];
                    short s = (short)(d * short.MaxValue);
                    fs.WriteByte((byte)(s & 0xFF));
                    fs.WriteByte((byte)((s >> 8) & 0xFF));
                }
            }
        }

        public static void SaveRaw(string fileName, Complex[] input)
        {
            File.Delete(fileName);
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                for (int i = 0; i < input.Length; i++)
                {
                    Complex c = input[i];
                    short s = (short)(c.Real * short.MaxValue);
                    fs.WriteByte((byte)(s & 0xFF));
                    fs.WriteByte((byte)((s >> 8) & 0xFF));
                    s = (short)(c.Imaginary * short.MaxValue);
                    fs.WriteByte((byte)(s & 0xFF));
                    fs.WriteByte((byte)((s >> 8) & 0xFF));
                }
            }
        }
    }
}