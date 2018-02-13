using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ControlMic
{
    public partial class NotificationBallForm : Form
    {
        public event EventHandler DraggingEnd;


        private bool dragging;
        private Point pointClicked;


        public NotificationBallForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;

            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, 20, 20, 200, 200));
            BackColor = Color.Red;
            MouseDown += StartDragging;
            MouseMove += Move;
            MouseUp += StopDragging;
        }

        private void StopDragging(object sender, MouseEventArgs e)
        {
            dragging = false;
            DraggingEnd?.Invoke(this, e);
        }

        private void Move(object sender, MouseEventArgs e)
        {
            if (!dragging) return;

            var pointMoveTo = this.PointToScreen(new Point(e.X, e.Y));
            pointMoveTo.Offset(-pointClicked.X, -pointClicked.Y);
            this.Location = pointMoveTo;
        }

        private void StartDragging(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                pointClicked = new Point(e.X, e.Y);
            }
            else
            {
                dragging = false;
                DraggingEnd?.Invoke(this, e);
            }
        }

        public void SetVisibility(bool visible)
        {
            Invoke(new MethodInvoker(delegate
            {
                Visible = visible;
            }));

        }

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,     // x-coordinate of upper-left corner
            int nTopRect,      // y-coordinate of upper-left corner
            int nRightRect,    // x-coordinate of lower-right corner
            int nBottomRect,   // y-coordinate of lower-right corner
            int nWidthEllipse, // height of ellipse
            int nHeightEllipse // width of ellipse
        );
    }
}
