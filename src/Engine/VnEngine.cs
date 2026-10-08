// VniTyping — engine gõ tiếng Việt lấy âm tiết làm trung tâm.
// Port 1:1 từ ukengine.js v2 (repo VniTyping); cả hai chạy chung tests/cases.json.
// Kế thừa ý tưởng và bảng dữ liệu từ UniKey 3.62 (Copyright (C) 1998-2002 Pham Kim Long).
// Copyright (C) 2026 Nguyễn Thành Đạt. GPL v2 hoặc mới hơn.
using System.Collections.Generic;
using System.Text;

namespace VniTyping.Engine
{
    public enum InputMethod
    {
        Telex = 0,
        Vni = 1,
        Viqr = 2,
        ViqrStar = 3
    }

    /// <summary>Chỉnh sửa cần áp dụng: xoá lùi Backs ký tự trước con trỏ rồi chèn Text</summary>
    public struct Edit
    {
        public readonly int Backs;
        public readonly string Text;

        public Edit(int backs, string text)
        {
            Backs = backs;
            Text = text;
        }

        public bool IsEmpty
        {
            get { return Backs == 0 && Text.Length == 0; }
        }
    }

    public sealed class VnEngine
    {
        // ---- tuỳ chọn (mặc định theo thiết kế đã chốt) ----
        /// <summary>Bỏ dấu tự do; false = dấu phải gõ ngay sau nguyên âm</summary>
        public bool FreeMarking = true;
        /// <summary>Kiểu mới: hoà, khoẻ, thuỷ</summary>
        public bool ModernStyle = true;
        /// <summary>Giữ nguyên từ không phải tiếng Việt (google, address...)</summary>
        public bool AutoRestore = true;
        /// <summary>uo + w → ươ</summary>
        public bool HornUO = true;
        public bool MacroEnabled;

        private bool enabled = true;
        private InputMethod method;
        private Word w = new Word();
        private readonly Dictionary<string, string> macros = new Dictionary<string, string>();

        public VnEngine(InputMethod method)
        {
            this.method = method;
        }

        public InputMethod Method
        {
            get { return method; }
            set { method = value; Reset(); }
        }

