// VniTyping — biểu tượng app (vẽ bằng code) và icon ở khay hệ thống.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace VniTyping.UI
{
    internal static class AppIcon
    {
        /// <summary>Ô vuông bo góc màu nhấn chữ "V"; khi tắt bộ gõ: màu xám chữ "E"</summary>
        public static void Draw(Graphics g, RectangleF r, Theme t, bool off)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            Gfx.FillRound(g, off ? t.Muted : t.Accent, r, r.Width * 0.22f);
            using (var f = new Font("Segoe UI", r.Height * 0.58f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(off ? t.Field : t.AccentInk))
            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(off ? "E" : "V", f, b, new RectangleF(r.X, r.Y + r.Height * 0.03f, r.Width, r.Height), sf);
            }
        }

        public static Icon Make(int size, Theme t, bool off)
        {
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    Draw(g, new RectangleF(0, 0, size, size), t, off);
                }
                IntPtr h = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(h).Clone();
                try
                {
                    NativeMethods.DestroyIcon(h);
                }
                catch (Exception)
                {
                    // không có user32 (Mono): bỏ qua
                }
                return icon;
            }
        }
    }

    /// <summary>Icon ở khay hệ thống và menu chuột phải của nó</summary>
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon notify = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem[] modeItems = new ToolStripMenuItem[4];
        private bool hintShown;

        public event EventHandler ShowRequested;
        public event EventHandler ExitRequested;
        public event EventHandler<ModeEventArgs> ModeRequested;

        public TrayIcon()
        {
            var show = new ToolStripMenuItem("Hiện cửa sổ");
            show.ShortcutKeyDisplayString = "Ctrl+Alt+V";
            show.Font = new Font(menu.Font, FontStyle.Bold);
            show.Click += delegate { Raise(ShowRequested); };
            menu.Items.Add(show);
            menu.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < 4; i++)
            {
                int mode = i;
                modeItems[i] = new ToolStripMenuItem(ModeBar.Labels[i]);
                modeItems[i].Click += delegate
                {
                    if (ModeRequested != null) ModeRequested(this, new ModeEventArgs(mode));
                };
                menu.Items.Add(modeItems[i]);
            }
            menu.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem("Thoát");
            exit.Click += delegate { Raise(ExitRequested); };
            menu.Items.Add(exit);

            notify.ContextMenuStrip = menu;
            notify.Text = "VniTyping";
            notify.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Raise(ShowRequested);
            };
        }

        private void Raise(EventHandler h)
        {
            if (h != null) h(this, EventArgs.Empty);
        }

        public void Update(Theme t, int mode)
        {
            bool off = mode == 3;
            var old = notify.Icon;
            notify.Icon = AppIcon.Make(SystemInformation.SmallIconSize.Width, t, off);
            if (old != null) old.Dispose();
            notify.Text = "VniTyping · " + ModeBar.Labels[mode];
            menu.Renderer = new ThemeMenuRenderer(t);
            for (int i = 0; i < 4; i++) modeItems[i].Checked = i == mode;
        }

        public bool Visible
        {
            get { return notify.Visible; }
            set { notify.Visible = value; }
        }

        /// <summary>Nhắc một lần mỗi phiên: app vẫn chạy ở khay</summary>
        public void ShowHintOnce()
        {
            if (hintShown) return;
            hintShown = true;
            try
            {
                notify.ShowBalloonTip(3000, "VniTyping vẫn đang chạy",
                    "Bấm vào biểu tượng ở khay hoặc Ctrl+Alt+V để mở lại.", ToolTipIcon.None);
            }
            catch (Exception)
            {
                // một số môi trường không hỗ trợ balloon
            }
        }

        public void Dispose()
        {
            notify.Visible = false;
            if (notify.Icon != null) notify.Icon.Dispose();
            notify.Dispose();
            menu.Dispose();
        }
    }

    internal sealed class ModeEventArgs : EventArgs
    {
        public readonly int Mode;

        public ModeEventArgs(int mode)
        {
            Mode = mode;
        }
    }
}
