using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using Pixbuf = Gdk.Pixbuf;
using Image = Gtk.Image;
using Cairo;

namespace VFOTracker
{
    class MainWindow : Window
    {
        [UI] private Image imageFFT = null;
        [UI] private Image imagePassband = null;
        [UI] private Image imageComplex = null;
        [UI] private Label lblStatus = null;
        [UI] private SpinButton spinFreq = null;

        private const int SIZEX = 512;
        private const int SIZEY = 512;


        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);
            DeleteEvent += Window_DeleteEvent;
            spinFreq.ValueChanged += spinChanged;
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private void spinChanged(object sender, EventArgs a)
        {
            Program.vfoFreq = spinFreq.Value;
        }

        public void UpdateFrame(StatusUpdate su)
        {
            Application.Invoke((object o, EventArgs e) =>
            {
                imageFFT.Pixbuf = new Pixbuf(su.fftData, Gdk.Colorspace.Rgb, false, 8, Program.DISPLAY_SIZE, Program.DISPLAY_SIZE / 2, Program.DISPLAY_SIZE * 3, null);
                imagePassband.Pixbuf = new Pixbuf(su.fftPassbandData, Gdk.Colorspace.Rgb, false, 8, Program.DISPLAY_SIZE, Program.DISPLAY_SIZE / 2, Program.DISPLAY_SIZE * 3, null);
                imageComplex.Pixbuf = new Pixbuf(su.complexData, Gdk.Colorspace.Rgb, false, 8, Program.DISPLAY_SIZE, Program.DISPLAY_SIZE, Program.DISPLAY_SIZE * 3, null);
                lblStatus.Text = su.status;
            });
        }

        public void PartialUpdateFrame(StatusUpdate su)
        {
            Application.Invoke((object o, EventArgs e) =>
            {
                imageFFT.Pixbuf = new Pixbuf(su.fftData, Gdk.Colorspace.Rgb, false, 8, Program.DISPLAY_SIZE, Program.DISPLAY_SIZE / 2, Program.DISPLAY_SIZE * 3, null);
                lblStatus.Text = su.status;
            });
        }

        private void OnExpose(object sender, EventArgs args)
        {

        }
    }
}