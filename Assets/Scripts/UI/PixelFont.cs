using System.Collections.Generic;
using UnityEngine;

namespace WaveTeam.UI
{
    /// <summary>
    /// 像素字体：一张 1-bit 位图图集 + 每个字的度量。
    /// 图集和字表由 Tools/make_pixel_font.py 生成到 Assets/Resources/PixelFont/，
    /// 这里只负责读一次、缓存起来。
    ///
    /// 与 Unity 内置 Arial 的区别：这是真正的点阵字，采样用 Point、阀值化过，
    /// 放大多少倍都是硬边方块，不会糊。
    /// </summary>
    public sealed class PixelFont
    {
        public const string ResourcePath = "PixelFont/pixel_font";

        /// <summary>JsonUtility 只认字段、不认字典，所以字表在 json 里存成 4 个等长并行数组。</summary>
        [System.Serializable]
        private sealed class FontJson
        {
            public int size;
            public int cellW;
            public int cellH;
            public int pitchX;
            public int pitchY;
            public int cols;
            public int rows;
            public int atlasW;
            public int atlasH;
            public string chars;
            public int[] col;
            public int[] row;
            public int[] adv;
        }

        public struct Glyph
        {
            public int Col;
            public int Row;
            public int Advance;
        }

        public readonly Texture2D Atlas;
        public readonly int CellW;
        public readonly int CellH;
        public readonly int AtlasW;
        public readonly int AtlasH;
        public readonly int EmSize;

        private readonly int _pitchX;
        private readonly int _pitchY;
        private readonly Dictionary<char, Glyph> _glyphs;
        private readonly HashSet<char> _warned = new HashSet<char>();

        /// <summary>字体一行的高度（像素），排版时按它走行距。</summary>
        public int LineHeight { get { return CellH; } }

        /// <summary>缺字时用的步进宽：半个字，比整字窄，一眼能看出是漏字而不是空格。</summary>
        public int MissingAdvance { get { return Mathf.Max(2, CellW / 2); } }

        private static PixelFont _instance;
        private static bool _tried;

        public static PixelFont Instance
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    _instance = Load();
                }
                return _instance;
            }
        }

        private PixelFont(FontJson json, Texture2D atlas)
        {
            Atlas = atlas;
            CellW = json.cellW;
            CellH = json.cellH;
            AtlasW = json.atlasW;
            AtlasH = json.atlasH;
            EmSize = json.size;
            _pitchX = json.pitchX;
            _pitchY = json.pitchY;

            int n = json.chars != null ? json.chars.Length : 0;
            _glyphs = new Dictionary<char, Glyph>(n);
            for (int i = 0; i < n; i++)
            {
                // 三个数组是等长的；截断的 json 会在读之前被挡掉，这里再防一手越界
                if (json.col == null || json.row == null || json.adv == null) break;
                if (i >= json.col.Length || i >= json.row.Length || i >= json.adv.Length) break;
                _glyphs[json.chars[i]] = new Glyph
                {
                    Col = json.col[i],
                    Row = json.row[i],
                    Advance = json.adv[i],
                };
            }
        }

        private static PixelFont Load()
        {
            var jsonAsset = Resources.Load<TextAsset>(ResourcePath);
            var atlas = Resources.Load<Texture2D>(ResourcePath);
            if (jsonAsset == null || atlas == null)
            {
                Debug.LogWarning("[像素字体] 读不到 Resources/" + ResourcePath +
                                 "（.json 或 .png 缺一）。请先跑 python Tools/make_pixel_font.py。");
                return null;
            }

            var json = JsonUtility.FromJson<FontJson>(jsonAsset.text);
            if (json == null || string.IsNullOrEmpty(json.chars))
            {
                Debug.LogWarning("[像素字体] " + ResourcePath + ".json 解析失败或字表为空。");
                return null;
            }
            // 成功也要说一声：以前只有失败才出声，于是「没警告」既可能是成功、
            // 也可能是这段压根没跑到，两种情况的日志长得一模一样。
            Debug.Log("[像素字体] 已载入 " + json.chars.Length + " 字，图集 " +
                      atlas.width + "x" + atlas.height + "，格 " + json.cellW + "x" + json.cellH +
                      "，可读=" + atlas.isReadable);
            return new PixelFont(json, atlas);
        }

        public bool TryGet(char c, out Glyph glyph)
        {
            return _glyphs.TryGetValue(c, out glyph);
        }

        /// <summary>取一个字的步进宽（画完这个字光标前进多少像素）。</summary>
        public int AdvanceOf(char c)
        {
            Glyph g;
            if (_glyphs.TryGetValue(c, out g)) return g.Advance;

            // 漏字只提醒一次，别每帧刷屏
            if (c != ' ' && c != '\n' && !_warned.Contains(c))
            {
                _warned.Add(c);
                Debug.LogWarning("[像素字体] 字表里没有 '" + c + "'（U+" +
                                 ((int)c).ToString("X4") + "），该字会留空。" +
                                 "把字加进 Tools/make_pixel_font.py 的 EXTRA 再重跑即可。");
            }
            return MissingAdvance;
        }

        /// <summary>整行的像素宽（不含换行后的第二行）。</summary>
        public int MeasureWidth(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int w = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\n') break;
                w += AdvanceOf(s[i]);
            }
            return w;
        }

        public int LineCount(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            int n = 1;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }

        /// <summary>
        /// 取一个字的图集 UV 范围。
        /// Unity 的 UV 原点在左下，PNG 像素原点在左上，所以 v 要翻过来算。
        /// </summary>
        public bool GetUv(char c, out Vector2 uvMin, out Vector2 uvMax)
        {
            uvMin = uvMax = Vector2.zero;
            Glyph g;
            if (!_glyphs.TryGetValue(c, out g)) return false;

            float x0 = g.Col * _pitchX;
            float y0 = g.Row * _pitchY;
            uvMin = new Vector2(x0 / AtlasW, 1f - (y0 + CellH) / AtlasH);
            uvMax = new Vector2((x0 + CellW) / AtlasW, 1f - y0 / AtlasH);
            return true;
        }
    }
}
