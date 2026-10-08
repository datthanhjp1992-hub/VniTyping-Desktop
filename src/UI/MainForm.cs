// VniTyping — cửa sổ chính (theo docs/ui-design.html đã duyệt).
// Cửa sổ không viền, tự vẽ thanh tiêu đề để theo được màu của từng giao diện.
using System;
using System.Drawing;
using System.Windows.Forms;
using VniTyping.Engine;

namespace VniTyping.UI
{
    internal sealed class MainForm : Form
    {
        private const int HotKeyId = 1;
        public static readonly int ShowMessage = RegisterShowMessage();

        // kích thước thiết kế ở 96 DPI
        private const int TitleH = 32, Pad = 10, ModeH = 28, Gap = 8, FootH = 28, TitleBtnW = 40, Grip = 6;

        private readonly Settings settings;
        private readonly VnEngine engine = new VnEngine(InputMethod.Telex);
        private readonly float scale = 1f;
        private Theme theme;

        private readonly ToolTip tips = new ToolTip();
        private readonly TitleButton btnPin, btnMenu, btnMin, btnClose;
        private readonly ModeBar modeBar = new ModeBar();
        private readonly FieldPanel field = new FieldPanel();
        private readonly TextBox editor = new TextBox();
        private readonly FlatButton btnClear = new FlatButton("Xoá", false);
        private readonly FlatButton btnCopy = new FlatButton("Copy", true);
        private readonly ContextMenuStrip optionsMenu = new ContextMenuStrip();
        private readonly TrayIcon tray = new TrayIcon();
        private readonly Timer statusTimer = new Timer();

        private ToolStripMenuItem miTop, miModern, miRestore, miHorn;
        private ToolStripMenuItem[] miThemes;

        private Font uiFont, uiBold, labelFont, titleFont, editorFont;
        private Rectangle statusRect;
        private string statusOverride;
        private bool statusOk;
        private bool toggleArmed;
        private bool hotKeyOk;
        private bool scrollbarPending;
        private HelpForm helpForm;
        private Icon ownIcon;   // icon do app tạo (không dispose icon mặc định dùng chung của WinForms)

