using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;

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

        private HexWaveformRenderer _waveform;
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
            UIStyle.ApplyRound(bg, UIStyle.Card);

            // 波形预览（上半部）：六边形路径，节奏点高亮
            var waveGo = new GameObject("Waveform", typeof(RectTransform), typeof(CanvasRenderer), typeof(HexWaveformRenderer));
            waveGo.transform.SetParent(transform, false);
            _waveform = waveGo.GetComponent<HexWaveformRenderer>();
            _waveform.SetPattern(Character.EffectivePattern, RhythmPattern.StableHash(Character.Name));
            _waveform.SetPitch(264f / (Character.Pattern.TotalBeats * RhythmPattern.HexPerBeat)); // 缩略图：整段波形铺满卡宽
            Character.Changed += RefreshWaveform;
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
                UIStyle.Text, TextAnchor.MiddleLeft);
            UIStyle.OutlineText(label);
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
            UIStyle.ApplyRound(img, UIStyle.Accent);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(PlayPreview);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(76f, 34f);
            rt.anchoredPosition = new Vector2(-10f, 4f);

            var label = UIFactory.CreateText("Label", go.transform, "试听", 20, Color.white, TextAnchor.MiddleCenter);
            UIStyle.OutlineText(label);
            label.raycastTarget = false;
            UIFactory.Stretch((RectTransform)label.transform);
        }

        private void PlayPreview()
        {
            if (Character == null || Character.Sample == null || Character.Sample.Clip == null) return;
            _source.PlayOneShot(Character.Sample.Clip);
        }

        /// <summary>加点/洗点后刷新波形预览（角色共享，故订阅其 Changed 事件）。</summary>
        private void RefreshWaveform()
        {
            if (_waveform != null && Character != null)
                _waveform.SetPattern(Character.EffectivePattern, RhythmPattern.StableHash(Character.Name));
        }

        private void OnDestroy()
        {
            if (Character != null) Character.Changed -= RefreshWaveform;
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
