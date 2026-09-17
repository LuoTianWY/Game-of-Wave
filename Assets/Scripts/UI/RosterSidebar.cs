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
        private Text _tabLabel;
        private readonly List<DrumCharacterCard> _cards = new List<DrumCharacterCard>();

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
            UIResource.ApplySpriteOrColor(bg, "UI/sidebar", new Color(0.10f, 0.10f, 0.14f, 0.96f));

            // 卡片容器（面板左起 TabWidth 处开始摆卡）
            _cardsRoot = new GameObject("Cards", typeof(RectTransform)).transform;
            _cardsRoot.SetParent(transform, false);
            var cr = (RectTransform)_cardsRoot;
            cr.anchorMin = Vector2.zero;
            cr.anchorMax = Vector2.one;
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            BuildTab();
            ApplyPanelPosition();
        }

        private void BuildTab()
        {
            var go = new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            UIResource.ApplySpriteOrColor(img, "UI/sidebar_tab", new Color(0.30f, 0.60f, 0.85f, 0.95f));
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Toggle);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(TabWidth, 120f);
            rt.anchoredPosition = Vector2.zero;

            _tabLabel = UIFactory.CreateText("Label", go.transform, "▶", 20, Color.white, TextAnchor.MiddleCenter);
            _tabLabel.raycastTarget = false;
            UIFactory.Stretch((RectTransform)_tabLabel.transform);
        }

        private void Toggle()
        {
            _expanded = !_expanded;
            ApplyPanelPosition();
        }

        private void ApplyPanelPosition()
        {
            Rect.anchoredPosition = new Vector2(_expanded ? 0f : PanelWidth - TabWidth, 0f);
            if (_tabLabel != null) _tabLabel.text = _expanded ? "▶" : "◀";
        }

        /// <summary>清空并重新陈列角色卡。</summary>
        public void Populate(IReadOnlyList<DrumCharacter> characters)
        {
            ClearCards();
            float y = -20f;
            foreach (var c in characters)
            {
                var card = DrumCharacterCard.Create(c, _cardsRoot);
                _cards.Add(card);
                var rt = card.Rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(TabWidth + 12f, y);
                y -= DrumCharacterCard.Height + 12f;
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