        private static int RegisterShowMessage()
        {
            try
            {
                return NativeMethods.RegisterWindowMessage("VniTyping.ShowWindow.7d3f");
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public MainForm(Settings settings)
        {
            this.settings = settings;
            using (var g = CreateGraphics()) scale = g.DpiX / 96f;

            Text = "VniTyping";
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            MinimumSize = new Size(S(280), S(200));

            btnPin = new TitleButton(TitleGlyph.Pin, "Luôn nổi trên cùng (Ctrl+T)", tips);
            btnMenu = new TitleButton(TitleGlyph.Menu, "Tuỳ chọn", tips);
            btnMin = new TitleButton(TitleGlyph.Minimize, "Ẩn xuống khay hệ thống (Esc)", tips);
            btnClose = new TitleButton(TitleGlyph.Close, "Thoát", tips);
            tips.SetToolTip(btnClear, "Xoá hết (Ctrl+Del)");
            tips.SetToolTip(btnCopy, "Copy tất cả (Ctrl+Enter)");

            editor.Multiline = true;
            editor.BorderStyle = BorderStyle.None;
            editor.ScrollBars = ScrollBars.None;   // chỉ hiện khi văn bản dài hơn ô (UpdateScrollbar)
            editor.WordWrap = true;
            editor.AcceptsReturn = true;
            editor.HideSelection = false;
            editor.MaxLength = 0;
            field.Controls.Add(editor);

            Controls.AddRange(new Control[] { btnPin, btnMenu, btnMin, btnClose, modeBar, field, btnClear, btnCopy });

            BuildOptionsMenu();
            WireEvents();

            // trạng thái từ file ini
            engine.ModernStyle = settings.ModernStyle;
            engine.AutoRestore = settings.AutoRestore;
            engine.HornUO = settings.HornUO;
            TopMost = settings.TopMost;
            PlaceWindow();
            ApplyTheme(Theme.ById(settings.Theme));
            SetMode(settings.Mode);
            SyncMenuChecks();
            tray.Visible = true;
            ActiveControl = editor;
        }

        private int S(float v)
        {
            return (int)Math.Round(v * scale);
        }

        // ===================================================================
        // Khởi tạo
        // ===================================================================
        private void WireEvents()
        {
            btnPin.Click += delegate { SetTopMost(!settings.TopMost); };
            btnMenu.Click += delegate
            {
                optionsMenu.Show(btnMenu, new Point(btnMenu.Width, btnMenu.Height), ToolStripDropDownDirection.BelowLeft);
            };
            btnMin.Click += delegate { HideToTray(); };
            btnClose.Click += delegate { Close(); };
            btnClear.Click += delegate { ClearText(); };
            btnCopy.Click += delegate
            {
                FinishWord();
                Copy();
                editor.Focus();
            };
            modeBar.SelectedChanged += delegate
            {
                SetMode(modeBar.Selected);
                editor.Focus();
            };

            editor.KeyDown += Editor_KeyDown;
            editor.KeyUp += Editor_KeyUp;
            editor.KeyPress += Editor_KeyPress;
            editor.MouseWheel += Editor_MouseWheel;
            editor.TextChanged += delegate
            {
                InvalidateStatus();
                QueueScrollbarUpdate();
            };
            editor.Resize += delegate { QueueScrollbarUpdate(); };
            editor.GotFocus += delegate { SetFieldActive(true); };
            editor.LostFocus += delegate { SetFieldActive(false); };

            statusTimer.Interval = 1500;
            statusTimer.Tick += delegate
            {
                statusTimer.Stop();
                statusOverride = null;
                InvalidateStatus();
            };

            tray.ShowRequested += delegate { ShowFromTray(); };
            tray.ExitRequested += delegate { Close(); };
            tray.ModeRequested += delegate(object s, ModeEventArgs e) { SetMode(e.Mode); };
        }

        private ToolStripMenuItem Item(string text, string shortcut, EventHandler click)
        {
            var mi = new ToolStripMenuItem(text);
            if (shortcut != null) mi.ShortcutKeyDisplayString = shortcut;
            mi.Click += click;
            return mi;
        }

        private void BuildOptionsMenu()
        {
            miTop = Item("Luôn nổi trên cùng", "Ctrl+T", delegate { SetTopMost(!settings.TopMost); });
            miModern = Item("Bỏ dấu kiểu mới (hoà, thuỷ)", null, delegate
            {
                settings.ModernStyle = engine.ModernStyle = !settings.ModernStyle;
                SyncMenuChecks();
            });
            miRestore = Item("Giữ nguyên từ tiếng Anh", null, delegate
            {
                settings.AutoRestore = engine.AutoRestore = !settings.AutoRestore;
                SyncMenuChecks();
            });
            miHorn = Item("Gõ ươ bằng một phím w", null, delegate
            {
                settings.HornUO = engine.HornUO = !settings.HornUO;
                SyncMenuChecks();
            });

            var miTheme = new ToolStripMenuItem("Giao diện");
            miThemes = new ToolStripMenuItem[Theme.All.Count];
            for (int i = 0; i < Theme.All.Count; i++)
            {
                var t = Theme.All[i];
                miThemes[i] = Item(t.Name, null, delegate
                {
                    ApplyTheme(t);
                    SyncMenuChecks();
                });
                miThemes[i].Image = Swatch(t);
                miTheme.DropDownItems.Add(miThemes[i]);
            }

            var miFont = new ToolStripMenuItem("Cỡ chữ");
            miFont.DropDownItems.Add(Item("Lớn hơn", "Ctrl+cuộn lên", delegate { ZoomFont(0.5f); }));
            miFont.DropDownItems.Add(Item("Nhỏ hơn", "Ctrl+cuộn xuống", delegate { ZoomFont(-0.5f); }));
            miFont.DropDownItems.Add(Item("Mặc định", "Ctrl+0", delegate { ZoomFont(0); }));

            optionsMenu.Items.AddRange(new ToolStripItem[]
            {
                miTop, miModern, miRestore, miHorn,
                new ToolStripSeparator(),
                miTheme, miFont,
                new ToolStripSeparator(),
                Item("Phím tắt && cách gõ", "F1", delegate { ShowHelp(); }),
                Item("Giới thiệu && giấy phép", null, delegate { ShowAbout(); }),
                new ToolStripSeparator(),
                Item("Thoát", null, delegate { Close(); })
            });
        }

        private Bitmap Swatch(Theme t)
        {
            int n = S(14);
            var bmp = new Bitmap(n, n);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(t.WinBg)) g.FillEllipse(b, 0, 0, n - 1, n - 1);
                using (var b = new SolidBrush(t.Accent)) g.FillPie(b, 0, 0, n - 1, n - 1, 135, 180);
                using (var p = new Pen(Color.FromArgb(70, 0, 0, 0))) g.DrawEllipse(p, 0, 0, n - 1, n - 1);
            }
            return bmp;
        }

