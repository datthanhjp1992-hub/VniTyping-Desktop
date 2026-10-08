// VniTyping — bảng dữ liệu tiếng Việt cho engine.
// Port từ ukengine.js v2 (repo VniTyping). Kế thừa bảng nguyên âm của UniKey 3.62
// (Copyright (C) 1998-2002 Pham Kim Long). GPL v2 hoặc mới hơn.
using System.Collections.Generic;

namespace VniTyping.Engine
{
    internal static class VnTables
    {
        // Dấu phụ gắn vào chữ gốc
        public const int None = 0, Hat = 1, Breve = 2, Horn = 3, Stroke = 4;

        // Dấu thanh: 0 = không dấu, 1 sắc, 2 huyền, 3 hỏi, 4 ngã, 5 nặng
        public const int Sac = 1, Huyen = 2, Hoi = 3, Nga = 4, Nang = 5;

        // 12 nguyên âm × 6 cột: sắc, huyền, hỏi, ngã, nặng, không dấu
        public static readonly string[] BD =
        {
            "áàảãạa", "ấầẩẫậâ", "ắằẳẵặă",
            "éèẻẽẹe", "ếềểễệê",
            "íìỉĩịi",
            "óòỏõọo", "ốồổỗộô", "ớờởỡợơ",
            "úùủũụu", "ứừửữựư",
            "ýỳỷỹỵy"
        };

        private const string RowBase = "aaaeeiooouuy";
        private static readonly int[] RowMark = { None, Hat, Breve, None, Hat, None, None, Hat, Horn, None, Horn, None };

        /// <summary>Một ký tự đã phân rã: chữ gốc, dấu phụ, dấu thanh</summary>
        public struct Dec
        {
            public char B;
            public int M;
            public int T;

            public Dec(char b, int m, int t)
            {
                B = b;
                M = m;
                T = t;
            }
        }

        /// <summary>Phân rã một ký tự chữ thường</summary>
        public static readonly Dictionary<char, Dec> Decomp = new Dictionary<char, Dec>();

        public static readonly HashSet<string> Onsets = new HashSet<string>();
        public static readonly HashSet<string> OnsetPrefixes = new HashSet<string>();

        /// <summary>Toàn bộ vần hợp lệ (nguyên âm + phụ âm cuối), không kể dấu thanh</summary>
        public static readonly List<Dec[]> Rimes = new List<Dec[]>();

        private const string OnsetList = "b c ch d đ g gh gi h k kh l m n ng ngh nh p ph qu r s t th tr v x";

        private const string RimeList =
            "a ai ao au ay ac ach am an ang anh ap at " +
            "ăc ăm ăn ăng ăp ăt " +
            "âc âm ân âng âp ât âu ây " +
            "e eo ec em en eng ep et " +
            "ê êu êch êm ên ênh êp êt " +
            "i ia iu ich im in inh ip it " +
            "iêc iêm iên iêng iêp iêt iêu " +
            "o oi oc om on ong op ot oong ooc " +
            "oa oai oao oay oac oach oam oan oang oanh oap oat " +
            "oăc oăm oăn oăng oăt " +
            "oe oeo oen oet " +
            "ô ôi ôc ôm ôn ông ôp ôt " +
            "ơ ơi ơm ơn ơp ơt " +
            "u ua ui uc um un ung up ut " +
            "uân uâng uât uây " +
            "uê uêch uênh " +
            "uy uya uyu uych uynh uyt uyên uyêt " +
            "uơ " +
            "uôc uôi uôm uôn uông uôt " +
            "ư ưa ưi ưu ưc ưm ưng ưt " +
            "ươc ươi ươm ươn ương ươp ươt ươu " +
            "y yêm yên yêt yêu ych ynh yt";

        static VnTables()
        {
            for (int r = 0; r < 12; r++)
                for (int c = 0; c < 6; c++)
                    Decomp[BD[r][c]] = new Dec(RowBase[r], RowMark[r], c == 5 ? 0 : c + 1);
            Decomp['đ'] = new Dec('d', Stroke, 0);
            for (char ch = 'a'; ch <= 'z'; ch++)
                if (!Decomp.ContainsKey(ch)) Decomp[ch] = new Dec(ch, None, 0);

            Onsets.Add("");
            OnsetPrefixes.Add("");
            foreach (string o in OnsetList.Split(' '))
            {
                Onsets.Add(o);
                for (int i = 1; i <= o.Length; i++) OnsetPrefixes.Add(o.Substring(0, i));
            }

            foreach (string r in RimeList.Split(' '))
            {
                var dec = new Dec[r.Length];
                for (int i = 0; i < r.Length; i++) dec[i] = Decomp[r[i]];
                Rimes.Add(dec);
            }
        }

        public static bool IsVowel(char b)
        {
            return "aeiouy".IndexOf(b) >= 0;
        }

        public static int RowOf(char b, int m)
        {
            for (int r = 0; r < 12; r++)
                if (RowBase[r] == b && RowMark[r] == m) return r;
            return -1;
        }

        public static bool IsWordChar(char c)
        {
            return Decomp.ContainsKey(char.ToLowerInvariant(c));
        }
    }
}
