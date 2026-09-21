using UnityEngine;
using UnityEngine.UI;

namespace WaveTeam.UI
{
    /// <summary>
    /// 程序化 UI 风格层（无美术素材阶段的占位设计，主题：夜色舞台 × 霓虹波形）。
    /// 提供统一配色 + 内置圆角精灵 + 文字描边，让界面脱离「纯色方块」、便于游戏测试。
    /// 美术 PNG 就位后，可在 UIResource.ApplySpriteOrColor 里优先覆盖贴图（路径见 UI素材表.md）。
    /// </summary>
    public static class UIStyle
    {
        // —— 配色 ——
        public static readonly Color BgDeep   = new Color(0.10f, 0.12f, 0.20f, 1f); // 画布底色
        public static readonly Color Stage    = new Color(0.14f, 0.19f, 0.30f, 1f); // 鼓轨舞台
        public static readonly Color Panel    = new Color(0.16f, 0.20f, 0.31f, 1f); // 侧栏面板
        public static readonly Color Card     = new Color(0.21f, 0.27f, 0.40f, 1f); // 角色卡
        public static readonly Color Cell     = new Color(0.13f, 0.17f, 0.27f, 1f); // 鼓轨格底
        public static readonly Color Line     = new Color(1f, 1f, 1f, 0.10f);        // 分隔线
        public static readonly Color Accent   = new Color(0.42f, 0.82f, 1.00f, 1f); // 青（波形/主按钮）
        public static readonly Color Accent2  = new Color(0.62f, 0.48f, 1.00f, 1f); // 紫（次级强调）
        public static readonly Color Text     = new Color(0.93f, 0.95f, 0.99f, 1f); // 主文字
        public static readonly Color TextDim  = new Color(0.70f, 0.74f, 0.83f, 1f); // 次文字
        public static readonly Color Ok       = new Color(0.46f, 0.86f, 0.56f, 1f); // 绿（确定/契合）
        public static readonly Color Danger   = new Color(1.00f, 0.46f, 0.46f, 1f); // 红（取消/警示）

        private static Sprite _round;

        /// <summary>程序生成的白色圆角方块（9-slice），配合 Image.color 着色；不依赖内置资源，避免版本差异导致加载失败。</summary>
        public static Sprite Round
        {
            get
            {
                if (_round == null) _round = CreateRoundSprite();
                return _round;
            }
        }

        /// <summary>运行时生成 32×32 白色圆角矩形精灵（9-slice 边框 8px），供 Image.Type.Sliced 使用。</summary>
        private static Sprite CreateRoundSprite()
        {
            const int size = 32;
            const int radius = 10;
            const int border = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 圆角矩形 SDF：像素中心到圆角矩形边界的符号距离
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sp.name = "UI_RoundRect_Gen";
            return sp;
        }

        /// <summary>给 Image 套圆角并着色（9-slice 保持圆角不被拉伸变形）；圆角精灵缺失时回退纯色。</summary>
        public static void ApplyRound(Image img, Color color)
        {
            if (img == null) return;
            if (Round == null)
            {
                img.sprite = null;
                img.type = Image.Type.Simple;
                img.color = color;
                return;
            }
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.color = color;
        }

        /// <summary>给文字加深色描边，提升可读性（重复调用安全）。</summary>
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
