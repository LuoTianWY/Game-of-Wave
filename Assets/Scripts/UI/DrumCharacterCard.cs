using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;

namespace WaveTeam.UI
{
    /// <summary>
    /// 鼓角色卡：波形预览 + 名字/性格名词 + 试听按钮 + 拖拽。
    /// 拖出侧栏后跟随鼠标；松开时若 OnDragEnd 返回 false（或未订阅），则回到原位。
    /// </summary>
    public sealed class DrumCharacterCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Width = 280f;
        public const float Height = 150f;

        public DrumCharacter Character { get; private set; }

        /// <summary>拖拽结束回调，返回 true 表示已被安置（不会回原位）。</summary>
        public System.Func<DrumCharacterCard, Vector2, bool> OnDragEnd;

        private WaveformRenderer _waveform;
        private AudioSource _source;

        private Transform _originalParent;
        private Vector2 _originalAnchored;
        private Vector2 _originalPivot;
        private Canvas _rootCanvas;

        public RectTransform Rect { get { return (RectTransform)transform; } }

        public static DrumCharacterCard Create(DrumCharacter character, Transform parent)
        {
            var go = new GameObject("Card_" + character.Name, typeof(RectTransform), typeof(Image), typeof(DrumCharacterCard));
            go.transform.SetParent(parent, false);
            var card = go.GetComponent<DrumCharacterCard>();
            card.Character = character;
            card.BuildVisual();
            return card;
        }

        private void BuildVisual()
        {
            Rect.sizeDelta = new Vector2(Width, Height);

            var bg = GetComponent<Image>();
            UIResource.ApplySpriteOrColor(bg, "UI/card_bg", new Color(0.13f, 0.13f, 0.17f, 0.95f));

            // 波形预览（上半部）
            var waveGo = new GameObject("Waveform", typeof(RectTransform), typeof(WaveformRenderer));
            waveGo.transform.SetParent(transform, false);
            _waveform = waveGo.GetComponent<WaveformRenderer>();
            _waveform.SetDefinition(Character.Waveform);
            _waveform.color = new Color(0.45f, 0.85f, 1f, 1f); // 波形即角色符号，用醒目青色
            _waveform.raycastTarget = false;
            var waveRt = (RectTransform)waveGo.transform;
            waveRt.anchorMin = new Vector2(0f, 0.45f);
            waveRt.anchorMax = new Vector2(1f, 1f);
            waveRt.offsetMin = new Vector2(8f, 6f);
            waveRt.offsetMax = new Vector2(-8f, -6f);

            // 名字 + 性格名词（左下）
            var label = UIFactory.CreateText("Label", transform,
                Character.Name + " · " + Character.Noun, 20,
                new Color(0.92f, 0.92f, 0.96f, 1f), TextAnchor.MiddleLeft);
            label.raycastTarget = false;
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = labelRt.anchorMax = labelRt.pivot = new Vector2(0f, 0f);
            labelRt.sizeDelta = new Vector2(180f, 40f);
            labelRt.anchoredPosition = new Vector2(10f, 4f);

            AddPlayButton();

            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
        }

        private void AddPlayButton()
        {
            var go = new GameObject("Play", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.60f, 0.85f, 0.95f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(PlayPreview);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(76f, 34f);
            rt.anchoredPosition = new Vector2(-10f, 4f);

            var label = UIFactory.CreateText("Label", go.transform, "试听", 20, Color.white, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            UIFactory.Stretch((RectTransform)label.transform);
        }

        private void PlayPreview()
        {
            if (Character == null || Character.Sample == null || Character.Sample.Clip == null) return;
            _source.PlayOneShot(Character.Sample.Clip);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _originalParent = transform.parent;
            _originalAnchored = Rect.anchoredPosition;
            _originalPivot = Rect.pivot;

            _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas != null)
            {
                transform.SetParent(_rootCanvas.transform, true);
                transform.SetAsLastSibling();
            }
            Rect.pivot = new Vector2(0.5f, 0.5f);
            FollowPointer(e);
        }

        public void OnDrag(PointerEventData e)
        {
            FollowPointer(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            // 卡片是「模具」：无论是否成功落位都回到侧栏原位，可反复拖出复用（无限供给）
            if (OnDragEnd != null) OnDragEnd(this, e.position);
            ReturnToOrigin();
        }

        /// <summary>回到拖拽前的侧栏位置。</summary>
        public void ReturnToOrigin()
        {
            if (_originalParent != null)
            {
                transform.SetParent(_originalParent, false);
                Rect.pivot = _originalPivot;
                Rect.anchoredPosition = _originalAnchored;
            }
            _rootCanvas = null;
        }

        private void FollowPointer(PointerEventData e)
        {
            if (_rootCanvas == null) return;
            var canvasRect = (RectTransform)_rootCanvas.transform;
            Vector3 world;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, e.position, _rootCanvas.worldCamera, out world))
            {
                Rect.position = world;
            }
        }
    }
}
