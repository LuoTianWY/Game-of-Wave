using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>程序化构建 uGUI 的工厂方法。</summary>
    public static class UIFactory
    {
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.28f, 0.95f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.GetComponent<Text>();
            text.text = label;
            text.fontSize = 15;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Stretch((RectTransform)labelGo.transform);
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
