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
        private const float TabVisualWidth = 34f;   // 拉手实际宽度（比收起后的可见宽度略宽）
        private const float ReferenceHeight = 1080f;

        private bool _expanded = true;
        private Transform _cardsRoot;
        private Image _tabArrow;
        private Sprite _chevronRight;
        private Sprite _chevronLeft;
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

            var subtitle = UIFactory.PlacePixelText("Subtitle", transform, "ROSTER", 1.1f,
                                                   UIStyle.TextDim, PixelText.Align.Left,
                                                   new Vector2(0f, 1f),
                                                   new Vector2(TabWidth + 92f, -22f));
            subtitle.raycastTarget = false;

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
            rt.sizeDelta = new Vector2(TabVisualWidth, 132f);
            rt.anchoredPosition = Vector2.zero;

            // 只保留一个像素箭头（程序化画，比字体里的三角更清楚）
            _chevronRight = MakeChevronSprite(16, UIStyle.Text, true);
            _chevronLeft = MakeChevronSprite(16, UIStyle.Text, false);

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(go.transform, false);
            _tabArrow = arrowGo.GetComponent<Image>();
            _tabArrow.sprite = _chevronRight;
            _tabArrow.raycastTarget = false;
            UIFactory.Place((RectTransform)arrowGo.transform, new Vector2(0.5f, 0.5f),
                new Vector2(16f, 16f), new Vector2(2f, 0f));
        }

        /// <summary>程序化画一个像素箭头（chevron），比字体三角在拉手上更醒目。</summary>
        private static Sprite MakeChevronSprite(int size, Color color, bool pointRight)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var px = new Color[size * size];

            int mid = size / 2;
            for (int i = 0; i < mid; i++)
            {
                int x = pointRight ? i : size - 1 - i;
                int y1 = mid - i;
                int y2 = mid + i;
                for (int t = 0; t < 2; t++)
                {
                    int xx = Mathf.Clamp(x + t, 0, size - 1);
                    px[y1 * size + xx] = color;
                    px[y2 * size + xx] = color;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size),
                                 new Vector2(0.5f, 0.5f), size);
        }

        private void Toggle()
        {
            _expanded = !_expanded;
            ApplyPanelPosition();
        }

        private void ApplyPanelPosition()
        {
            Rect.anchoredPosition = new Vector2(_expanded ? 0f : PanelWidth - TabVisualWidth, 0f);
            if (_tabArrow != null)
                _tabArrow.sprite = _expanded ? _chevronRight : _chevronLeft;
        }

        /// <summary>清空并重新陈列角色卡。</summary>
        public void Populate(IReadOnlyList<DrumCharacter> characters)
        {
            ClearCards();
            float y = -HeaderHeight - 14f;
            int index = 0;
            foreach (var c in characters)
            {
                var card = DrumCharacterCard.Create(c, _cardsRoot);
                _cards.Add(card);
                var rt = card.Rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(TabWidth + 12f, y);

                // 卡片左上角的编号（01..06），像设备上的通道号
                var num = UIFactory.PlacePixelText("Index", rt, (index + 1).ToString("00"),
                    1.1f, UIStyle.TextDim, PixelText.Align.Left,
                    new Vector2(0f, 1f), new Vector2(6f, -6f));
                num.raycastTarget = false;

                // 编号旁边一颗小色点，作为通道指示灯
                var dot = UIFactory.CreatePanel("ChannelDot", rt, UIStyle.Accent2);
                dot.raycastTarget = false;
                UIFactory.Place((RectTransform)dot.transform, new Vector2(0f, 1f),
                    new Vector2(6f, 6f), new Vector2(26f, -12f));
                index++;

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
