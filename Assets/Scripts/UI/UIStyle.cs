using UnityEngine;
using UnityEngine.UI;

namespace WaveTeam.UI
{
    /// <summary>
    /// 程序化 UI 风格层，主题：**暖色木工台 × 像素硬边**。
    ///
    /// 色板刻意选成和排练室/道具/立绘同一套暖木色（深木底、奶油米文字、暖金主按钮、
    /// 薄荷绿次强调），这样从房间切进音轨板不会像换了个游戏。
    ///
    /// 圆角也换掉了：原来是 32×32、Bilinear 的抗锯齿圆角，放大后边缘是柔的，
    /// 和像素画正相反。现在是运行时生成的 16×16 硬边像素九宫格（点采样、四角 2px
    /// 阶梯倒角、1px 描边 + 1px 内侧高光），凸面与凹槽各一个。
    ///
    /// 精灵本身只分「描边 / 高光 / 填充」三档亮度，靠 Image.color 相乘着色，
    /// 所以一个精灵能染出任何颜色，而且描边永远自动比填充深。
    /// </summary>
    public static class UIStyle
    {
        // —— 配色 ——
        public static readonly Color BgDeep   = Rgb(0x24, 0x1A, 0x15); // 画布底色（最深的木）
        public static readonly Color Stage    = Rgb(0x3A, 0x2A, 0x1F); // 鼓轨底板（木台面）
        public static readonly Color Panel    = Rgb(0x46, 0x33, 0x2A); // 侧栏面板
        public static readonly Color Card     = Rgb(0x5C, 0x42, 0x30); // 角色卡
        public static readonly Color Cell     = Rgb(0x2A, 0x1E, 0x16); // 鼓轨格底（凹槽）
        public static readonly Color CellAlt  = Rgb(0x2E, 0x22, 0x18); // 鼓轨格底（交替格）
        public static readonly Color Line     = new Color(0.94f, 0.90f, 0.85f, 0.12f); // 分隔线
        public static readonly Color Accent   = Rgb(0xE0, 0xA9, 0x4D); // 暖金（主按钮）
        public static readonly Color Accent2  = Rgb(0x4E, 0x7A, 0x63); // 薄荷绿（次强调/拉手）
        public static readonly Color Text     = Rgb(0xF0, 0xE6, 0xD8); // 主文字（奶油米）
        public static readonly Color TextDim  = Rgb(0xB9, 0xA7, 0x8F); // 次文字
        public static readonly Color Ok       = Rgb(0x6F, 0xBF, 0x73); // 绿（确定/契合）
        public static readonly Color Danger   = Rgb(0xC2, 0x5A, 0x4A); // 红（取消/警示）
        public static readonly Color Edge     = Rgb(0x4A, 0x3A, 0x30); // 描边
        public static readonly Color EdgeDeep = Rgb(0x2A, 0x1E, 0x16); // 更深的描边（压在亮底上）
        public static readonly Color Ink      = Rgb(0x24, 0x1A, 0x15); // 亮底按钮上的深色文字
        public static readonly Color Grey     = Rgb(0x5A, 0x4A, 0x3E); // 中性按钮（取消/退出/回起点）

        private static Color Rgb(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        // —— 像素九宫格 ——

        private const int FrameSize = 16;    // 精灵边长（像素）
        private const int FrameBorder = 6;   // 9-slice 边距（UI 单位：PPU 100 且画布 referencePixelsPerUnit 100）

        private static Sprite _raised;
        private static Sprite _well;

        /// <summary>凸面：高光在左上，用在面板、按钮、卡片上。</summary>
        public static Sprite FrameRaised
        {
            get { if (_raised == null) _raised = BuildFrame(false); return _raised; }
        }

        /// <summary>凹槽：阴影在左上、高光在右下，用在鼓轨格槽上。</summary>
        public static Sprite FrameWell
        {
            get { if (_well == null) _well = BuildFrame(true); return _well; }
        }

        /// <summary>兼容旧名字：以前是抗锯齿圆角，现在等同于凸面像素边框。</summary>
        public static Sprite Round { get { return FrameRaised; } }

        /// <summary>
        /// 生成 16×16 硬边像素边框。
        /// 由外到内三档：0=深棕描边(74)、1=高光/内阴影(255 或 168)、≥2=填充(224)。
        /// 四角切 2px 阶梯倒角，所以是「像素画的圆角」，不是抗锯齿圆角。
        /// </summary>
        private static Sprite BuildFrame(bool well)
        {
            const int n = FrameSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = well ? "UI_PixelWell_Gen" : "UI_PixelFrame_Gen";

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int l = x, r = n - 1 - x, b = y, t = n - 1 - y;
                    int ring = Mathf.Min(Mathf.Min(l, r), Mathf.Min(b, t));

                    // 四角 2px 阶梯倒角
                    bool chamfer = (l + b < 2) || (r + b < 2) || (l + t < 2) || (r + t < 2);
                    if (chamfer)
                    {
                        px[y * n + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    byte v;
                    if (ring == 0)
                    {
                        v = 74;                                     // 描边
                    }
                    else if (ring == 1)
                    {
                        bool lightSide = well ? (b == ring || r == ring) : (t == ring || l == ring);
                        v = lightSide ? (byte)255 : (byte)168;      // 高光 / 内阴影
                    }
                    else
                    {
                        v = 224;                                    // 填充
                    }
                    px[y * n + x] = new Color32(v, v, v, 255);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect,
                new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
        }

        /// <summary>给 Image 套凸面像素边框并着色（9-slice 保证倒角不被拉伸变形）。</summary>
        public static void ApplyFrame(Image img, Color color)
        {
            if (img == null) return;
            img.sprite = FrameRaised;
            img.type = Image.Type.Sliced;
            img.color = color;
        }

        /// <summary>给 Image 套凹槽像素边框（格槽用）。</summary>
        public static void ApplyWell(Image img, Color color)
        {
            if (img == null) return;
            img.sprite = FrameWell;
            img.type = Image.Type.Sliced;
            img.color = color;
        }

        /// <summary>兼容旧调用点：转发到 ApplyFrame。新代码请直接用 ApplyFrame / ApplyWell。</summary>
        public static void ApplyRound(Image img, Color color)
        {
            ApplyFrame(img, color);
        }

        /// <summary>
        /// 给系统字加深色描边。像素字（PixelText）自带实心字形，不需要这层，所以只对 Text 生效。
        /// </summary>
        public static Outline OutlineText(Text t, Color? color = null)
        {
            if (t == null) return null;
            var o = t.GetComponent<Outline>();
            if (o == null) o = t.gameObject.AddComponent<Outline>();
            o.effectColor = color ?? new Color(0f, 0f, 0f, 0.55f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            return o;
        }
    }
}
