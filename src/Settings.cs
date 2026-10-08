// VniTyping — thiết lập lưu trong VniTyping.ini cạnh file exe (portable, không ghi registry).
// Nếu thư mục chứa exe không cho ghi thì dùng %APPDATA%\VniTyping\VniTyping.ini.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VniTyping
{
    internal sealed class Settings
    {
        public const float DefaultFontSize = 10.5f;

        /// <summary>0 = TELEX, 1 = VNI, 2 = VIQR, 3 = OFF (đúng thứ tự trên ModeBar)</summary>
        public int Mode;
        /// <summary>Kiểu gõ gần nhất khác OFF, dùng cho Ctrl+Shift</summary>
        public int LastMethod;
        public string Theme = UI.Theme.DefaultId;
        public bool TopMost = true;
        public bool ModernStyle = true;
        public bool AutoRestore = true;
        public bool HornUO = true;
        public float FontSize = DefaultFontSize;
        public Rectangle Bounds = Rectangle.Empty;

        private static string PortablePath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "VniTyping.ini"); }
        }

        private static string AppDataPath
        {
            get
            {
                return Path.Combine(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VniTyping"), "VniTyping.ini");
            }
        }

        public static Settings Load()
        {
            var s = new Settings();
            // Có cả hai file (thư mục exe chỉ đọc) thì lấy file ghi gần nhất
            string path = File.Exists(PortablePath) ? PortablePath : null;
            if (File.Exists(AppDataPath) &&
                (path == null || File.GetLastWriteTimeUtc(AppDataPath) > File.GetLastWriteTimeUtc(path)))
                path = AppDataPath;
            if (path == null) return s;
            try
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && !line.StartsWith(";") && !line.StartsWith("["))
                        d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                s.Mode = Clamp(Int(d, "Mode", 0), 0, 3);
                s.LastMethod = Clamp(Int(d, "LastMethod", 0), 0, 2);
                s.Theme = Str(d, "Theme", s.Theme);
                s.TopMost = Bool(d, "TopMost", true);
                s.ModernStyle = Bool(d, "ModernStyle", true);
                s.AutoRestore = Bool(d, "AutoRestore", true);
                s.HornUO = Bool(d, "HornUO", true);
                s.FontSize = Math.Max(8f, Math.Min(24f, Float(d, "FontSize", DefaultFontSize)));
                int w = Int(d, "Width", 0), h = Int(d, "Height", 0);
                if (w > 0 && h > 0) s.Bounds = new Rectangle(Int(d, "Left", 0), Int(d, "Top", 0), w, h);
            }
            catch (Exception)
            {
                // file hỏng: dùng mặc định
            }
            return s;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; Thiết lập của VniTyping. Xoá file này để về mặc định.");
            sb.AppendLine("[VniTyping]");
            sb.AppendLine("Mode=" + Mode + "          ; 0 TELEX, 1 VNI, 2 VIQR, 3 OFF");
            sb.AppendLine("LastMethod=" + LastMethod);
            sb.AppendLine("Theme=" + Theme);
            sb.AppendLine("TopMost=" + (TopMost ? 1 : 0));
            sb.AppendLine("ModernStyle=" + (ModernStyle ? 1 : 0));
            sb.AppendLine("AutoRestore=" + (AutoRestore ? 1 : 0));
            sb.AppendLine("HornUO=" + (HornUO ? 1 : 0));
            sb.AppendLine("FontSize=" + FontSize.ToString(CultureInfo.InvariantCulture));
            if (!Bounds.IsEmpty)
            {
                sb.AppendLine("Left=" + Bounds.Left);
                sb.AppendLine("Top=" + Bounds.Top);
                sb.AppendLine("Width=" + Bounds.Width);
                sb.AppendLine("Height=" + Bounds.Height);
            }
            string text = sb.ToString();
            if (TryWrite(PortablePath, text)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppDataPath));
            }
            catch (Exception)
            {
                return;
            }
            TryWrite(AppDataPath, text);
        }

        private static bool TryWrite(string path, string text)
        {
            try
            {
                File.WriteAllText(path, text, new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static string Str(Dictionary<string, string> d, string k, string def)
        {
            string v;
            return d.TryGetValue(k, out v) ? StripComment(v) : def;
        }

        private static string StripComment(string v)
        {
            int c = v.IndexOf(';');
            return (c >= 0 ? v.Substring(0, c) : v).Trim();
        }

        private static int Int(Dictionary<string, string> d, string k, int def)
        {
            int v;
            return int.TryParse(Str(d, k, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def;
        }

        private static float Float(Dictionary<string, string> d, string k, float def)
        {
            float v;
            return float.TryParse(Str(d, k, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
        }

        private static bool Bool(Dictionary<string, string> d, string k, bool def)
        {
            string v = Str(d, k, "");
            if (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return def;
        }
    }
}
