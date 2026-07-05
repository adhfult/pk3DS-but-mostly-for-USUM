using System;
using System.Drawing;
using System.Windows.Forms;

namespace pk3DS.WinForms.Controls
{
    public class StatBar : Control
    {
        private int _value = 0;
        private int _maximum = 255;

        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0, Math.Min(value, _maximum));
                Invalidate();
            }
        }

        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(1, value);
                Invalidate();
            }
        }

        public StatBar()
        {
            DoubleBuffered = true;
            Height = 15;
            Width = 100;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.Clear(BackColor); // Usually the dark theme background

            if (_value <= 0) return;

            float percentage = (float)_value / _maximum;
            int barWidth = (int)(Width * percentage);

            // Determine color based on Showdown ranges
            Color barColor;
            if (_value < 30) barColor = Color.FromArgb(243, 68, 68);       // Red
            else if (_value < 60) barColor = Color.FromArgb(255, 127, 15);  // Orange
            else if (_value < 90) barColor = Color.FromArgb(255, 221, 87);  // Yellow
            else if (_value < 120) barColor = Color.FromArgb(160, 229, 21); // Light Green
            else if (_value < 150) barColor = Color.FromArgb(35, 205, 94);  // Green
            else barColor = Color.FromArgb(0, 194, 184);                    // Cyan

            using (var brush = new SolidBrush(barColor))
            {
                g.FillRectangle(brush, 0, 0, barWidth, Height);
            }
            
            // Draw a subtle border around the filled portion
            using (var pen = new Pen(Color.FromArgb(100, 0, 0, 0)))
            {
                g.DrawRectangle(pen, 0, 0, barWidth - 1, Height - 1);
            }
        }
    }
}
