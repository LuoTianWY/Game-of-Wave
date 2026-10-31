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

        /// <summary>拖拽过程中每帧回调，用来在鼓轨板上刷新落点虚影。</summary>
        public System.Action<DrumCharacterCard, Vector2> OnDragMove;

        private HexWaveformRenderer _waveform;
        private AudioSource _source;
        private bool _dragging;
        private bool _hovering;
        private Vector2 _hoverBasePos;
        private float _hoverBaseScale = 1f;
        private float _hoverT;
        private int _hoverSibling;

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
            UIResource.ApplyFrameOrColor(bg, "UI/card_bg", UIStyle.Card);

            // 左侧音色大类色条
            var rail = UIFactory.CreatePanel("CategoryRail", transform,
                HexCategoryInfo.BaseColor(Character.Category));
            rail.raycastTarget = false;
            var rrt = (RectTransform)rail.transform;
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 0.5f);
            rrt.sizeDelta = new Vector2(4f, -16f);
            rrt.anchoredPosition = new Vector2(2f, 0f);

            // 波形预览（上半部）：六边形路径，节奏点高亮
            var waveGo = new GameObject("Waveform", typeof(RectTransform), typeof(CanvasRenderer), typeof(HexWaveformRenderer));
            waveGo.transform.SetParent(transform, false);
            _waveform = waveGo.GetComponent<HexWaveformRenderer>();
            _waveform.SetPattern(Character.EffectivePattern, RhythmPattern.StableHash(Character.Name));
            _waveform.SetPitch(264f / (Character.Pattern.TotalBeats * RhythmPattern.HexPerBeat)); // 缩略图：整段波形铺满卡宽
            Character.Changed += RefreshWaveform;
            _waveform.color = Color.white;                      // 颜色由音色大类贴图提供
            _waveform.SetCategory(Character.Category);           // 六边形外观 = 音色大类贴图
            _waveform.raycastTarget = false;
            var waveRt = (RectTransform)waveGo.transform;
            waveRt.anchorMin = new Vector2(0f, 0.58f);
            waveRt.anchorMax = new Vector2(1f, 1f);
            waveRt.offsetMin = new Vector2(8f, 6f);
            waveRt.offsetMax = new Vector2(-8f, -6f);

            // 立绘缩略图（左下）
            var frame = UIFactory.CreatePanel("Portrait", transform, new Color(0f, 0f, 0f, 0.35f));
            frame.raycastTarget = false;
            UIStyle.ApplyWell(frame, new Color(0f, 0f, 0f, 0.35f));
            UIFactory.Place((RectTransform)frame.transform, new Vector2(0f, 0f),
                new Vector2(72f, 82f), new Vector2(10f, 8f));

            var portrait = UIResource.Load(PortraitPath());
            if (portrait != null)
            {
                var art = new GameObject("Art", typeof(RectTransform), typeof(Image));
                art.transform.SetParent(frame.transform, false);
                var img = art.GetComponent<Image>();
                img.sprite = portrait;
                img.preserveAspect = true;
                img.raycastTarget = false;
                UIFactory.Place((RectTransform)art.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(66f, 76f), Vector2.zero);
            }
            else
            {
                var fill = UIFactory.CreatePanel("PortraitFill", frame.transform,
                    HexCategoryInfo.BaseColor(Character.Category));
                fill.raycastTarget = false;
                UIFactory.Place((RectTransform)fill.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(60f, 70f), Vector2.zero);
            }

            // 名字 + 性格名词（右侧），像素字
            var label = UIFactory.PlacePixelText("Label", transform,
                Character.Name + " · " + Character.Noun, 1.5f,
                UIStyle.Text, PixelText.Align.Left,
                new Vector2(0f, 0f), new Vector2(90f, 50f));
            label.raycastTarget = false;

            // 玩法属性提示：帮助玩家理解角色为什么适合某种编排
            var stats = RehearsalTraits.GetStats(Character.Noun);

            string hintText =
                "稳 " + stats.Stability + "   密 " + stats.Density;

            var gameplayHint = UIFactory.PlacePixelText(
                "GameplayHint",
                transform,
                hintText,
                1.1f,
                UIStyle.Text,
                PixelText.Align.Left,
                new Vector2(0f, 0f),
                new Vector2(90f, 30f)
            );

            gameplayHint.raycastTarget = false;

            AddPlayButton();

            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
        }

        private void AddPlayButton()
        {
            var btn = UIFactory.CreatePixelButton("Play", transform, "试听", 1.5f,
                                                  UIStyle.Accent, UIStyle.Ink, PlayPreview);
            UIResource.ApplyFrameOrColor(btn.image, "UI/btn_play", UIStyle.Accent);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(70f, 30f);
            rt.anchoredPosition = new Vector2(-8f, 6f);
        }

        /// <summary>角色名 → 立绘路径（与角色面板用的是同一批立绘）。</summary>
        private string PortraitPath()
        {
            switch (Character.Name)
            {
                case "阿宁": return "Characters/Portraits/aning";
                case "小川": return "Characters/Portraits/xiaochuan";
                case "老周": return "Characters/Portraits/laozhou";
                case "小雨": return "Characters/Portraits/xiaoyu";
                case "石头": return "Characters/Portraits/shitou";
                case "神秘嘉宾": return "Characters/Portraits/guest";
                default: return null;
            }
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
            _dragging = true;
            ResetHover();

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
            if (OnDragMove != null) OnDragMove(this, e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            FollowPointer(e);
            if (OnDragMove != null) OnDragMove(this, e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            // 卡片是「模具」：无论是否成功落位都回到侧栏原位，可反复拖出复用（无限供给）
            if (OnDragEnd != null) OnDragEnd(this, e.position);
            ReturnToOrigin();
            _dragging = false;
        }

        // ---------- 悬浮：整张卡平滑抬起，强调可拖动 ----------
        //
        // 用每帧检测 + 缓动：
        //   · 指针进入/离开只决定目标值 0/1，实际位置用 MoveTowards + SmoothStep 过渡，
        //     所以抬升和回落都是平滑动画，而不是瞬移；
        //   · 轮询检测保证鼠标移开后一定能回落（指针事件在卡片移动时容易漏掉 Exit）。

        private void Update()
        {
            float target = 0f;
            if (!_dragging)
            {
                var canvas = GetComponentInParent<Canvas>();
                Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? canvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(Rect, Input.mousePosition, cam))
                    target = 1f;
            }

            if (Mathf.Approximately(_hoverT, target)) return;

            if (!_hovering && target > 0f)
            {
                // 开始悬浮：记录基准状态并抬到相邻卡之上
                _hovering = true;
                _hoverBasePos = Rect.anchoredPosition;
                _hoverBaseScale = Rect.localScale.x;
                _hoverSibling = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
            }

            _hoverT = Mathf.MoveTowards(_hoverT, target, Time.unscaledDeltaTime * 7f);
            float e = Mathf.SmoothStep(0f, 1f, _hoverT);
            Rect.anchoredPosition = _hoverBasePos + new Vector2(0f, 6f * e);
            Rect.localScale = Vector3.one * Mathf.Lerp(_hoverBaseScale, _hoverBaseScale * 1.035f, e);

            if (_hoverT <= 0f && _hovering)
            {
                _hovering = false;
                Rect.anchoredPosition = _hoverBasePos;
                Rect.localScale = Vector3.one * _hoverBaseScale;
                transform.SetSiblingIndex(_hoverSibling);
            }
        }

        /// <summary>拖动开始时立刻结束悬浮并复位。</summary>
        private void ResetHover()
        {
            if (!_hovering && _hoverT <= 0f) return;

            _hoverT = 0f;
            _hovering = false;
            Rect.anchoredPosition = _hoverBasePos;
            Rect.localScale = Vector3.one * _hoverBaseScale;
            transform.SetSiblingIndex(_hoverSibling);
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