        /// <summary>false = tắt bộ gõ, phím đi thẳng vào ô nhập</summary>
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; Reset(); }
        }

        public void SetMacros(IDictionary<string, string> map)
        {
            macros.Clear();
            foreach (var p in map) macros[p.Key.ToLowerInvariant()] = p.Value;
        }

        /// <summary>Xoá trạng thái từ đang gõ</summary>
        public void Reset()
        {
            w = new Word();
        }

        // ===================================================================
        // Trạng thái từ đang gõ
        // ===================================================================
        private sealed class Letter
        {
            public char B;     // chữ gốc a–z (hoặc ký tự khác)
            public int M;      // dấu phụ
            public bool Up;    // chữ hoa
            public bool Sk;    // sinh ra từ phím tắt w [ ] { }
            public bool Auto;  // ơ được tự thêm sau ư

            public Letter(char b, int m, bool up, bool sk)
            {
                B = b;
                M = m;
                Up = up;
                Sk = sk;
            }
        }

        private sealed class Word
        {
            public List<Letter> Ls = new List<Letter>();
            public int Tone;
            public string Raw = "";     // văn bản gốc + phím đã gõ, dùng khi trả lại nguyên văn
            public string Out = "";     // chuỗi đang hiển thị
            public bool Locked;         // không biến đổi gì thêm cho tới hết từ
            public bool RawMode;        // đang hiển thị nguyên văn
            public bool Tf;             // đã có biến đổi dấu
            public bool Esc;            // VIQR: phím kế tiếp là ký tự thường
        }

        private sealed class Parse
        {
            public int Vs, Ve;   // phụ âm đầu = [0, Vs), nguyên âm = [Vs, Ve), phụ âm cuối = [Ve, hết)

            public Parse(int vs, int ve)
            {
                Vs = vs;
                Ve = ve;
            }
        }

        private enum ActType
        {
            Tone,
            Hat,
            W,
            Horn,
            Breve,
            Stroke,
            Short,
            Escape
        }

        private sealed class Act
        {
            public ActType Type;
            public int T;
            public string Targets;
            public bool Adjacent;
            public char B;
            public bool Up;

            public Act(ActType type)
            {
                Type = type;
            }
        }

        // ===================================================================
        // Hiển thị và luật chính tả
        // ===================================================================
        private static char LowerCharOf(char b, int m)
        {
            if (VnTables.IsVowel(b)) return VnTables.BD[VnTables.RowOf(b, m)][5];
            return m == VnTables.Stroke ? 'đ' : b;
        }

        private static char CharOf(Letter l, int tone)
        {
            char ch;
            if (VnTables.IsVowel(l.B)) ch = VnTables.BD[VnTables.RowOf(l.B, l.M)][tone > 0 ? tone - 1 : 5];
            else ch = l.M == VnTables.Stroke ? 'đ' : l.B;
            return l.Up ? char.ToUpperInvariant(ch) : ch;
        }

        private static string KeyOf(List<Letter> ls, int from, int to)
        {
            var sb = new StringBuilder(to - from);
            for (int i = from; i < to; i++) sb.Append(LowerCharOf(ls[i].B, ls[i].M));
            return sb.ToString();
        }

        private static Parse Mk(List<Letter> ls, int vs)
        {
            int ve = vs;
            while (ve < ls.Count && VnTables.IsVowel(ls[ve].B)) ve++;
            return new Parse(vs, ve);
        }

        /// <summary>Các cách tách phụ âm đầu + vần; "qu" và "gi" có thể nuốt chữ u / i</summary>
        private static List<Parse> Parses(List<Letter> ls)
        {
            int n = ls.Count, i = 0;
            while (i < n && !VnTables.IsVowel(ls[i].B)) i++;
            string on = KeyOf(ls, 0, i);
            var res = new List<Parse>();
            if (on == "q" && i < n && ls[i].B == 'u' && ls[i].M == VnTables.None)
            {
                res.Add(Mk(ls, i + 1));
            }
            else if (on == "g" && i + 1 < n && ls[i].B == 'i' && ls[i].M == VnTables.None && VnTables.IsVowel(ls[i + 1].B))
            {
                res.Add(Mk(ls, i + 1));
                res.Add(Mk(ls, i));
            }
            else
            {
                res.Add(Mk(ls, i));
            }
            return res;
        }

        private static bool RimeMatch(List<Letter> ls, int start, bool complete)
        {
            int len = ls.Count - start;
            foreach (var r in VnTables.Rimes)
            {
                if (complete ? r.Length != len : r.Length < len) continue;
                bool ok = true;
                for (int j = 0; j < len && ok; j++)
                {
                    var l = ls[start + j];
                    if (r[j].B != l.B) ok = false;
                    else if (r[j].M != l.M && (complete || l.M != VnTables.None)) ok = false;
                }
                if (ok) return true;
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra âm tiết hợp lệ. complete = false chấp nhận cả "đang gõ dở".
        /// Trả về cách tách hợp lệ, hoặc null.
        /// </summary>
        private static Parse ValidParse(List<Letter> ls, int tone, bool complete)
        {
            int n = ls.Count;
            foreach (var p in Parses(ls))
            {
                string on = KeyOf(ls, 0, p.Vs);
                if (p.Vs == n)
                {
                    if (!complete && VnTables.OnsetPrefixes.Contains(on)) return p;
                    continue;
                }
                if (!VnTables.Onsets.Contains(on)) continue;
                char fb = ls[p.Vs].B;
                if (on == "c" && "eiy".IndexOf(fb) >= 0) continue;
                if (on == "k" && "eiy".IndexOf(fb) < 0) continue;
                if ((on == "gh" || on == "ngh") && "ei".IndexOf(fb) < 0) continue;
                if (!RimeMatch(ls, p.Vs, complete)) continue;
                if (tone == VnTables.Huyen || tone == VnTables.Hoi || tone == VnTables.Nga)
                {
                    string fin = KeyOf(ls, p.Ve, n);
                    if (fin == "c" || fin == "ch" || fin == "p" || fin == "t") continue;
                }
                return p;
            }
            return null;
        }

        /// <summary>Vị trí đặt dấu thanh trong từ, -1 nếu không có nguyên âm</summary>
        private static int TonePosition(List<Letter> ls, bool modernStyle)
        {
            var ps = Parses(ls);
            var p = ValidParse(ls, 0, false) ?? ps[ps.Count - 1];
            var idx = new List<int>();
            int lastMarked = -1;
            for (int i = p.Vs; i < p.Ve; i++)
            {
                idx.Add(i);
                if (ls[i].M != VnTables.None) lastMarked = i;
            }
            if (idx.Count == 0) return -1;
            if (lastMarked >= 0) return lastMarked;            // ê, ơ, ư... (ươ → ơ)
            if (idx.Count == 1) return idx[0];
            if (idx.Count >= 3) return idx[1];                 // oai, uya → chữ giữa
            if (p.Ve < ls.Count) return idx[1];                // có phụ âm cuối: hoàn
            string pair = "" + ls[idx[0]].B + ls[idx[1]].B;
            if (modernStyle && (pair == "oa" || pair == "oe" || pair == "uy")) return idx[1];
            return idx[0];                                     // âm mở: mùa, hòa
        }

        // ===================================================================
        // Kiểu gõ → hành động
        // ===================================================================
        private static Act ToneAct(int t)
        {
            var a = new Act(ActType.Tone);
            a.T = t;
            return a;
        }

        private static Act HatAct(string targets)
        {
            var a = new Act(ActType.Hat);
            a.Targets = targets;
            return a;
        }

        private static Act StrokeAct(bool adjacent)
        {
            var a = new Act(ActType.Stroke);
            a.Adjacent = adjacent;
            return a;
        }

        private static Act ShortAct(char b, bool up)
        {
            var a = new Act(ActType.Short);
            a.B = b;
            a.Up = up;
            return a;
        }

        private static Act ActionOf(InputMethod method, char key)
        {
            char k = char.ToLowerInvariant(key);
            int t;
            if (method == InputMethod.Telex)
            {
                t = "zsfrxj".IndexOf(k);
                if (t >= 0) return ToneAct(t);
                if (k == 'a' || k == 'e' || k == 'o') return HatAct(k.ToString());
                if (k == 'w') return new Act(ActType.W);
                if (k == 'd') return StrokeAct(true);
                if (key == '[') return ShortAct('o', false);
                if (key == ']') return ShortAct('u', false);
                if (key == '{') return ShortAct('o', true);
                if (key == '}') return ShortAct('u', true);
                return null;
            }
            if (method == InputMethod.Vni)
            {
                t = "012345".IndexOf(k);
                if (t >= 0) return ToneAct(t);
                if (k == '6') return HatAct("aeo");
                if (k == '7') return new Act(ActType.Horn);
                if (k == '8') return new Act(ActType.Breve);
                if (k == '9') return StrokeAct(false);
                return null;
            }
            // VIQR / VIQR*
            t = "'`?~.".IndexOf(k);
            if (t >= 0) return ToneAct(t + 1);
            if (k == '0' || k == '=') return ToneAct(0);
            if (k == '^') return HatAct("aeo");
            if (k == (method == InputMethod.ViqrStar ? '*' : '+')) return new Act(ActType.Horn);
            if (k == '(') return new Act(ActType.Breve);
            if (k == 'd') return StrokeAct(true);
            if (k == '\\') return new Act(ActType.Escape);
            return null;
        }

        // ===================================================================
        // Đồng bộ với ô nhập
        // ===================================================================
        private static bool WordFromText(string text, List<Letter> ls, out int tone)
        {
            tone = 0;
            foreach (char c in text)
            {
                VnTables.Dec d;
                if (!VnTables.Decomp.TryGetValue(char.ToLowerInvariant(c), out d)) return false;
                if (d.T != 0)
                {
                    if (tone != 0) return false;
                    tone = d.T;
                }
                ls.Add(new Letter(d.B, d.M, c != char.ToLowerInvariant(c), false));
            }
            return true;
        }

        private static int TrailingWordStart(string text, int end)
        {
            int i = end;
            while (i > 0 && VnTables.IsWordChar(text[i - 1])) i--;
            return i;
        }

        /// <summary>Kiểm tra một từ có phải âm tiết tiếng Việt hoàn chỉnh không</summary>
        public static bool IsValidSyllable(string word)
        {
            var ls = new List<Letter>();
            int tone;
            return WordFromText(word, ls, out tone) && ValidParse(ls, tone, true) != null;
        }

        /// <summary>Chuỗi engine đang hiển thị cho từ đang gõ</summary>
        public string CurrentWord
        {
            get { return w.Out; }
        }

        /// <summary>Nạp lại trạng thái từ văn bản đứng trước con trỏ (text[0..end))</summary>
        public void SyncFrom(string text, int end)
        {
            Reset();
            int start = TrailingWordStart(text, end);
            if (start == end) return;
            string word = text.Substring(start, end - start);
            int tone;
            bool parsed = WordFromText(word, w.Ls, out tone);
            w.Raw = w.Out = word;
            if (!parsed) w.Ls.Clear();
            w.Tone = parsed ? tone : 0;
            if (parsed)
            {
                w.Tf = w.Tone > 0;
                foreach (var l in w.Ls) if (l.M != VnTables.None) w.Tf = true;
            }
            if (!parsed || !Ok(w.Ls, w.Tone))
            {
                w.Locked = true;
                w.RawMode = true;
            }
        }

        public void SyncFrom(string textBeforeCaret)
        {
            SyncFrom(textBeforeCaret, textBeforeCaret.Length);
        }

        /// <summary>
        /// Chỉ nạp lại khi văn bản trước con trỏ không khớp từ đang gõ (Undo, Paste, click...).
        /// Nên gọi trước mỗi phím.
        /// </summary>
        public void EnsureSync(string text, int end)
        {
            string o = w.Out;
            int n = end - o.Length;
            bool same = n >= 0 && string.CompareOrdinal(text, n, o, 0, o.Length) == 0 &&
                        !(n > 0 && VnTables.IsWordChar(text[n - 1]));
            if (!same) SyncFrom(text, end);
        }

        public void EnsureSync(string textBeforeCaret)
        {
            EnsureSync(textBeforeCaret, textBeforeCaret.Length);
        }

        /// <summary>Backspace khi dùng engine độc lập</summary>
        public void Backspace()
        {
            string o = w.Out;
            SyncFrom(o.Length > 0 ? o.Substring(0, o.Length - 1) : "");
        }

        // ===================================================================
        // Xử lý phím
        // ===================================================================
        private bool Ok(List<Letter> ls, int tone)
        {
            return !AutoRestore || ValidParse(ls, tone, false) != null;
        }

        private void Render()
        {
            if (w.RawMode)
            {
                w.Out = w.Raw;
                return;
            }
            int tp = w.Tone != 0 ? TonePosition(w.Ls, ModernStyle) : -1;
            var sb = new StringBuilder(w.Ls.Count);
            for (int i = 0; i < w.Ls.Count; i++) sb.Append(CharOf(w.Ls[i], i == tp ? w.Tone : 0));
            w.Out = sb.ToString();
        }

        private static Letter LetterOf(char key)
        {
            char lower = char.ToLowerInvariant(key);
            return new Letter(lower, VnTables.None, key != lower, false);
        }

        private static Edit Diff(string a, string b)
        {
            int p = 0;
            while (p < a.Length && p < b.Length && a[p] == b[p]) p++;
            return new Edit(a.Length - p, b.Substring(p));
        }

        private bool IsWordKey(char key)
        {
            if ((key >= 'a' && key <= 'z') || (key >= 'A' && key <= 'Z')) return true;
            if (method == InputMethod.Telex && "[]{}".IndexOf(key) >= 0) return true;
            return w.Ls.Count > 0 && !w.Locked && ActionOf(method, key) != null;
        }

        /// <summary>Xử lý một phím, trả về chỉnh sửa cần áp dụng vào ô nhập</summary>
        public Edit Process(char key)
        {
            if (!enabled) return new Edit(0, key.ToString());
            string old = w.Out;
            if (IsWordKey(key))
            {
                WordKey(key);
                return Diff(old, w.Out);
            }
            var d = Diff(old, Finish());
            Reset();
            return new Edit(d.Backs, d.Text + key);
        }

        /// <summary>Kết thúc từ (Enter, Tab...): trả lại tiếng Anh, gõ tắt</summary>
        public Edit EndWord()
        {
            var d = Diff(w.Out, Finish());
            Reset();
            return d;
        }

        private void WordKey(char key)
        {
            w.Raw += key;
            if (w.Esc)
            {
                // VIQR: "\" + phím → phím thường
                w.Esc = false;
                w.Ls.RemoveAt(w.Ls.Count - 1);
                w.Raw = w.Raw.Substring(0, w.Raw.Length - 2) + key;
                Literal(key);
            }
            else
            {
                var act = w.Locked ? null : ActionOf(method, key);
                if (!(act != null && Apply(act, key))) Literal(key);
            }
            // "ưo" không phải vần tiếng Việt: gõ móc cho u trước rồi mới gõ o → tự thành "ươ"
            if (HornUO && !w.Locked)
            {
                for (int j = 0; j + 1 < w.Ls.Count; j++)
                {
                    if (w.Ls[j].B == 'u' && w.Ls[j].M == VnTables.Horn &&
                        w.Ls[j + 1].B == 'o' && w.Ls[j + 1].M == VnTables.None)
                    {
                        w.Ls[j + 1].M = VnTables.Horn;
                        w.Ls[j + 1].Auto = true;
                    }
                }
            }
            Render();
        }

        /// <summary>Thêm phím như chữ thường; nếu từ không còn là tiếng Việt thì trả lại nguyên văn</summary>
        private void Literal(char key)
        {
            w.Ls.Add(LetterOf(key));
            if (w.Locked) return;
            if (!Ok(w.Ls, w.Tone))
            {
                if (w.Tf) w.RawMode = true;
                w.Locked = true;
            }
        }

        /// <summary>Gõ lại cùng phím dấu: bỏ dấu, chèn phím đó, khoá từ</summary>
        private bool Undo(char key)
        {
            w.Ls.Add(LetterOf(key));
            w.Locked = true;
            return true;
        }

        private bool Apply(Act act, char key)
        {
            var ls = w.Ls;
            int n = ls.Count, i;
            bool free = FreeMarking;

            switch (act.Type)
            {
                case ActType.Tone:
                {
                    bool hasVowel = false;
                    foreach (var l in ls) if (VnTables.IsVowel(l.B)) hasVowel = true;
                    if (!hasVowel || (!free && !VnTables.IsVowel(ls[n - 1].B))) return false;
                    if (act.T == 0)
                    {
                        if (w.Tone == 0) return false;
                        w.Tone = 0;
                        w.Tf = true;
                        return true;
                    }
                    if (w.Tone == act.T)
                    {
                        w.Tone = 0;
                        return Undo(key);
                    }
                    if (!Ok(ls, act.T)) return false;
                    w.Tone = act.T;
                    w.Tf = true;
                    return true;
                }

                case ActType.Hat:
                {
                    for (i = n - 1; i >= 0; i--)
                    {
                        var l = ls[i];
                        if (!VnTables.IsVowel(l.B) || act.Targets.IndexOf(l.B) < 0) continue;
                        if (l.M == VnTables.Hat)
                        {
                            l.M = VnTables.None;
                            return Undo(key);
                        }
                        if (!free && i != n - 1) return false;
                        int oldM = l.M;
                        l.M = VnTables.Hat;
                        if (Ok(ls, w.Tone))
                        {
                            w.Tf = true;
                            return true;
                        }
                        l.M = oldM;
                        return false;
                    }
                    return false;
                }

                case ActType.W:
                case ActType.Horn:
                case ActType.Breve:
                {
                    var cands = new List<int[]>();
                    int j;
                    if (act.Type != ActType.Breve && HornUO)
                    {
                        for (j = 0; j + 1 < n; j++)
                            if (ls[j].B == 'u' && ls[j + 1].B == 'o' && !(j > 0 && ls[j - 1].B == 'q'))
                                cands.Add(new[] { j, j + 1 });
                    }
                    for (j = n - 1; j >= 0; j--)
                    {
                        char b = ls[j].B;
                        if ((act.Type != ActType.Breve && (b == 'u' || b == 'o')) ||
                            (act.Type != ActType.Horn && b == 'a'))
                            cands.Add(new[] { j });
                    }
                    foreach (var ids in cands)
                    {
                        bool allMarked = true, anyAuto = false;
                        foreach (int k in ids)
                        {
                            if (ls[k].M != TargetMark(ls[k])) allMarked = false;
                            if (ls[k].Auto) anyAuto = true;
                        }
                        if (allMarked)
                        {
                            if (anyAuto)
                            {
                                // ơ đã tự thêm: phím móc chỉ xác nhận
                                foreach (int k in ids) ls[k].Auto = false;
                                return true;
                            }
                            if (ids.Length == 1 && ls[ids[0]].Sk)
                            {
                                // "ww" → "w"
                                ls[ids[0]] = LetterOf(key);
                                w.Locked = true;
                                return true;
                            }
                            foreach (int k in ids) ls[k].M = VnTables.None;
                            return Undo(key);
                        }
                        if (!free && ids[ids.Length - 1] != n - 1) continue;
                        var saved = new int[ids.Length];
                        for (int x = 0; x < ids.Length; x++)
                        {
                            saved[x] = ls[ids[x]].M;
                            ls[ids[x]].M = TargetMark(ls[ids[x]]);
                        }
                        if (Ok(ls, w.Tone))
                        {
                            w.Tf = true;
                            return true;
                        }
                        for (int x = 0; x < ids.Length; x++) ls[ids[x]].M = saved[x];
                    }
                    if (act.Type == ActType.W)
                    {
                        // "w" đứng riêng → ư
                        ls.Add(new Letter('u', VnTables.Horn, key == 'W', true));
                        if (Ok(ls, w.Tone))
                        {
                            w.Tf = true;
                            return true;
                        }
                        ls.RemoveAt(ls.Count - 1);
                    }
                    return false;
                }

                case ActType.Stroke:
                {
                    if (n == 0 || ls[0].B != 'd' || (act.Adjacent && n != 1)) return false;
                    if (ls[0].M == VnTables.Stroke)
                    {
                        ls[0].M = VnTables.None;
                        return Undo(key);
                    }
                    ls[0].M = VnTables.Stroke;
                    if (Ok(ls, w.Tone))
                    {
                        w.Tf = true;
                        return true;
                    }
                    ls[0].M = VnTables.None;
                    return false;
                }

                case ActType.Short:
                {
                    // [ ] { } → ơ ư Ơ Ư
                    var last = n > 0 ? ls[n - 1] : null;
                    if (last != null && last.Sk && last.B == act.B)
                    {
                        ls[n - 1] = LetterOf(key);
                        w.Locked = true;
                        return true;
                    }
                    ls.Add(new Letter(act.B, VnTables.Horn, act.Up, true));
                    if (Ok(ls, w.Tone))
                    {
                        w.Tf = true;
                        return true;
                    }
                    ls.RemoveAt(ls.Count - 1);
                    return false;
                }

                case ActType.Escape:
                    ls.Add(LetterOf(key));
                    w.Esc = true;
                    return true;
            }
            return false;
        }

        private static int TargetMark(Letter l)
        {
            return l.B == 'a' ? VnTables.Breve : VnTables.Horn;
        }

        /// <summary>Chuỗi cuối cùng của từ: chuẩn hoá ươ → uơ, trả lại nguyên văn, gõ tắt</summary>
        private string Finish()
        {
            if (w.Ls.Count == 0 && !w.RawMode) return w.Out;
            string output = w.Out;
            if (!w.RawMode && w.Tf && !w.Locked && AutoRestore)
            {
                var ls = w.Ls;
                int n = ls.Count;
                // "thưở" → "thuở": ươ không có phụ âm cuối chỉ đúng khi là uơ
                if (n >= 2 && ls[n - 2].B == 'u' && ls[n - 2].M == VnTables.Horn &&
                    ls[n - 1].B == 'o' && ls[n - 1].M == VnTables.Horn &&
                    !(n >= 3 && VnTables.IsVowel(ls[n - 3].B)))
                {
                    ls[n - 2].M = VnTables.None;
                    Render();
                    output = w.Out;
                }
                if (ValidParse(ls, w.Tone, true) == null) output = w.Raw;
            }
            if (MacroEnabled && macros.Count > 0)
            {
                string m;
                if (macros.TryGetValue(output.ToLowerInvariant(), out m)) output = m;
            }
            return output;
        }
    }
}
