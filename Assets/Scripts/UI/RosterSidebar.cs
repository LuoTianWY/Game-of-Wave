using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Audio;

namespace WaveTeam.UI
{
    /// <summary>
    /// 右侧角色栏：展开/收起 + 陈列可拖拽的角色卡。
    /// </summary>
    public sealed class RosterSidebar : MonoBehaviour
    {
        public const float PanelWidth = 320f;
        public const float TabWidth = 24f;
        private const float ReferenceHeight = 1080f;

        private bool _expanded = true;
        private Transform _cardsRoot;
        private PixelText _tabLabel;
        private readonly List<DrumCharacterCard> _cards = new List<DrumCharacterCard>();

        private const float HeaderHeight = 62f;   // 顶部「角色」标题占的高度，卡片整体让开

        public RectTransform Rect { get { return (RectTransform)transform; } }
        public IReadOnlyList<DrumCharacterCard> Cards { get { return _cards; } }

        public static RosterSidebar Create(Transform parent)
        {
            var go = new GameObject("RosterSidebar", typeof(RectTransform), typeof(Image), typeof(RosterSidebar));
            go.transform.SetParent(parent, false);
            var sb = go.GetComponent<RosterSidebar>();
            sb.Build();
            return sb;
        }

        private void Build()
        {
            Rect.anchorMin = Rect.anchorMax = Rect.pivot = new Vector2(1f, 0.5f);
            Rect.sizeDelta = new Vector2(PanelWidth, ReferenceHeight);
            Rect.anchoredPosition = Vector2.zero;

            var bg = GetComponent<Image>();
            // 侧栏用偏紫的深咖色，和青灰色的音轨区、暖木色的调音台形成三个色区
            UIResource.ApplyFrameOrColor(bg, "UI/sidebar", new Color(0.20f, 0.145f, 0.15f, 1f));

            // 卡片陈列区背后的暗槽，让整列卡片像卡在设备槽里
            var shelf = UIFactory.CreatePanel("CardShelf", transform, new Color(0f, 0f, 0f, 0.20f));
            shelf.raycastTarget = false;
            var shrt = (RectTransform)shelf.transform;
            shrt.anchorMin = Vector2.zero;
            shrt.anchorMax = Vector2.one;
            shrt.offsetMin = new Vector2(TabWidth + 10f, 10f);
            shrt.offsetMax = new Vector2(-10f, -HeaderHeight - 6f);

            // 面板四角螺丝
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                var screw = UIFactory.CreatePanel("Screw", transform, UIStyle.EdgeDeep);
                screw.raycastTarget = false;
                UIFactory.Place((RectTransform)screw.transform, corner,
                    new Vector2(10f, 10f),
                    new Vector2(corner.x > 0.5f ? -9f : 9f, corner.y > 0.5f ? -9f : 9f));
            }

            // 卡片区左侧的竖向色条（和音轨的薄荷色呼应）
            var rail = UIFactory.CreatePanel("Rail", transform, UIStyle.Accent2);
            rail.raycastTarget = false;
            var railRt = (RectTransform)rail.transform;
            railRt.anchorMin = new Vector2(0f, 0f);
            railRt.anchorMax = new Vector2(0f, 1f);
            railRt.pivot = new Vector2(0f, 0.5f);
            railRt.sizeDelta = new Vector2(3f, -HeaderHeight - 20f);
            railRt.anchoredPosition = new Vector2(TabWidth + 6f, -6f);

            // 卡片容器（面板左起 TabWidth 处开始摆卡）
            _cardsRoot = new GameObject("Cards", typeof(RectTransform)).transform;
            _cardsRoot.SetParent(transform, false);
            var cr = (RectTransform)_cardsRoot;
            cr.anchorMin = Vector2.zero;
            cr.anchorMax = Vector2.one;
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            BuildHeader();
            BuildTab();
            ApplyPanelPosition();
        }

        /// <summary>顶部标题 + 分隔线。卡片从这里往下排，不会被压住。</summary>
        private void BuildHeader()
        {
            var title = UIFactory.PlacePixelText("Title", transform, "角色", 2f,
                                                 UIStyle.Text, PixelText.Align.Left,
                                                 new Vector2(0f, 1f),
                                                 new Vector2(TabWidth + 16f, -18f));
            title.raycastTarget = false;

            var led = UIFactory.CreatePanel("HeaderLed", transform, UIStyle.Accent2);
            led.raycastTarget = false;
            UIFactory.Place((RectTransform)led.transform, new Vector2(1f, 1f),
                            new Vector2(14f, 14f), new Vector2(-30f, -20f));

            // 标题右侧的三颗小状态灯
            Color[] dots = { UIStyle.Ok, UIStyle.Accent, UIStyle.Danger };
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = UIFactory.CreatePanel("HeadDot_" + i, transform, dots[i]);
                dot.raycastTarget = false;
                UIFactory.Place((RectTransform)dot.transform, new Vector2(1f, 1f),
                                new Vector2(9f, 9f), new Vector2(-78f - i * 14f, -22f));
            }

            var divider = UIFactory.CreatePanel("Divider", transform, UIStyle.Line);
            divider.raycastTarget = false;
            UIFactory.Place((RectTransform)divider.transform, new Vector2(0f, 1f),
                            new Vector2(PanelWidth - TabWidth - 32f, 2f),
                            new Vector2(TabWidth + 16f, -HeaderHeight + 4f));
        }

        private void BuildTab()
        {
            var go = new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            UIResource.ApplyFrameOrColor(img, "UI/sidebar_tab", UIStyle.Accent2);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Toggle);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(TabWidth, 120f);
            rt.anchoredPosition = Vector2.zero;

            // 拉手箭头：▶ / ◀ 已在像素字图集里（Tools/make_pixel_font.py 的 EXTRA 补进去的），
            // 展开时是 ▶（指向面板外），收起时是 ◀。
            _tabLabel = PixelText.Create("Label", go.transform, "▶", 2f,
                                         UIStyle.Text, PixelText.Align.Center);
            _tabLabel.raycastTarget = false;
        }

        private void Toggle()
        {
            _expanded = !_expanded;
            ApplyPanelPosition();
        }

        private void ApplyPanelPosition()
        {
            Rect.anchoredPosition = new Vector2(_expanded ? 0f : PanelWidth - TabWidth, 0f);
            if (_tabLabel != null) _tabLabel.Text = _expanded ? "▶" : "◀";
        }

        /// <summary>清空并重新陈列角色卡。</summary>
        public void Populate(IReadOnlyList<DrumCharacter> characters)
        {
            ClearCards();
            float y = -HeaderHeight - 14f;
            foreach (var c in characters)
            {
                var card = DrumCharacterCard.Create(c, _cardsRoot);
                _cards.Add(card);
                var rt = card.Rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(TabWidth + 12f, y);

                // 卡片之间的分隔槽
                var sep = UIFactory.CreatePanel("CardSep", _cardsRoot, new Color(1f, 1f, 1f, 0.06f));
                sep.raycastTarget = false;
                UIFactory.Place((RectTransform)sep.transform, new Vector2(0f, 1f),
                    new Vector2(PanelWidth - TabWidth - 28f, 1f),
                    new Vector2(TabWidth + 14f, y - DrumCharacterCard.Height - 5f));

                y -= DrumCharacterCard.Height + 10f;
            }
        }

        private void ClearCards()
        {
            _cards.Clear();
            if (_cardsRoot == null) return;
            foreach (Transform child in _cardsRoot) Object.Destroy(child.gameObject);
        }
    }
}
