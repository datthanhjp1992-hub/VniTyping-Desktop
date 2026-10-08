// Chạy bộ test chung cho engine C#:  VniTyping.Tests.exe tests\cases.json
//
// cases.json chép nguyên từ repo VniTyping (bản web). Mỗi ca mô phỏng một ô nhập:
// gõ lần lượt từng phím, áp { Backs, Text } vào văn bản, giống hệt MainForm.
// Token đặc biệt trong "input":
//   {BS}          Backspace (ô nhập tự xoá, engine đồng bộ lại ở phím sau)
//   {ENTER}       kết thúc từ rồi xuống dòng
//   {PASTE:xxx}   dán "xxx" vào trước con trỏ (engine không được báo)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using VniTyping.Engine;

namespace VniTyping.Tests
{
    internal static class TestRunner
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string path = args.Length > 0 ? args[0] : Path.Combine("tests", "cases.json");
            var groups = (List<object>)new Json(File.ReadAllText(path, Encoding.UTF8)).Parse();
            int total = 0, fail = 0;
            foreach (Dictionary<string, object> g in groups)
            {
                foreach (Dictionary<string, object> c in (List<object>)g["cases"])
                {
                    total++;
                    string method = (string)(Get(c, "method") ?? Get(g, "method") ?? "telex");
                    var opts = (Dictionary<string, object>)(Get(c, "opts") ?? Get(g, "opts"));
                    var macros = (Dictionary<string, object>)Get(c, "macros");
                    string input = (string)c["input"], expect = (string)c["expect"];
                    string got = Type(method, opts, macros, input);
                    if (got != expect)
                    {
                        fail++;
                        Console.WriteLine("FAIL [" + g["name"] + "] " + Quote(input) + " -> " + Quote(got) +
                                          "  (mong đợi " + Quote(expect) + ")");
                    }
                }
            }
            Console.WriteLine((total - fail) + "/" + total + " ca đạt");
            return fail == 0 ? 0 : 1;
        }

        private static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\n", "\\n") + "\"";
        }

        private static InputMethod MethodOf(string name)
        {
            switch (name)
            {
                case "vni": return InputMethod.Vni;
                case "viqr": return InputMethod.Viqr;
                case "viqr*": return InputMethod.ViqrStar;
                default: return InputMethod.Telex;
            }
        }

        private static string Type(string method, Dictionary<string, object> opts,
                                   Dictionary<string, object> macros, string input)
        {
            var engine = new VnEngine(MethodOf(method));
            if (opts != null)
            {
                foreach (var o in opts)
                {
                    bool v = (bool)o.Value;
                    switch (o.Key)
                    {
                        case "freeMarking": engine.FreeMarking = v; break;
                        case "toneNextToVowel": engine.FreeMarking = !v; break;
                        case "modernStyle": engine.ModernStyle = v; break;
                        case "autoRestore": engine.AutoRestore = v; break;
                        case "hornUO": engine.HornUO = v; break;
                        default: throw new Exception("Tuỳ chọn lạ: " + o.Key);
                    }
                }
            }
            if (macros != null)
            {
                var m = new Dictionary<string, string>();
                foreach (var p in macros) m[p.Key] = (string)p.Value;
                engine.SetMacros(m);
                engine.MacroEnabled = true;
            }

            var text = new StringBuilder();
            int i = 0;
            while (i < input.Length)
            {
                if (string.CompareOrdinal(input, i, "{BS}", 0, 4) == 0)
                {
                    if (text.Length > 0) text.Length--;
                    i += 4;
                    continue;
                }
                if (string.CompareOrdinal(input, i, "{PASTE:", 0, 7) == 0)
                {
                    int end = input.IndexOf('}', i);
                    text.Append(input, i + 7, end - i - 7);
                    i = end + 1;
                    continue;
                }
                string s = text.ToString();
                engine.EnsureSync(s);
                if (string.CompareOrdinal(input, i, "{ENTER}", 0, 7) == 0)
                {
                    Apply(text, engine.EndWord());
                    text.Append('\n');
                    i += 7;
                    continue;
                }
                Apply(text, engine.Process(input[i]));
                i++;
            }
            return text.ToString();
        }

        private static void Apply(StringBuilder text, Edit e)
        {
            text.Length -= e.Backs;
            text.Append(e.Text);
        }

        /// <summary>Bộ đọc JSON tối giản (đủ cho cases.json), để không phụ thuộc thư viện ngoài</summary>
        private sealed class Json
        {
            private readonly string s;
            private int p;

            public Json(string s)
            {
                this.s = s;
            }

            public object Parse()
            {
                object v = Value();
                Ws();
                if (p != s.Length) throw Err("dư ký tự");
                return v;
            }

            private Exception Err(string msg)
            {
                return new FormatException("JSON lỗi tại vị trí " + p + ": " + msg);
            }

            private void Ws()
            {
                while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
            }

            private object Value()
            {
                Ws();
                if (p >= s.Length) throw Err("hết dữ liệu");
                char c = s[p];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (string.CompareOrdinal(s, p, "true", 0, 4) == 0) { p += 4; return true; }
                if (string.CompareOrdinal(s, p, "false", 0, 5) == 0) { p += 5; return false; }
                if (string.CompareOrdinal(s, p, "null", 0, 4) == 0) { p += 4; return null; }
                int start = p;
                while (p < s.Length && "+-0123456789.eE".IndexOf(s[p]) >= 0) p++;
                if (start == p) throw Err("ký tự lạ '" + c + "'");
                return double.Parse(s.Substring(start, p - start), CultureInfo.InvariantCulture);
            }

            private Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                p++;
                Ws();
                if (s[p] == '}') { p++; return d; }
                while (true)
                {
                    Ws();
                    string k = Str();
                    Ws();
                    if (s[p++] != ':') throw Err("thiếu ':'");
                    d[k] = Value();
                    Ws();
                    char c = s[p++];
                    if (c == '}') return d;
                    if (c != ',') throw Err("thiếu ',' hoặc '}'");
                }
            }

            private List<object> Arr()
            {
                var a = new List<object>();
                p++;
                Ws();
                if (s[p] == ']') { p++; return a; }
                while (true)
                {
                    a.Add(Value());
                    Ws();
                    char c = s[p++];
                    if (c == ']') return a;
                    if (c != ',') throw Err("thiếu ',' hoặc ']'");
                }
            }

            private string Str()
            {
                if (s[p] != '"') throw Err("thiếu '\"'");
                p++;
                var sb = new StringBuilder();
                while (true)
                {
                    char c = s[p++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    char e = s[p++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(p, 4), NumberStyles.HexNumber));
                            p += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
            }
        }
    }
}
