// VniTyping — các control tự vẽ theo theme (WinForms + GDI+).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace VniTyping.UI
{
    /// <summary>Tiện ích vẽ dùng chung</summary>
    internal static class Gfx
    {
        /// <summary>Trộn màu: t = 0 → b, t = 1 → a</summary>
        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(255,
                (int)(a.R * t + b.R * (1 - t)),
                (int)(a.G * t + b.G * (1 - t)),
                (int)(a.B * t + b.B * (1 - t)));
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Color c, float width, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(c, width))
                g.DrawPath(pen, p);
        }

        /// <summary>Quầng sáng mờ quanh một hình (giao diện Phố Neon)</summary>
        public static void Glow(Graphics g, Color c, RectangleF r, float radius, float size)
        {
            for (int i = 4; i >= 1; i--)
            {
                float k = size * i / 4f;
                var rr = RectangleF.Inflate(r, k, k);
                FillRound(g, Color.FromArgb(18, c), rr, radius + k);
            }
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static bool FontExists(string family)
        {
            try
            {
                using (var ff = new FontFamily(family)) return ff.Name.Length > 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, Font> fontCache =
            new System.Collections.Generic.Dictionary<string, Font>();

        /// <summary>
        /// Font dùng chung, tạo một lần cho mỗi (tên, cỡ, kiểu) và KHÔNG bao giờ dispose.
        /// Lý do: trên .NET Framework, gán control.Font một font "bằng" font cũ (cùng tên/cỡ/kiểu)
        /// thì control vẫn giữ đối tượng cũ; nếu dispose đối tượng cũ, control sẽ lỗi
        /// ArgumentException ở lần dùng font sau đó.
        /// </summary>
        public static Font MakeFont(string family, float size, FontStyle style)
        {
            if (!FontExists(family)) family = FontExists("Segoe UI") ? "Segoe UI" : SystemFonts.MessageBoxFont.FontFamily.Name;
            string key = family + "|" + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            Font f;
            if (!fontCache.TryGetValue(key, out f))
            {
                f = new Font(family, size, style);
                fontCache[key] = f;
            }
            return f;
        }
    }

    /// <summary>Control tự vẽ, biết theme và tỉ lệ DPI</summary>
    internal abstract class ThemedControl : Control
    {
        protected Theme theme = Theme.All[0];
        protected float scale = 1f;
        protected bool hover, down;

        protected ThemedControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public void ApplyTheme(Theme t, float s)
        {
            theme = t;
            scale = s;
            Invalidate();
        }

        protected float S(float v)
        {
            return v * scale;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                down = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected void PrepareGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }
    }

    internal enum TitleGlyph
    {
        Pin,
        Menu,
        Minimize,
        Close
    }

    /// <summary>Nút trên thanh tiêu đề: ghim, ⋯, thu nhỏ, đóng</summary>
    internal sealed class TitleButton : ThemedControl
    {
        public TitleGlyph Glyph;
        public bool Checked;

        public TitleButton(TitleGlyph glyph, string tip, ToolTip tt)
        {
            Glyph = glyph;
            Cursor = Cursors.Default;
            tt.SetToolTip(this, tip);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            PrepareGraphics(g);
            bool isClose = Glyph == TitleGlyph.Close;
            Color bg = theme.Title, fg = theme.Ink;
            if (hover || down)
            {
                if (isClose)
                {
                    bg = Color.FromArgb(196, 43, 28);
                    fg = Color.White;
                }
                else
                {
                    bg = Gfx.Mix(theme.Ink, theme.Title, down ? 0.16f : 0.09f);
                }
            }
            if (Glyph == TitleGlyph.Pin && Checked && !(hover && isClose)) fg = theme.Accent;
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);

            float cx = Width / 2f, cy = Height / 2f;
            using (var pen = new Pen(fg, Math.Max(1f, S(1.2f))))
            using (var brush = new SolidBrush(fg))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                switch (Glyph)
                {
                    case TitleGlyph.Close:
                    {
                        float k = S(4.5f);
                        g.DrawLine(pen, cx - k, cy - k, cx + k, cy + k);
                        g.DrawLine(pen, cx - k, cy + k, cx + k, cy - k);
                        break;
                    }
                    case TitleGlyph.Minimize:
                    {
                        float k = S(5f);
                        g.DrawLine(pen, cx - k, cy, cx + k, cy);
                        break;
                    }
                    case TitleGlyph.Menu:
                    {
                        float r = S(1.4f), gap = S(4.5f);
                        for (int i = -1; i <= 1; i++)
                            g.FillEllipse(brush, cx + i * gap - r, cy - r, 2 * r, 2 * r);
                        break;
                    }
                    case TitleGlyph.Pin:
                    {
                        // đinh ghim: đầu hình thang + thân kim
                        var head = new[]
                        {
                            new PointF(cx - S(3f), cy - S(5.5f)), new PointF(cx + S(3f), cy - S(5.5f)),
                            new PointF(cx + S(3f), cy - S(1.5f)), new PointF(cx + S(5f), cy + S(1f)),
                            new PointF(cx - S(5f), cy + S(1f)), new PointF(cx - S(3f), cy - S(1.5f))
                        };
                        if (Checked) g.FillPolygon(brush, head);
                        else g.DrawPolygon(pen, head);
                        g.DrawLine(pen, cx, cy + S(1f), cx, cy + S(6f));
                        break;
                    }
                }
            }
        }
    }

    /// <summary>Hàng chọn kiểu gõ TELEX · VNI · VIQR · OFF</summary>
    internal sealed class ModeBar : ThemedControl
    {
        public static readonly string[] Labels = { "TELEX", "VNI", "VIQR", "OFF" };
        private int selected;
        private int hot = -1;
        private Font font;

        public event EventHandler SelectedChanged;

        public int Selected
        {
            get { return selected; }
            set
            {
                selected = value;
                Invalidate();
            }
        }

        public void SetFont(Font f)
        {
            font = f;
            Invalidate();
        }

        private RectangleF Seg(int i)
        {
            float pad = S(2), w = (Width - 2 * pad) / Labels.Length;
            return new RectangleF(pad + i * w, pad, w, Height - 2 * pad);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < Labels.Length; i++)
                if (Seg(i).Contains(e.Location)) h = i;
            if (h != hot)
            {
                hot = h;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            for (int i = 0; i < Labels.Length; i++)
            {
                if (Seg(i).Contains(e.Location) && i != selected)
                {
                    selected = i;
                    Invalidate();
                    if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
                }
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            PrepareGraphics(g);
            using (var b = new SolidBrush(theme.WinBg)) g.FillRectangle(b, ClientRectangle);
            var all = new RectangleF(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, theme.ModeBg, all, S(6));
            if (theme.BorderWidth > 1) Gfx.DrawRound(g, theme.Line, 1, all, S(6));
            if (font == null) return;

            bool off = selected == Labels.Length - 1;
            for (int i = 0; i < Labels.Length; i++)
            {
                var r = Seg(i);
                Color fg = theme.Muted;
                if (i == selected)
                {
                    if (theme.Glow && !off) Gfx.Glow(g, theme.Accent, r, S(4), S(5));
                    Gfx.FillRound(g, theme.Field, r, S(4));
                    if (theme.BorderWidth > 1) Gfx.DrawRound(g, off ? theme.Muted : theme.Accent, S(2), RectangleF.Inflate(r, -S(1), -S(1)), S(4));
                    else Gfx.DrawRound(g, Color.FromArgb(theme.IsDark ? 60 : 28, 0, 0, 0), 1, r, S(4));
                    fg = off ? theme.Muted : theme.Accent;
                }
                else if (i == hot)
                {
                    fg = theme.Ink;
                }
                Gfx.Text(g, Labels[i], font, fg, Rectangle.Round(r),
                         TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    /// <summary>Nút phẳng bo góc: Xoá / Copy</summary>
    internal sealed class FlatButton : ThemedControl
    {
        public bool Primary;
        private Font font;

        public FlatButton(string text, bool primary)
        {
            Text = text;
            Primary = primary;
            Cursor = Cursors.Hand;
        }

        public void SetFont(Font f)
        {
            font = f;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            PrepareGraphics(g);
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : theme.WinBg)) g.FillRectangle(b, ClientRectangle);
            float pad = theme.Glow && Primary ? S(4) : 0;
            var r = new RectangleF(pad, pad, Width - 1 - 2 * pad, Height - 1 - 2 * pad);
            Color bg = Primary ? theme.Accent : theme.Field;
            Color fg = Primary ? theme.AccentInk : theme.Ink;
            if (down) bg = Gfx.Mix(theme.IsDark ? Color.White : Color.Black, bg, 0.12f);
            else if (hover) bg = Gfx.Mix(theme.IsDark ? Color.White : Color.Black, bg, 0.06f);
            if (theme.Glow && Primary) Gfx.Glow(g, theme.Accent, r, S(4), S(4));
            Gfx.FillRound(g, bg, r, S(4));
            if (!Primary || theme.BorderWidth > 1)
                Gfx.DrawRound(g, Primary ? theme.Accent : theme.Line, theme.BorderWidth, r, S(4));
            if (font != null)
                Gfx.Text(g, Text, font, fg, Rectangle.Round(r),
                         TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Khung bao ô soạn thảo: viền, gạch dưới màu nhấn khi đang gõ</summary>
    internal sealed class FieldPanel : Panel
    {
        private Theme theme = Theme.All[0];
        private float scale = 1f;
        public bool Active;

        public FieldPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void ApplyTheme(Theme t, float s)
        {
            theme = t;
            scale = s;
            BackColor = t.Field;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(theme.Field)) g.FillRectangle(b, ClientRectangle);
            int bw = theme.BorderWidth;
            using (var pen = new Pen(theme.Line, bw))
            {
                pen.Alignment = PenAlignment.Inset;
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
            int under = (int)Math.Round(scale * 2);
            using (var b = new SolidBrush(Active ? theme.Accent : theme.Line))
                g.FillRectangle(b, 0, Height - under, Width, under);
        }
    }

    /// <summary>Màu menu theo theme</summary>
    internal sealed class ThemeColorTable : ProfessionalColorTable
    {
        private readonly Theme t;

        public ThemeColorTable(Theme t)
        {
            this.t = t;
            UseSystemColors = false;
        }

        private Color Hot
        {
            get { return Gfx.Mix(t.Ink, t.Field, 0.10f); }
        }

        public override Color ToolStripDropDownBackground { get { return t.Field; } }
        public override Color ImageMarginGradientBegin { get { return t.Field; } }
        public override Color ImageMarginGradientMiddle { get { return t.Field; } }
        public override Color ImageMarginGradientEnd { get { return t.Field; } }
        public override Color MenuBorder { get { return t.Line; } }
        public override Color MenuItemBorder { get { return Hot; } }
        public override Color MenuItemSelected { get { return Hot; } }
        public override Color MenuItemSelectedGradientBegin { get { return Hot; } }
        public override Color MenuItemSelectedGradientEnd { get { return Hot; } }
        public override Color MenuItemPressedGradientBegin { get { return Hot; } }
        public override Color MenuItemPressedGradientEnd { get { return Hot; } }
        public override Color SeparatorDark { get { return t.Line; } }
        public override Color SeparatorLight { get { return t.Field; } }
        public override Color CheckBackground { get { return t.Field; } }
        public override Color CheckSelectedBackground { get { return Hot; } }
        public override Color CheckPressedBackground { get { return Hot; } }
        public override Color ButtonSelectedBorder { get { return Hot; } }
    }

    internal sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Theme t;

        public ThemeMenuRenderer(Theme t) : base(new ThemeColorTable(t))
        {
            this.t = t;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? t.Ink : t.Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = t.Ink;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // dấu ✓ màu nhấn; mục có ảnh (chấm màu theme) thì vẽ viền quanh ảnh
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            if (e.Item.Image != null)
            {
                Gfx.DrawRound(g, t.Accent, 2, RectangleF.Inflate(r, 2, 2), 4);
                return;
            }
            using (var pen = new Pen(t.Accent, Math.Max(1.5f, r.Height / 8f)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new[]
                {
                    new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.55f),
                    new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.75f),
                    new PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.3f)
                });
            }
        }
    }
}
