// VniTyping — 9 bộ giao diện. Màu lấy từ bản thiết kế đã duyệt (docs/ui-design.html).
using System.Collections.Generic;
using System.Drawing;

namespace VniTyping.UI
{
    internal sealed class Theme
    {
        public string Id;
        public string Name;
        public Color WinBg;      // nền cửa sổ
        public Color Title;      // thanh tiêu đề
        public Color Field;      // ô soạn thảo, nút thường
        public Color Ink;        // chữ
        public Color Muted;      // chữ phụ
        public Color Line;       // viền
        public Color Accent;     // màu nhấn
        public Color AccentInk;  // chữ trên nền màu nhấn
        public Color ModeBg;     // nền hàng chọn kiểu gõ
        public Color TitleStripe = Color.Empty;   // Hoa Phượng: viền lá dưới thanh tiêu đề
        public bool IsDark;
        public bool Glow;        // Phố Neon: nút phát sáng
        public bool MonoLabels;  // Mực Đêm: nhãn chữ mono
        public int BorderWidth = 1;
        public string UiFont = "Segoe UI";
        public string EditorFont = "Segoe UI";

        private static Color C(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        private static Theme Make(string id, string name, int winBg, int title, int field, int ink, int muted,
                                  int line, int accent, int accentInk, bool dark)
        {
            var t = new Theme();
            t.Id = id;
            t.Name = name;
            t.WinBg = C(winBg);
            t.Title = C(title);
            t.Field = C(field);
            t.Ink = C(ink);
            t.Muted = C(muted);
            t.Line = C(line);
            t.Accent = C(accent);
            t.AccentInk = C(accentInk);
            t.IsDark = dark;
            t.ModeBg = Gfx.Mix(t.Ink, t.WinBg, dark ? 0.10f : 0.06f);
            return t;
        }

        public static readonly List<Theme> All = Build();

        public const string DefaultId = "sen";

        private static List<Theme> Build()
        {
            var list = new List<Theme>();
            Theme t;

            list.Add(Make("sen", "Sen", 0xf5f7f6, 0xe7edea, 0xffffff, 0x1c2b33, 0x687784, 0xd3ddd9, 0x1f7a6c, 0xffffff, false));

            t = Make("giay", "Giấy Dó", 0xeee5d2, 0xe2d4b5, 0xfaf6ea, 0x2b2620, 0x7a6d57, 0xcfbd93, 0x9c3b2e, 0xffffff, false);
            // Georgia thiếu chữ hai dấu (ế, ầ…) trên Windows → dùng font có chân hỗ trợ đủ tiếng Việt
            t.UiFont = "Cambria";
            t.EditorFont = "Cambria";
            list.Add(t);

            t = Make("dem", "Mực Đêm", 0x1a2025, 0x12161a, 0x12161a, 0xe7e4dc, 0x8b959d, 0x2e373e, 0xc9a24b, 0x12161a, true);
            t.MonoLabels = true;
            list.Add(t);

            t = Make("neon", "Phố Neon", 0x241a3d, 0x1c1030, 0x1c1030, 0xf4eefc, 0xa794c9, 0x3d2c60, 0xff3d81, 0xffffff, true);
            t.Glow = true;
            list.Add(t);

            t = Make("phuong", "Hoa Phượng", 0xfff8f0, 0xfde6d4, 0xffffff, 0x3a1f14, 0x8a6352, 0xf0cdb6, 0xd9481c, 0xffffff, false);
            t.TitleStripe = C(0x2f8a3c);
            list.Add(t);

            t = Make("daodao", "Anh Đào", 0xfdf3f6, 0xf8e1e9, 0xffffff, 0x3b2530, 0x8d6878, 0xefcfdb, 0xc2456e, 0xffffff, false);
            t.ModeBg = C(0xf6dce6);
            list.Add(t);

            list.Add(Make("halong", "Vịnh Hạ Long", 0x123138, 0x0c2328, 0x0c2328, 0xdff3ef, 0x86aaa6, 0x22474f, 0x3fc1b0, 0x06201c, true));

            list.Add(Make("caphe", "Cà Phê Sữa", 0xe9dccb, 0xd9c5ab, 0xf5ede2, 0x2e1f14, 0x76604c, 0xc9b292, 0x6b4426, 0xf5ede2, false));

            t = Make("tuongphan", "Tương Phản Cao", 0x000000, 0x000000, 0x000000, 0xffffff, 0xd0d0d0, 0xffffff, 0xffd400, 0x000000, true);
            t.BorderWidth = 2;
            t.ModeBg = C(0x000000);
            list.Add(t);

            return list;
        }

        public static Theme ById(string id)
        {
            foreach (var t in All)
                if (t.Id == id) return t;
            return All[0];
        }
    }
}
