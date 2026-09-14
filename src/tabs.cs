// macOS-style segmented tabs: rounded container + sliding white pill indicator.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AgentToast
{
    public class MacTabs : Control
    {
        private List<string> ids = new List<string>();
        private List<string> names = new List<string>();
        private int selectedIndex = 0;
        private RectangleF pill;
        private RectangleF pillTarget;
        private Timer anim;

        public event EventHandler SelectedIndexChanged;

        public MacTabs()
        {
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
            this.Height = 34;
            this.BackColor = Color.FromArgb(245, 245, 247); // macOS window bg
            this.Font = new Font("Segoe UI", 9.5f);
            anim = new Timer();
            anim.Interval = 15;
            anim.Tick += AnimateStep;
        }

        public void AddTab(string id, string name)
        {
            ids.Add(id);
            names.Add(name);
            Invalidate();
        }

        public string SelectedId
        {
            get
            {
                if (selectedIndex >= 0 && selectedIndex < ids.Count) return ids[selectedIndex];
                return null;
            }
        }

        public void SelectTab(string id)
        {
            int i = ids.IndexOf(id);
            if (i < 0) return;
            selectedIndex = i;
            pill = PillRect(i);
            pillTarget = pill;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (ids.Count > 0)
            {
                pill = PillRect(selectedIndex);
                pillTarget = pill;
            }
            Invalidate();
        }

        private RectangleF PillRect(int i)
        {
            float w = (this.ClientSize.Width - 4) / (float)ids.Count;
            return new RectangleF(2 + i * w + 2, 3, w - 4, this.ClientSize.Height - 6);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (ids.Count == 0) return;
            float w = (this.ClientSize.Width - 4) / (float)ids.Count;
            int i = (int)((e.X - 2) / w);
            if (i < 0 || i >= ids.Count || i == selectedIndex) return;
            selectedIndex = i;
            pillTarget = PillRect(i);
            if (pill.IsEmpty) pill = pillTarget;
            anim.Start();
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }

        private void AnimateStep(object s, EventArgs e)
        {
            float dx = pillTarget.X - pill.X;
            float dw = pillTarget.Width - pill.Width;
            if (Math.Abs(dx) < 0.4f && Math.Abs(dw) < 0.4f)
            {
                pill = pillTarget;
                anim.Stop();
            }
            else
            {
                pill.X += dx * 0.28f;
                pill.Width += dw * 0.28f;
            }
            Invalidate();
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (ids.Count == 0) return;

            // container
            RectangleF rect = new RectangleF(0.5f, 0.5f, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
            using (GraphicsPath path = Rounded(rect, 9))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 226, 232)))
                g.FillPath(b, path);

            // pill (soft shadow + white fill)
            if (!pill.IsEmpty)
            {
                RectangleF shadow = pill;
                shadow.Y += 1.5f;
                using (GraphicsPath sp = Rounded(shadow, 7))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                    g.FillPath(sb, sp);
                using (GraphicsPath pp = Rounded(pill, 7))
                using (SolidBrush pb = new SolidBrush(Color.White))
                    g.FillPath(pb, pp);

            // labels
            float w = (this.ClientSize.Width - 4) / (float)ids.Count;
            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                using (Brush on = new SolidBrush(Color.FromArgb(20, 20, 24)))
                using (Brush off = new SolidBrush(Color.FromArgb(110, 110, 118)))
                using (Font bold = new Font(this.Font, FontStyle.Bold))
                {
                    for (int i = 0; i < names.Count; i++)
                    {
                        RectangleF r = new RectangleF(2 + i * w, 0, w, this.ClientSize.Height);
                        bool sel = (i == selectedIndex);
                        g.DrawString(names[i], sel ? bold : this.Font, sel ? on : off, r, fmt);
                    }
                }
            }
        }
    }
}
}
