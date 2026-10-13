using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>程序化构建 uGUI 的工厂方法。</summary>
    public static class UIFactory
    {
        /// <summary>
        /// 内置字体兼容获取：2021.x 及更早为 Arial.ttf，Unity/团结 2022.2+ 为 LegacyRuntime.ttf。
        /// 先试 Arial（2021 直接命中、无报错）；2022.2+ 调 Arial 会抛 ArgumentException，再回落 LegacyRuntime。
        /// </summary>
        public static Font BuiltinFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { /* 2022.2+ 已移除 Arial，会抛异常 */ }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { /* 理论上不会到这里 */ }
            }
            return font;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.font = BuiltinFont();
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = UIStyle.Grey;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.GetComponent<Text>();
            text.text = label;
            text.fontSize = 30;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = BuiltinFont();
            Stretch((RectTransform)labelGo.transform);
            return btn;
        }

        // ---------- 像素字 / 像素按钮 ----------
        //
        // 音轨板这一套界面已经全面改用项目自制的 PixelText（位图像素字），
        // 不再用内置 Arial —— 否则同一个游戏里会出现两套字形。

        /// <summary>居中、按内容自适应的像素文字（按钮标签用这个）。</summary>
        public static PixelText CreatePixelText(string name, Transform parent, string text,
                                                float pixelScale, Color color,
                                                PixelText.Align align = PixelText.Align.Left)
        {
            return PixelText.Create(name, parent, text, pixelScale, color, align);
        }

        /// <summary>按锚点摆放的像素文字，尺寸取 PreferredSize（面板里的标题/说明用这个）。</summary>
        public static PixelText PlacePixelText(string name, Transform parent, string text,
                                               float pixelScale, Color color, PixelText.Align align,
                                               Vector2 anchor, Vector2 anchoredPos)
        {
            var t = PixelText.Create(name, parent, text, pixelScale, color, align);
            Place((RectTransform)t.transform, anchor, t.PreferredSize, anchoredPos);
            return t;
        }

        /// <summary>
        /// 像素风按钮：凸面像素边框 + 居中像素字 + 调好的状态色。
        /// hover 略微提亮、按下压暗、禁用变灰，都用 ColorBlock 的 tint 实现，
        /// 不会污染 Image.color（底色要留着当按钮颜色）。
        /// </summary>
        public static Button CreatePixelButton(string name, Transform parent, string label,
                                               float pixelScale, Color fill, Color textColor,
                                               System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            UIStyle.ApplyFrame(img, fill);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var text = PixelText.Create("Label", go.transform, label, pixelScale, textColor,
                                        PixelText.Align.Center);
            text.raycastTarget = false;
            return btn;
        }

        /// <summary>按锚点/尺寸/偏移摆放一个 RectTransform。</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 anchoredPos)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>创建一条 tile 波形条（左上锚点），tile 用彩色块表示：高度∝音高、颜色∝特质。</summary>
        public static RectTransform CreateTileStrip(string name, Transform parent, IReadOnlyList<Tile> tiles, float tileWidth, float maxHeight, Vector2 anchoredPos)
        {
            var root = CreateRect(name, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = anchoredPos;
            PopulateStrip(root, tiles, tileWidth, maxHeight);
            return root;
        }

        /// <summary>重建一条 tile 波形条的内容（清空后重新填充）。</summary>
        public static void PopulateStrip(RectTransform root, IReadOnlyList<Tile> tiles, float tileWidth, float maxHeight)
        {
            foreach (Transform child in root) Object.Destroy(child.gameObject);
            root.sizeDelta = new Vector2(tiles.Count * tileWidth, maxHeight);
            float x = 0f;
            foreach (var t in tiles)
            {
                var block = CreatePanel("t", root, TraitColor.Of(t.Trait));
                var rt = (RectTransform)block.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(x, 0f);
                rt.sizeDelta = new Vector2(tileWidth - 2f, Mathf.Max(4f, maxHeight * (t.Pitch / 6f)));
                x += tileWidth;
            }
        }
    }
}
