using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>拖拽卡上的一个 tile 块（供播放时高亮）。</summary>
    public sealed class CardBlock
    {
        public Image Image;
        public Color Base;
        public float Onset;    // 相对卡片起点的拍数
        public float Duration;
        public float Pitch;
    }

    /// <summary>
    /// 一张可拖拽的角色卡：把一名角色的波形画成块状条。
    /// 若存在同名贴图（Resources/Characters/&lt;名字&gt;.png），则用贴图替换块状波形（素材替换钩子）。
    /// </summary>
    public sealed class DraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Character Data;
        public bool InRoster;
        public readonly List<CardBlock> Blocks = new List<CardBlock>();

        public System.Action<DraggableCard, Vector2> OnDragBegin;
        public System.Action<DraggableCard, Vector2> OnDragging;
        public System.Action<DraggableCard, Vector2> OnDragEnd;

        public RectTransform Rect { get { return (RectTransform)transform; } }

        public static DraggableCard Create(Character data, bool inRoster, float pixelsPerBeat, float tileMaxHeight, float maxPitch)
        {
            var go = new GameObject("Card_" + data.Name, typeof(RectTransform), typeof(Image), typeof(DraggableCard));
            var card = go.GetComponent<DraggableCard>();
            card.Data = data;
            card.InRoster = inRoster;
            card.Rect.pivot = new Vector2(0f, 0.5f);
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0f, 1f);
            card.BuildVisual(pixelsPerBeat, tileMaxHeight, maxPitch);
            return card;
        }

        public void BuildVisual(float pixelsPerBeat, float tileMaxHeight, float maxPitch)
        {
            Blocks.Clear();
            var bg = GetComponent<Image>();
            bg.color = new Color(0.13f, 0.13f, 0.17f, 0.92f);

            float totalBeats = ChainTimeline.TotalBeats(Data);
            float cardWidth = totalBeats * pixelsPerBeat;
            Rect.sizeDelta = new Vector2(cardWidth, tileMaxHeight + 56f);

            var sprite = Resources.Load<Sprite>("Characters/" + Data.Name);
            if (sprite != null)
            {
                // 素材替换钩子：存在同名贴图则整卡显示贴图，不再画块状波形
                var spriteGo = new GameObject("Sprite", typeof(RectTransform), typeof(Image));
                spriteGo.transform.SetParent(transform, false);
                var img = spriteGo.GetComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = (RectTransform)spriteGo.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                AddName();
                return;
            }

            float x = 0f;
            float onset = 0f;
            foreach (var t in Data.Tiles)
            {
                var block = new GameObject("t", typeof(RectTransform), typeof(Image));
                block.transform.SetParent(transform, false);
                var img = block.GetComponent<Image>();
                img.color = TraitColor.Of(t.Trait);
                img.raycastTarget = false;
                var rt = (RectTransform)block.transform;
                rt.pivot = new Vector2(0f, 0f);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(x, 0f);
                rt.sizeDelta = new Vector2(t.Duration * pixelsPerBeat - 4f, Mathf.Max(12f, tileMaxHeight * (t.Pitch / maxPitch)));

                Blocks.Add(new CardBlock { Image = img, Base = img.color, Onset = onset, Duration = t.Duration, Pitch = t.Pitch });

                x += t.Duration * pixelsPerBeat;
                onset += t.Duration;
            }

            AddName();
        }

        private void AddName()
        {
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(transform, false);
            var text = nameGo.GetComponent<Text>();
            text.text = Data.Name;
            text.fontSize = 24;
            text.color = new Color(0.92f, 0.92f, 0.96f, 1f);
            text.alignment = TextAnchor.LowerLeft;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.raycastTarget = false;
            var rt = (RectTransform)nameGo.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(Rect.sizeDelta.x - 8f, 52f);
            rt.anchoredPosition = new Vector2(4f, 0f);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (OnDragBegin != null) OnDragBegin(this, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (OnDragging != null) OnDragging(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (OnDragEnd != null) OnDragEnd(this, eventData.position);
        }
    }
}
