using System;

namespace VFOTracker
{
    interface ISource
    {
        double[] GetSamples();
        void Stop();
    }
}