        private void SyncMenuChecks()
        {
            miTop.Checked = settings.TopMost;
            miModern.Checked = settings.ModernStyle;
            miRestore.Checked = settings.AutoRestore;
            miHorn.Checked = settings.HornUO;
            for (int i = 0; i < miThemes.Length; i++) miThemes[i].Checked = Theme.All[i] == theme;
        }

        /// <summary>Khôi phục vị trí cũ nếu còn nằm trên một màn hình, không thì góc dưới phải</summary>
        private void PlaceWindow()
        {
            var b = settings.Bounds;
            if (!b.IsEmpty)
            {
                foreach (var scr in Screen.AllScreens)
                {
                    if (scr.WorkingArea.IntersectsWith(b))
                    {
                        Bounds = b;
                        return;
                    }
                }
            }
            var wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(S(360), S(280));
            Location = new Point(wa.Right - Width - S(24), wa.Bottom - Height - S(24));
        }

        // ===================================================================
        // Giao diện
        // ===================================================================
        private void ApplyTheme(Theme t)
        {
            theme = t;
            settings.Theme = t.Id;

            uiFont = Gfx.MakeFont(t.UiFont, 9f, FontStyle.Regular);
            uiBold = Gfx.MakeFont(t.UiFont, 9f, FontStyle.Bold);
            labelFont = t.MonoLabels ? Gfx.MakeFont("Consolas", 8.5f, FontStyle.Bold) : Gfx.MakeFont(t.UiFont, 8.25f, FontStyle.Bold);
            titleFont = t.MonoLabels ? Gfx.MakeFont("Consolas", 8.5f, FontStyle.Regular) : Gfx.MakeFont(t.UiFont, 9f, FontStyle.Regular);
            editorFont = Gfx.MakeFont(t.EditorFont, settings.FontSize, FontStyle.Regular);

            BackColor = t.WinBg;
            foreach (Control c in Controls)
            {
                var tc = c as ThemedControl;
                if (tc != null) tc.ApplyTheme(t, scale);
            }
            modeBar.SetFont(labelFont);
            btnClear.SetFont(uiFont);
            btnCopy.SetFont(uiBold);
            field.ApplyTheme(t, scale);
            editor.BackColor = t.Field;
            editor.ForeColor = t.Ink;
            editor.Font = editorFont;
            optionsMenu.Renderer = new ThemeMenuRenderer(t);
            optionsMenu.Font = uiFont;
            ApplyEditorScrollbarTheme();
            UpdateIcons();
            DoLayout();
            Invalidate(true);

            if (helpForm != null && !helpForm.IsDisposed) helpForm.ApplyTheme(t, uiFont);
        }

