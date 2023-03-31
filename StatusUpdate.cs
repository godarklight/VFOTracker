
using System;
using System.Numerics;
using Gdk;
using Gtk;
using Cairo;

namespace VFOTracker
{
    class StatusUpdate
    {
        public string status;
        public byte[] complexData;
        public byte[] fftData;
    }
}