// VniTyping — bảng phím tắt và cách gõ (F1), tự đổi theo kiểu gõ đang chọn.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace VniTyping.UI
{
    internal sealed class HelpForm : Form
    {
        private readonly TextBox box = new TextBox();
        private int mode = -1;

        private const string Shortcuts =
            "PHÍM TẮT\r\n" +
            "  Ctrl+Enter          Copy toàn bộ văn bản\r\n" +
            "  Ctrl+Shift+Enter    Copy rồi xoá trắng\r\n" +
            "  Ctrl+Del            Xoá hết\r\n" +
            "  Ctrl+1 / 2 / 3 / 4  TELEX / VNI / VIQR / OFF\r\n" +
            "  Ctrl+Shift          Bật/tắt nhanh tiếng Việt\r\n" +
            "  Ctrl+T              Bật/tắt luôn nổi trên cùng\r\n" +
            "  Ctrl+cuộn chuột     Đổi cỡ chữ (Ctrl+0: mặc định)\r\n" +
            "  Esc  hoặc  –        Ẩn xuống khay hệ thống\r\n" +
            "  Ctrl+Alt+V          Gọi cửa sổ lên (ở bất kỳ đâu)\r\n" +
            "  ✕                   Thoát hẳn\r\n";

        private static readonly string[] Guides =
        {
            "CÁCH GÕ TELEX\r\n" +
            "  s sắc · f huyền · r hỏi · x ngã · j nặng · z xoá dấu\r\n" +
            "  aa â · aw ă · ee ê · oo ô · ow ơ · uw ư · dd đ\r\n" +
            "  w đứng riêng → ư · [ → ơ · ] → ư\r\n\r\n" +
            "  chaof → chào     vieetj → việt     tieengs → tiếng\r\n" +
            "  cuowngf → cường (ươ chỉ cần một w)\r\n" +
            "  hoafn → hoàn (gõ dấu ở đâu cũng được)\r\n" +
            "  Gõ lại phím dấu để huỷ: ass → as, ww → w\r\n",

            "CÁCH GÕ VNI\r\n" +
            "  1 sắc · 2 huyền · 3 hỏi · 4 ngã · 5 nặng · 0 xoá dấu\r\n" +
            "  6 dấu mũ (â ê ô) · 7 dấu móc (ơ ư) · 8 dấu trăng (ă) · 9 đ\r\n\r\n" +
            "  cha2o → chào     vie65t → việt     tie61ng → tiếng\r\n" +
            "  nguoi72 → người (ươ chỉ cần một phím 7)\r\n",

            "CÁCH GÕ VIQR\r\n" +
            "  ' sắc · ` huyền · ? hỏi · ~ ngã · . nặng · = hoặc 0 xoá dấu\r\n" +
            "  ^ dấu mũ (â ê ô) · + dấu móc (ơ ư) · ( dấu trăng (ă) · dd đ\r\n\r\n" +
            "  cha`o → chào     vie^.t → việt     tie^'ng → tiếng\r\n" +
            "  Gõ dấu câu ngay sau chữ: thêm \\ phía trước, chao\\. → chao.\r\n",

            "BỘ GÕ ĐANG TẮT\r\n" +
            "  Phím đi thẳng vào ô gõ như bình thường.\r\n" +
            "  Chọn TELEX, VNI hoặc VIQR (hoặc Ctrl+Shift) để gõ tiếng Việt.\r\n"
        };

        private const string Notes =
            "GHI CHÚ\r\n" +
            "  Từ tiếng Anh như google, address, windows được giữ nguyên.\r\n" +
            "  Thiết lập lưu trong VniTyping.ini cạnh file exe.\r\n";

        public HelpForm(float scale)
        {
            Text = "VniTyping · Phím tắt & cách gõ";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size((int)(500 * scale), (int)(420 * scale));
            Padding = new Padding((int)(12 * scale));
            box.Multiline = true;
            box.ReadOnly = true;
            box.BorderStyle = BorderStyle.None;
            box.ScrollBars = ScrollBars.Vertical;
            box.Dock = DockStyle.Fill;
            box.TabStop = false;
            Controls.Add(box);
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.F1) Close();
            };
        }

        public void ApplyTheme(Theme t, Font uiFont)
        {
            BackColor = t.Field;
            box.BackColor = t.Field;
            box.ForeColor = t.Ink;
            var old = box.Font;
            box.Font = Gfx.MakeFont("Consolas", uiFont.SizeInPoints, FontStyle.Regular);
            if (old != null && old != Control.DefaultFont) old.Dispose();
        }

        public void SetMode(int m)
        {
            if (m == mode) return;
            mode = m;
            box.Text = Guides[Math.Max(0, Math.Min(3, m))] + "\r\n" + Shortcuts + "\r\n" + Notes;
            box.Select(0, 0);
        }
    }
}