        private void ApplyEditorScrollbarTheme()
        {
            if (!editor.IsHandleCreated) return;
            try
            {
                NativeMethods.SetWindowTheme(editor.Handle, theme.IsDark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch (Exception)
            {
                // Windows cũ: thanh cuộn giữ màu mặc định
            }
        }

        private void UpdateIcons()
        {
            bool off = settings.Mode == 3;
            var old = ownIcon;
            ownIcon = AppIcon.Make(S(32), theme, off);
            Icon = ownIcon;
            if (old != null) old.Dispose();
            tray.Update(theme, settings.Mode);
        }

        /// <summary>
        /// Đổi ScrollBars làm TextBox tạo lại cửa sổ con, nên không làm ngay giữa lúc
        /// TextBox đang xử lý phím: hoãn tới khi xử lý xong.
        /// </summary>
        private void QueueScrollbarUpdate()
        {
            if (scrollbarPending || !IsHandleCreated) return;
            scrollbarPending = true;
            BeginInvoke(new MethodInvoker(delegate
            {
                scrollbarPending = false;
                UpdateScrollbar();
            }));
        }

        /// <summary>Thanh cuộn chỉ hiện khi số dòng (kể cả dòng tự ngắt) vượt chiều cao ô</summary>
        private void UpdateScrollbar()
        {
            if (!editor.IsHandleCreated) return;
            bool need = false;
            if (editor.TextLength > 0)
            {
                int lines = editor.GetLineFromCharIndex(editor.TextLength) + 1;
                need = lines * editor.Font.Height > editor.ClientSize.Height;
            }
            var want = need ? ScrollBars.Vertical : ScrollBars.None;
            if (editor.ScrollBars == want) return;
            int s = editor.SelectionStart, len = editor.SelectionLength;
            editor.ScrollBars = want;
            editor.Select(s, len);
            editor.ScrollToCaret();
            ApplyEditorScrollbarTheme();
        }

        private void SetFieldActive(bool on)
        {
            field.Active = on;
            field.Invalidate();
        }

        private void SetTopMost(bool on)
        {
            settings.TopMost = on;
            TopMost = on;
            btnPin.Checked = on;
            btnPin.Invalidate();
            SyncMenuChecks();
        }

        private void ZoomFont(float delta)
        {
            float size = delta == 0 ? Settings.DefaultFontSize : settings.FontSize + delta;
            settings.FontSize = Math.Max(8f, Math.Min(24f, size));
            editorFont = Gfx.MakeFont(theme.EditorFont, settings.FontSize, FontStyle.Regular);
            editor.Font = editorFont;
            QueueScrollbarUpdate();
            Flash("Cỡ chữ " + settings.FontSize + "pt", true);
        }

        // ===================================================================
        // Bố cục và vẽ
        // ===================================================================
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            DoLayout();
        }

        private void DoLayout()
        {
            if (btnClose == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            int bw = theme != null ? theme.BorderWidth : 1;
            int th = S(TitleH), pad = S(Pad), bwid = S(TitleBtnW);

            btnClose.Bounds = new Rectangle(w - bw - bwid, bw, bwid, th - bw);
            btnMin.Bounds = new Rectangle(btnClose.Left - bwid, bw, bwid, th - bw);
            btnMenu.Bounds = new Rectangle(btnMin.Left - bwid, bw, bwid, th - bw);
            btnPin.Bounds = new Rectangle(btnMenu.Left - bwid, bw, bwid, th - bw);
            btnPin.Checked = settings.TopMost;

            modeBar.Bounds = new Rectangle(pad, th + pad, w - 2 * pad, S(ModeH));

            int footY = h - pad - S(FootH);
            btnCopy.Bounds = new Rectangle(w - pad - S(66), footY, S(66), S(FootH));
            btnClear.Bounds = new Rectangle(btnCopy.Left - S(6) - S(56), footY, S(56), S(FootH));
            statusRect = new Rectangle(pad, footY, Math.Max(0, btnClear.Left - pad - S(6)), S(FootH));

            int fieldY = modeBar.Bottom + S(Gap);
            field.Bounds = new Rectangle(pad, fieldY, w - 2 * pad, Math.Max(S(30), footY - S(Gap) - fieldY));
            editor.Bounds = new Rectangle(S(8), S(6), Math.Max(10, field.Width - S(10)), Math.Max(10, field.Height - S(12)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int w = ClientSize.Width, th = S(TitleH);
            using (var b = new SolidBrush(theme.WinBg)) g.FillRectangle(b, ClientRectangle);
            using (var b = new SolidBrush(theme.Title)) g.FillRectangle(b, 0, 0, w, th);
            if (!theme.TitleStripe.IsEmpty)
                using (var b = new SolidBrush(theme.TitleStripe)) g.FillRectangle(b, 0, th - S(2), w, S(2));

            bool off = settings.Mode == 3;
            int logo = S(16);
            AppIcon.Draw(g, new RectangleF(S(10), (th - logo) / 2f, logo, logo), theme, off);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var titleRect = new Rectangle(S(34), 0, Math.Max(0, btnPin.Left - S(34)), th);
            Gfx.Text(g, off ? "VniTyping · OFF" : "VniTyping", titleFont, theme.Ink, titleRect,
                     TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            Gfx.Text(g, StatusText(), uiFont, statusOverride != null && statusOk ? theme.Accent : theme.Muted, statusRect,
                     TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            using (var pen = new Pen(theme.Line, theme.BorderWidth))
            {
                pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                g.DrawRectangle(pen, 0, 0, w - 1, ClientSize.Height - 1);
            }
        }

        private string StatusText()
        {
            if (statusOverride != null) return statusOverride;
            return CountChars(editor.Text) + " ký tự · " + ModeBar.Labels[settings.Mode];
        }

        private static int CountChars(string s)
        {
            int n = s.Length;
            foreach (char c in s)
                if (c == '\r') n--;
            return n;
        }

        private void InvalidateStatus()
        {
            Invalidate(statusRect);
        }

        private void Flash(string message, bool ok)
        {
            statusOverride = message;
            statusOk = ok;
            statusTimer.Stop();
            statusTimer.Start();
            InvalidateStatus();
        }

        // ===================================================================
        // Kiểu gõ và gõ phím
        // ===================================================================
        private void SetMode(int mode)
        {
            settings.Mode = mode;
            if (mode < 3)
            {
                settings.LastMethod = mode;
                engine.Method = (InputMethod)mode;
            }
            engine.Enabled = mode != 3;
            modeBar.Selected = mode;
            UpdateIcons();
            Invalidate();
            if (helpForm != null && !helpForm.IsDisposed) helpForm.SetMode(mode);
        }

        private void ToggleVietnamese()
        {
            SetMode(settings.Mode == 3 ? settings.LastMethod : 3);
            Flash(settings.Mode == 3 ? "Đã tắt tiếng Việt" : "Tiếng Việt: " + ModeBar.Labels[settings.Mode], true);
        }

        private void ApplyEdit(Edit r, int start, int length)
        {
            int from = Math.Max(0, start - r.Backs);
            editor.Select(from, start - from + length);
            editor.SelectedText = r.Text;
        }

        /// <summary>Chốt từ đang gõ (trả lại tiếng Anh nếu cần) trước khi copy / xuống dòng</summary>
        private void FinishWord()
        {
            if (!engine.Enabled) return;
            if (editor.SelectionLength > 0)
            {
                engine.Reset();
                return;
            }
            int s = editor.SelectionStart;
            engine.EnsureSync(editor.Text, s);
            var r = engine.EndWord();
            if (!r.IsEmpty) ApplyEdit(r, s, 0);
        }

        private void Editor_KeyPress(object sender, KeyPressEventArgs e)
        {
            char ch = e.KeyChar;
            if (!engine.Enabled || ch < ' ' || ch == (char)127) return;
            var mods = ModifierKeys;
            // Ctrl+phím là lệnh; Ctrl+Alt (AltGr) vẫn là ký tự
            if ((mods & Keys.Control) != 0 && (mods & Keys.Alt) == 0) return;
            int s = editor.SelectionStart, len = editor.SelectionLength;
            engine.EnsureSync(editor.Text, s);
            var r = engine.Process(ch);
            e.Handled = true;
            ApplyEdit(r, s, len);
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            var k = e.KeyCode;
            bool ctrl = e.Control, shift = e.Shift, alt = e.Alt;

            // Ctrl+Shift (nhấn rồi thả, không kèm phím khác) → bật/tắt tiếng Việt
            if (!alt && ((k == Keys.ShiftKey && ctrl) || (k == Keys.ControlKey && shift)))
            {
                toggleArmed = true;
                return;
            }
            toggleArmed = false;

            if (k == Keys.Enter && ctrl && !alt)
            {
                FinishWord();
                Copy();
                if (shift) ClearText();
                e.SuppressKeyPress = true;
                return;
            }
            if ((k == Keys.Enter || k == Keys.Tab) && !ctrl && !alt)
            {
                FinishWord();   // ô nhập tự xuống dòng sau đó
                return;
            }
            if (k == Keys.Escape)
            {
                HideToTray();
                e.SuppressKeyPress = true;
                return;
            }
            if (k == Keys.F1)
            {
                ShowHelp();
                e.SuppressKeyPress = true;
                return;
            }
            if (!ctrl || alt || shift) return;

            bool handled = true;
            switch (k)
            {
                case Keys.D1: case Keys.NumPad1: SetMode(0); break;
                case Keys.D2: case Keys.NumPad2: SetMode(1); break;
                case Keys.D3: case Keys.NumPad3: SetMode(2); break;
                case Keys.D4: case Keys.NumPad4: SetMode(3); break;
                case Keys.T: SetTopMost(!settings.TopMost); break;
                case Keys.A: editor.SelectAll(); break;
                case Keys.Delete: ClearText(); break;
                case Keys.Back: DeleteWordLeft(); break;
                case Keys.D0: case Keys.NumPad0: ZoomFont(0); break;
                case Keys.Oemplus: case Keys.Add: ZoomFont(0.5f); break;
                case Keys.OemMinus: case Keys.Subtract: ZoomFont(-0.5f); break;
                default: handled = false; break;
            }
            if (handled) e.SuppressKeyPress = true;
        }

        private void Editor_KeyUp(object sender, KeyEventArgs e)
        {
            if (toggleArmed && (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.ControlKey))
            {
                toggleArmed = false;
                ToggleVietnamese();
            }
        }

        private void Editor_MouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == 0) return;
            ZoomFont(e.Delta > 0 ? 0.5f : -0.5f);
            var he = e as HandledMouseEventArgs;
            if (he != null) he.Handled = true;
        }

        /// <summary>Ctrl+Backspace: TextBox của WinForms chèn ký tự lạ, nên tự xoá từ bên trái</summary>
        private void DeleteWordLeft()
        {
            if (editor.SelectionLength > 0)
            {
                editor.SelectedText = "";
                return;
            }
            string t = editor.Text;
            int s = editor.SelectionStart, i = s;
            while (i > 0 && char.IsWhiteSpace(t[i - 1])) i--;
            while (i > 0 && !char.IsWhiteSpace(t[i - 1])) i--;
            editor.Select(i, s - i);
            editor.SelectedText = "";
        }

        private void ClearText()
        {
            editor.Clear();
            engine.Reset();
            editor.Focus();
        }

        private void Copy()
        {
            string text = editor.Text;
            if (text.Length == 0)
            {
                Flash("Chưa có gì để copy", false);
                return;
            }
            try
            {
                Clipboard.SetDataObject(text, true, 10, 50);
                Flash("Đã copy " + CountChars(text) + " ký tự ✓", true);
            }
            catch (Exception)
            {
                Flash("Không copy được, hãy thử lại", false);
            }
        }

        // ===================================================================
        // Khay hệ thống, phím tắt toàn cục, đóng app
        // ===================================================================
        private void HideToTray()
        {
            FinishWord();
            Hide();
            tray.ShowHintOnce();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            try
            {
                NativeMethods.SetForegroundWindow(Handle);
            }
            catch (Exception)
            {
                // bỏ qua
            }
            editor.Focus();
        }

        private void ShowHelp()
        {
            if (helpForm == null || helpForm.IsDisposed) helpForm = new HelpForm(scale);
            helpForm.ApplyTheme(theme, uiFont);
            helpForm.SetMode(settings.Mode);
            helpForm.TopMost = TopMost;
            if (!helpForm.Visible) helpForm.Show(this);
            helpForm.Activate();
        }

        private void ShowAbout()
        {
            MessageBox.Show(this,
                "VniTyping " + Application.ProductVersion + "\n" +
                "Bộ gõ tiếng Việt portable cho Windows.\n\n" +
                "Copyright © 2026 Nguyễn Thành Đạt\n" +
                "Engine kế thừa ý tưởng và bảng dữ liệu từ UniKey 3.62,\n" +
                "Copyright © 1998–2002 Phạm Kim Long.\n\n" +
                "Phát hành theo GNU General Public License phiên bản 2\n" +
                "hoặc (tuỳ bạn chọn) bất kỳ phiên bản nào mới hơn.\n" +
                "Chương trình KHÔNG CÓ BẤT KỲ BẢO ĐẢM NÀO.",
                "Giới thiệu VniTyping", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;
                cp.Style |= NativeMethods.WS_MINIMIZEBOX;   // thu nhỏ được từ thanh taskbar
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                hotKeyOk = NativeMethods.RegisterHotKey(Handle, HotKeyId,
                    NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, (int)Keys.V);
            }
            catch (Exception)
            {
                hotKeyOk = false;
            }
            try
            {
                int round = NativeMethods.DWMWCP_ROUND;   // Windows 11: bo góc cửa sổ
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            }
            catch (Exception)
            {
                // Windows 10: góc vuông
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyEditorScrollbarTheme();
            editor.Select(editor.TextLength, 0);
            editor.Focus();
            if (!hotKeyOk) Flash("Ctrl+Alt+V đang bị app khác dùng", false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (hotKeyOk)
            {
                try
                {
                    NativeMethods.UnregisterHotKey(Handle, HotKeyId);
                }
                catch (Exception)
                {
                    // bỏ qua
                }
            }
            base.OnHandleDestroyed(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                // thu nhỏ từ taskbar cũng ẩn xuống khay
                WindowState = FormWindowState.Normal;
                HideToTray();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotKeyId)
            {
                if (Visible && ActiveForm == this) HideToTray();
                else ShowFromTray();
                return;
            }
            if (ShowMessage != 0 && m.Msg == ShowMessage)
            {
                ShowFromTray();
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == NativeMethods.WM_NCHITTEST && m.Result.ToInt32() == NativeMethods.HTCLIENT)
                m.Result = new IntPtr(HitTest(PointToClient(Cursor.Position)));
        }

        /// <summary>Viền để kéo giãn, thanh tiêu đề để kéo di chuyển</summary>
        private int HitTest(Point p)
        {
            int g = S(Grip), w = ClientSize.Width, h = ClientSize.Height;
            bool left = p.X < g, right = p.X >= w - g, top = p.Y < g, bottom = p.Y >= h - g;
            if (top && left) return NativeMethods.HTTOPLEFT;
            if (top && right) return NativeMethods.HTTOPRIGHT;
            if (bottom && left) return NativeMethods.HTBOTTOMLEFT;
            if (bottom && right) return NativeMethods.HTBOTTOMRIGHT;
            if (left) return NativeMethods.HTLEFT;
            if (right) return NativeMethods.HTRIGHT;
            if (top) return NativeMethods.HTTOP;
            if (bottom) return NativeMethods.HTBOTTOM;
            if (p.Y < S(TitleH)) return NativeMethods.HTCAPTION;
            return NativeMethods.HTCLIENT;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            settings.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            settings.Save();
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tray.Dispose();
                statusTimer.Dispose();
                tips.Dispose();
                optionsMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
