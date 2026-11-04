using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;
using WaveTeam.Room;

namespace WaveTeam.UI
{
    /// <summary>板上一个已放置的鼓（角色 + 起始拍 + 视觉）。</summary>
    public sealed class PlacedDrum
    {
        public readonly DrumCharacter Character;
        public float StartBeat { get; internal set; }
        public int LaneIndex { get; internal set; }        // 0..2，从上到下
        public readonly bool Locked;
        public readonly float DurationBeats; // 时值（拍）：该角色节奏型占的跨度
        public readonly RectTransform Rect;
        public readonly HexWaveformRenderer Waveform; // 视觉 = 六边形 blob（无框）
        internal PlacedDrum ConnectedNeighbor;        // 连接指向具体片段，换邻居时失效
        public bool ConnectedToNext { get { return ConnectedNeighbor != null; } }
        public GameObject SeamNode;                   // 接缝处的连接点按钮（待连接时显示）

        public PlacedDrum(DrumCharacter character, float startBeat, int track, float durationBeats,
                          RectTransform rect, HexWaveformRenderer waveform, bool locked = false)
        {
            Character = character;
            StartBeat = startBeat;
            LaneIndex = track;
            DurationBeats = durationBeats;
            Rect = rect;
            Waveform = waveform;
            Locked = locked;
        }
    }

    /// <summary>
    /// 鼓轨编排板：三轨共享时间轴，负责落位、渲染、放置校验（同轨不重叠）。
    /// 交互：卡片拖放落位、已放鼓左键拖动移动 / 右键收回、滚轮缩放（以鼠标为锚）、
    /// 空白处左键拖动或 A/D 平移、按钮回起点。
    /// </summary>
    public sealed class DrumTrackBoard : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler
    {
        public const float LeftMargin = 80f;
        public const float DefaultPixelsPerBeat = 94f;
        public const float SnapBeat = 1f;               // 吸附到整格（整数拍），不再 0.5 拍
        public const float TrackLengthBeats = 16f;
        public const int TrackCount = 3;                 // 三条音轨上下并排
        public const float TrackHeight = 90f;           // 单条音轨的高度
        public const float TrackGap = 10f;               // 轨与轨之间的间距

        /// <summary>三条轨整体占的高度（居中参考值）。</summary>
        public const float TrackBlockHalf =
            (TrackCount * TrackHeight + (TrackCount - 1) * TrackGap) * 0.5f;

        /// <summary>整块音轨相对板子中心的偏移（向下移，为顶部标尺留出空间）。</summary>
        public const float TrackCenterOffset = -20f;

        public const float MinPixelsPerBeat = 20f;
        public const float MaxPixelsPerBeat = 600f;
        private const float ZoomStep = 1.2f;
        private const float PanSpeed = 600f; // 像素/秒
        private const float SlotGap = 6f;    // 格槽间距
        private const float TimelineRightPadding = 12f;
        private const float RulerHeight = 48f;

        // 程序化木纹：按 1920×1080 参考分辨率铺板缝（和 RosterSidebar 的参考高一致）
        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
        private const float PlankHeight = 96f;
        private const float PlankWidth = 384f;

        private BeatSpace _space;
        private readonly List<PlacedDrum> _placed = new List<PlacedDrum>();
        private struct TimelineElement
        {
            public RectTransform Rect;
            public float Beat;
            public float Span;
            public float XOffset;
            public float WidthOffset;
        }

        private readonly List<TimelineElement> _timelineElements = new List<TimelineElement>();
        private RectTransform _trackArea;
        private RectTransform _rows;
        private RectTransform _timeline;
        private bool _fitTimeline = true;
        private Transform _drumRoot;
        private Canvas _canvas;
        private float _panLastX;
        private readonly Stack<UndoAction> _undo = new Stack<UndoAction>();
        private RehearsalRegion? _editableRegion;
        internal bool EditingLocked { get; set; }
        internal System.Action<DrumCharacter> OnPreviewCharacter;

        public IReadOnlyList<PlacedDrum> Placed { get { return _placed; } }
        public RectTransform Rect { get { return (RectTransform)transform; } }

        /// <summary>第 laneIndex 条轨的竖向中心（0 在最上面）。</summary>
        public static float TrackRowY(int laneIndex)
        {
            return TrackCenterOffset + TrackBlockHalf - TrackHeight * 0.5f
                   - laneIndex * (TrackHeight + TrackGap);
        }

        /// <summary>间隙和轨道外返回 -1。</summary>
        public static int TrackFromLocalY(float localY)
        {
            for (int lane = 0; lane < TrackCount; lane++)
                if (Mathf.Abs(localY - TrackRowY(lane)) <= TrackHeight * 0.5f)
                    return lane;
            return -1;
        }

        public static DrumTrackBoard Create(Transform parent, float topInset, float bottomInset,
                                            RehearsalRegion? editableRegion = null)
        {
            var go = new GameObject("DrumTrackBoard", typeof(RectTransform), typeof(Image), typeof(DrumTrackBoard));
            go.transform.SetParent(parent, false);
            var board = go.GetComponent<DrumTrackBoard>();
            board._editableRegion = editableRegion;
            board.Build(topInset, bottomInset);
            return board;
        }

        private void Build(float topInset, float bottomInset)
        {
            _canvas = GetComponentInParent<Canvas>();

            Rect.anchorMin = Vector2.zero;
            Rect.anchorMax = Vector2.one;
            Rect.pivot = new Vector2(0f, 0.5f);
            Rect.offsetMin = Vector2.zero;
            Rect.offsetMax = new Vector2(-RosterSidebar.PanelWidth, 0f);

            var bg = GetComponent<Image>();
            // 音轨区用偏冷的深青灰，和下方暖木色调音台拉开层次
            UIResource.ApplySpriteOrColor(bg, "UI/baseplate", new Color(0.10f, 0.14f, 0.15f, 1f));
            DrawPlanks();

            _trackArea = UIFactory.CreateRect("TrackArea", transform);
            UIFactory.Stretch(_trackArea);
            _trackArea.offsetMin = new Vector2(0f, bottomInset);
            _trackArea.offsetMax = new Vector2(0f, -topInset);

            _rows = UIFactory.CreateRect("Rows", _trackArea);
            _rows.anchorMin = new Vector2(0f, 0.5f);
            _rows.anchorMax = new Vector2(1f, 0.5f);
            _rows.sizeDelta = new Vector2(0f, TrackBlockHalf * 2f + RulerHeight + 8f);
            _rows.anchoredPosition = Vector2.zero;

            _timeline = UIFactory.CreateRect("TimelineViewport", _rows);
            UIFactory.Stretch(_timeline);
            _timeline.pivot = new Vector2(0f, 0.5f);
            _timeline.offsetMin = new Vector2(LeftMargin, 0f);
            _timeline.offsetMax = new Vector2(-TimelineRightPadding, 0f);
            _timeline.gameObject.AddComponent<RectMask2D>();

            var beatRoot = UIFactory.CreateRect("BeatLines", _timeline);
            UIFactory.Stretch(beatRoot);
            DrawTrack(beatRoot);
            DrawLaneLabels();

            var drums = UIFactory.CreateRect("Drums", _timeline);
            UIFactory.Stretch(drums);
            _drumRoot = drums;

            AddResetButton();
            ResizeTrackArea();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_drumRoot != null) ResizeTrackArea();
        }

        private void ResizeTrackArea()
        {
            float scale = Mathf.Clamp01(_trackArea.rect.height / _rows.rect.height);
            _rows.localScale = new Vector3(1f, scale, 1f);
            if (_fitTimeline) ResetView();
            else ApplyViewTransform();
        }

        private void DrawLaneLabels()
        {
            Color[] trackTint = { UIStyle.Accent2, UIStyle.Accent, UIStyle.Danger };
            for (int lane = 0; lane < TrackCount; lane++)
            {
                float y = TrackRowY(lane);
                var label = UIFactory.PlacePixelText("Layer_" + lane, _rows, "Layer " + (lane + 1),
                    1.2f, UIStyle.Text, PixelText.Align.Left,
                    new Vector2(0f, 0.5f), new Vector2(8f, y));
                label.raycastTarget = false;

                var bar = UIFactory.CreatePanel("LayerAccent_" + lane, _rows, trackTint[lane]);
                bar.raycastTarget = false;
                UIFactory.Place(bar.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(56f, 2f), new Vector2(8f, y - 14f));
            }
        }

        private void DrawTrack(Transform root)
        {
            DrawSection(root, "Opening", "开场", 0f, 4f, 0.05f);
            DrawSection(root, "Development", "发展", 4f, 8f, 0.09f);
            DrawSection(root, "Climax", "高潮", 8f, TrackLengthBeats, 0.05f);

            for (int lane = 0; lane < TrackCount; lane++)
            {
                float y = TrackRowY(lane);
                for (int beat = 0; beat < TrackLengthBeats; beat++)
                {
                    var color = beat % 2 == 0 ? UIStyle.Cell : UIStyle.CellAlt;
                    var slot = UIFactory.CreatePanel("Slot_" + lane + "_" + beat, root, color);
                    slot.raycastTarget = false;
                    UIResource.ApplyWellOrColor(slot, "UI/cell_slot", color);
                    BindToTimeline(slot.rectTransform, beat, y, TrackHeight - 4f,
                        span: 1f, xOffset: SlotGap * 0.5f, widthOffset: -SlotGap);
                }
            }

            if (_editableRegion.HasValue)
                DrawEditableRegion(root, _editableRegion.Value);

            float blockTop = TrackCenterOffset + TrackBlockHalf;
            var ruler = UIFactory.CreatePanel("BeatRuler", root, new Color(0f, 0f, 0f, 0.35f));
            ruler.raycastTarget = false;
            BindToTimeline(ruler.rectTransform, 0f, blockTop + 11f, 22f, span: TrackLengthBeats);

            for (int beat = 0; beat <= TrackLengthBeats; beat++)
            {
                bool bar = beat % 4 == 0;
                var line = UIFactory.CreatePanel("Beat_" + beat, root,
                    bar ? new Color(0.95f, 0.83f, 0.62f, 0.34f) : new Color(1f, 0.94f, 0.84f, 0.10f));
                line.raycastTarget = false;
                BindToTimeline(line.rectTransform, beat, TrackCenterOffset + 11f,
                    TrackBlockHalf * 2f + 22f, widthOffset: bar ? 2f : 1f);

                if (beat == TrackLengthBeats) continue;
                var number = PixelText.Create("RulerNum_" + beat, root, beat.ToString(),
                    1.1f, UIStyle.TextDim, PixelText.Align.Center);
                number.raycastTarget = false;
                BindToTimeline((RectTransform)number.transform, beat, blockTop + 11f,
                    number.PreferredSize.y, span: 1f);
            }
        }

        private void DrawEditableRegion(Transform root, RehearsalRegion region)
        {
            float y = TrackRowY(region.LaneIndex);
            float span = region.EndBeat - region.StartBeat;
            var fill = UIFactory.CreatePanel("EditableGap", root, new Color(1f, 0.78f, 0.25f, 0.24f));
            fill.raycastTarget = false;
            BindToTimeline(fill.rectTransform, region.StartBeat, y, TrackHeight, span: span);
            foreach (float edge in new[] { y - TrackHeight / 2f + 2f, y + TrackHeight / 2f - 2f })
            {
                var line = UIFactory.CreatePanel("GapBorder", root, UIStyle.Accent);
                line.raycastTarget = false;
                BindToTimeline(line.rectTransform, region.StartBeat, edge, 3f, span: span);
            }
            var label = UIFactory.CreateText("GapLabel", root, "补齐缺口", 16, UIStyle.Accent, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            BindToTimeline(label.rectTransform, region.StartBeat, y + TrackHeight / 2f - 13f, 20f, span: span);
        }

        private void DrawSection(Transform root, string name, string text, float startBeat, float endBeat, float tint)
        {
            float span = endBeat - startBeat;
            float top = TrackCenterOffset + TrackBlockHalf;
            var panel = UIFactory.CreatePanel(name + "Area", root, new Color(1f, 1f, 1f, tint));
            panel.raycastTarget = false;
            BindToTimeline(panel.rectTransform, startBeat, TrackCenterOffset + RulerHeight * 0.5f,
                TrackBlockHalf * 2f + RulerHeight, span: span);

            // 像素字库缺少部分中文分区字形，分区标题与排练目标共用系统字体。
            var label = UIFactory.CreateText(name + "Label", root, text, 19, UIStyle.Text, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            BindToTimeline(label.rectTransform, startBeat, top + 35f, 26f, span: span);
        }

        private void BindToTimeline(RectTransform rect, float beat, float y, float height,
                                    float span = 0f, float xOffset = 0f, float widthOffset = 0f)
        {
            UIFactory.Place(rect, new Vector2(0f, 0.5f), new Vector2(0f, height), new Vector2(0f, y));
            _timelineElements.Add(new TimelineElement
            {
                Rect = rect, Beat = beat, Span = span, XOffset = xOffset, WidthOffset = widthOffset
            });
        }

        /// <summary>
        /// 程序化木纹底：横向板缝 + 隔一段一条竖缝。
        /// 压在底板（UI/baseplate 或 UIStyle.Stage）之上、格槽之下，
        /// 让面板看起来是张木工台而不是一块纯色布。
        /// </summary>

        private void DrawPlanks()
        {
            var root = UIFactory.CreateRect("Planks", transform);
            UIFactory.Stretch(root);

            for (float y = 0f; y <= RefHeight; y += PlankHeight)
            {
                var seam = UIFactory.CreatePanel("Seam", root, new Color(0f, 0f, 0f, 0.20f));
                seam.raycastTarget = false;
                var srt = (RectTransform)seam.transform;
                srt.anchorMin = new Vector2(0f, 1f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot = new Vector2(0.5f, 1f);
                srt.sizeDelta = new Vector2(0f, 2f);
                srt.anchoredPosition = new Vector2(0f, -y);

                var hi = UIFactory.CreatePanel("SeamHi", root, new Color(1f, 0.92f, 0.80f, 0.045f));
                hi.raycastTarget = false;
                var hrt = (RectTransform)hi.transform;
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.sizeDelta = new Vector2(0f, 1f);
                hrt.anchoredPosition = new Vector2(0f, -y - 2f);
            }

            for (float x = 0f; x <= RefWidth; x += PlankWidth)
            {
                var joint = UIFactory.CreatePanel("Joint", root, new Color(0f, 0f, 0f, 0.12f));
                joint.raycastTarget = false;
                var jrt = (RectTransform)joint.transform;
                jrt.anchorMin = new Vector2(0f, 0f);
                jrt.anchorMax = new Vector2(0f, 1f);
                jrt.pivot = new Vector2(0f, 0.5f);
                jrt.sizeDelta = new Vector2(1f, 0f);
                jrt.anchoredPosition = new Vector2(x, 0f);
            }
        }

        private void AddResetButton()
        {
            var btn = UIFactory.CreatePixelButton("ResetView", transform, "回起点", 2f,
                                                  UIStyle.Grey, UIStyle.Text, ResetView);
            UIResource.ApplyFrameOrColor(btn.image, "UI/btn_reset", UIStyle.Grey);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(140, 44);
            rt.anchoredPosition = new Vector2(16, -86);   // 让开顶部的提示条
        }

        // ---------- 视口平移 / 缩放 ----------

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(h) > 0.01f)
            {
                Pan(h * PanSpeed * Time.deltaTime);
            }
        }

        public void OnScroll(PointerEventData e)
        {
            float localX = LocalX(e.position);
            if (float.IsNaN(localX)) return;
            float anchorBeat = _space.ToBeat(localX);
            float factor = e.scrollDelta.y > 0f ? ZoomStep : 1f / ZoomStep;
            ZoomAt(factor, anchorBeat);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _panLastX = e.button == PointerEventData.InputButton.Left ? LocalX(e.position) : float.NaN;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            float x = LocalX(e.position);
            if (float.IsNaN(x) || float.IsNaN(_panLastX)) { _panLastX = x; return; }
            float dx = x - _panLastX;
            _panLastX = x;
            if (Mathf.Abs(dx) > 0.001f) Pan(dx);
        }

        private void Pan(float dx)
        {
            _fitTimeline = false;
            _space.Pan(dx);
            ApplyViewTransform();
        }

        private void ZoomAt(float factor, float anchorBeat)
        {
            float newPpb = Mathf.Clamp(_space.PixelsPerBeat * factor, MinPixelsPerBeat, MaxPixelsPerBeat);
            float eff = newPpb / _space.PixelsPerBeat;
            _fitTimeline = false;
            _space.Zoom(eff, anchorBeat);
            ApplyViewTransform();
        }

        private void ResetView()
        {
            _fitTimeline = true;
            float fit = (_timeline.rect.width - 4f) / TrackLengthBeats;
            _space = new BeatSpace(Mathf.Clamp(fit, MinPixelsPerBeat, DefaultPixelsPerBeat), 2f);
            ApplyViewTransform();
        }

        private float LocalX(Vector2 screenPos)
        {
            Vector2 local;
            if (_canvas != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_timeline, screenPos, _canvas.worldCamera, out local))
                return local.x;
            return float.NaN;
        }

        /// <summary>平移 / 缩放后重排拍线与已放鼓。</summary>
        private void ApplyViewTransform()
        {
            foreach (var element in _timelineElements)
            {
                var rt = element.Rect;
                rt.anchoredPosition = new Vector2(_space.ToX(element.Beat) + element.XOffset, rt.anchoredPosition.y);
                rt.sizeDelta = new Vector2(_space.SpanToWidth(element.Span) + element.WidthOffset, rt.sizeDelta.y);
            }
            foreach (var p in _placed) LayoutPlaced(p);
            // 虚影也跟着平移/缩放走，否则拖拽中滚轮一缩放它就飘了
            if (_ghost != null && _ghost.gameObject.activeSelf) PlaceGhost();
        }

        // ---------- 落位 / 校验 ----------

        /// <summary>落位到最近的整拍。</summary>
        public static float Snap(float beat) { return Mathf.Round(beat / SnapBeat) * SnapBeat; }

        /// <summary>仅在同轨检查区间相交，首尾相接允许放置。</summary>
        public bool CanPlace(DrumCharacter character, float startBeat, int laneIndex, PlacedDrum except = null)
        {
            if (EditingLocked || (except != null && except.Locked)) return false;
            if (_editableRegion.HasValue && (character == null ||
                !_editableRegion.Value.Contains(laneIndex, startBeat, character.Pattern.TotalBeats))) return false;
            return IsSpaceAvailable(character, startBeat, laneIndex, except);
        }

        private bool IsSpaceAvailable(DrumCharacter character, float startBeat, int laneIndex, PlacedDrum except)
        {
            if (character == null || laneIndex < 0 || laneIndex >= TrackCount) return false;
            float duration = character.Pattern.TotalBeats;
            if (!(duration > 0f && startBeat >= 0f && startBeat + duration <= TrackLengthBeats)) return false;

            foreach (var p in _placed)
            {
                if (p == except || p.LaneIndex != laneIndex) continue;
                if (startBeat < p.StartBeat + p.DurationBeats && startBeat + duration > p.StartBeat) return false;
            }
            return true;
        }

        /// <summary>角色卡和已放片段共用同一套落点计算。</summary>
        private bool ResolveDrop(Vector2 screenPos, out float beat, out int laneIndex)
        {
            beat = 0f;
            laneIndex = -1;
            if (_canvas == null || (IsDropBlocked != null && IsDropBlocked(screenPos))) return false;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _timeline, screenPos, _canvas.worldCamera, out local)
                || !_timeline.rect.Contains(local)) return false;

            laneIndex = TrackFromLocalY(local.y);
            float rawBeat = _space.ToBeat(local.x);
            beat = Snap(rawBeat);
            return laneIndex >= 0 && rawBeat >= 0f && rawBeat < TrackLengthBeats;
        }

        public bool TryPlaceFromCard(DrumCharacterCard card, Vector2 screenPos)
        {
            return card != null && ResolveDrop(screenPos, out float beat, out int laneIndex)
                && Place(card.Character, beat, laneIndex) != null;
        }

        public bool TryMoveFromPointer(PlacedDrum drum, Vector2 screenPos)
        {
            return ResolveDrop(screenPos, out float beat, out int laneIndex) && TryMove(drum, beat, laneIndex);
        }

        // ---------- 拖拽落点预览 ----------
        //
        // 虚影是一个**没有调 SetCategory 的** HexWaveformRenderer：mainTexture 会退回白图，
        // 于是 Graphic.color 能直接拿来上绿/红，不会和音色贴图相乘变脏。

        private static readonly Color GhostOk = new Color(0.43f, 0.75f, 0.45f, 0.42f);
        private static readonly Color GhostBad = new Color(0.76f, 0.35f, 0.29f, 0.42f);

        private HexWaveformRenderer _ghost;
        private PixelText _ghostLabel;
        private float _ghostBeat;
        private int _ghostTrack;
        private float _ghostBeats = 1f;

        /// <summary>拖拽中每帧刷新落点虚影：绿=能放，红=不能放（重叠 / 越界 / 超出 16 拍）。</summary>
        public void ShowDropPreview(DrumCharacter character, Vector2 screenPos, PlacedDrum moving = null)
        {
            if (EditingLocked || (moving != null && moving.Locked) ||
                character == null || !ResolveDrop(screenPos, out float beat, out int laneIndex))
            {
                HideDropPreview();
                return;
            }

            EnsureGhost(character);
            SetGhostVisible(true);
            _ghostBeat = beat;
            _ghostTrack = laneIndex;
            _ghostBeats = character.Pattern.TotalBeats;
            _ghost.color = CanPlace(character, beat, laneIndex, moving) ? GhostOk : GhostBad;
            PlaceGhost();
            _ghostLabel.Text = "Layer " + (laneIndex + 1) + " · 拍 " + Mathf.RoundToInt(beat);
            _ghost.rectTransform.SetAsLastSibling();
        }

        /// <summary>松手 / 落位失败后收起虚影。</summary>
        public void HideDropPreview()
        {
            SetGhostVisible(false);
        }

        private void SetGhostVisible(bool on)
        {
            if (_ghost != null && _ghost.gameObject.activeSelf != on)
                _ghost.gameObject.SetActive(on);
        }

        private void EnsureGhost(DrumCharacter c)
        {
            if (_ghost != null)
            {
                _ghost.SetPattern(c.EffectivePattern, RhythmPattern.StableHash(c.Name));
                return;
            }

            var go = new GameObject("DropGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(HexWaveformRenderer));
            go.transform.SetParent(_drumRoot, false);

            _ghost = go.GetComponent<HexWaveformRenderer>();
            _ghost.SetPattern(c.EffectivePattern, RhythmPattern.StableHash(c.Name));
            _ghost.SetBaselineAnchored(true);
            _ghost.SetVerticalLimit(TrackHeight - 8f);
            _ghost.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.SetAsLastSibling();      // 永远压在已放鼓上面，看得清

            _ghostLabel = UIFactory.PlacePixelText("Beat", go.transform, "Layer 1 · 拍 00", 1.2f,
                                                   UIStyle.Text, PixelText.Align.Left,
                                                   new Vector2(0f, 1f), new Vector2(4f, -2f));
            _ghostLabel.raycastTarget = false;
        }

        private void PlaceGhost()
        {
            if (_ghost == null) return;
            var rt = (RectTransform)_ghost.transform;
            rt.anchoredPosition = new Vector2(_space.ToX(_ghostBeat), TrackRowY(_ghostTrack));
            rt.sizeDelta = new Vector2(_space.SpanToWidth(_ghostBeats), TrackHeight);
            _ghost.SetPitch(_space.PixelsPerBeat / RhythmPattern.HexPerBeat);
        }

        /// <summary>落点是否被别的 UI（比如右侧角色栏）盖住。由接线层设置，板子本身不依赖侧栏。</summary>
        public System.Func<Vector2, bool> IsDropBlocked;

        /// <summary>
        /// 接线：拖动中实时刷新落点虚影，松手落位并收起虚影。
        /// 被侧栏挡住的位置不显示虚影、也不落位（卡片是拖回侧栏的，不该掉到板上）。
        /// </summary>
        public void WireCard(DrumCharacterCard card)
        {
            if (card == null) return;
            card.OnDragMove = (c, pos) => ShowDropPreview(c.Character, pos);
            card.OnDragEnd = (c, pos) =>
            {
                HideDropPreview();
                return TryPlaceFromCard(c, pos);
            };
        }

        /// <summary>打开某已放鼓的细节预览（加点/洗点）。</summary>
        public void OpenDetail(PlacedDrum pd)
        {
            if (_canvas == null || pd == null) return;
            if (pd.Locked || pd.Character.FixedRhythm)
            {
                OnPreviewCharacter?.Invoke(pd.Character);
                return;
            }
            if (EditingLocked) return;
            DrumDetailPanel.Show(pd.Character, _canvas.transform);
        }

        /// <summary>移动校验 + 重定位；失败返回 false（保持原位）。</summary>
        public bool TryMove(PlacedDrum pd, float newBeat, int newTrack)
        {
            if (pd == null || pd.Locked || !_placed.Contains(pd)) return false;
            if (!CanPlace(pd.Character, newBeat, newTrack, pd)) return false;
            if (pd.StartBeat == newBeat && pd.LaneIndex == newTrack) return true;
            _undo.Push(new UndoAction { Type = UndoType.Move, Drum = pd,
                                        OldBeat = pd.StartBeat, OldTrack = pd.LaneIndex });
            MoveTo(pd, newBeat, newTrack);
            return true;
        }

        /// <summary>放置（校验通过才生效，记录撤销），返回新放置的鼓或 null。</summary>
        public PlacedDrum Place(DrumCharacter character, float startBeat, int track)
        {
            if (!CanPlace(character, startBeat, track)) return null;
            var pd = PlaceInternal(character, startBeat, track);
            _undo.Push(new UndoAction { Type = UndoType.Place, Drum = pd });
            return pd;
        }

        internal PlacedDrum PlacePreset(DrumCharacter character, RehearsalChallenge.Preset preset)
        {
            if (!preset.Locked || character == null || !character.FixedRhythm ||
                !IsSpaceAvailable(character, preset.StartBeat, preset.LaneIndex, null)) return null;
            return PlaceInternal(character, preset.StartBeat, preset.LaneIndex, locked: true);
        }

        internal PlacedDrum RestoreEditable(DrumCharacter character, float beat, int laneIndex)
        {
            return CanPlace(character, beat, laneIndex) ? PlaceInternal(character, beat, laneIndex) : null;
        }

        internal void RestoreConnection(PlacedDrum current)
        {
            foreach (var next in _placed)
            {
                if (!CanConnect(current, next)) continue;
                current.ConnectedNeighbor = next;
                RefreshConnections();
                return;
            }
        }

        private PlacedDrum PlaceInternal(DrumCharacter character, float startBeat, int track, bool locked = false)
        {
            var pd = CreatePlaced(character, startBeat, track, locked);
            _placed.Add(pd);
            RefreshConnections();
            return pd;
        }

        /// <summary>移动（裸重定位，重叠校验由交互层在移动前判断）。</summary>
        private void MoveTo(PlacedDrum pd, float newBeat, int newTrack)
        {
            pd.StartBeat = newBeat;
            pd.LaneIndex = newTrack;
            LayoutPlaced(pd);
            RefreshConnections();
        }

        private void LayoutPlaced(PlacedDrum drum)
        {
            drum.Rect.anchoredPosition = new Vector2(_space.ToX(drum.StartBeat), TrackRowY(drum.LaneIndex));
            drum.Rect.sizeDelta = new Vector2(_space.SpanToWidth(drum.DurationBeats), TrackHeight);
            drum.Waveform.SetPitch(_space.PixelsPerBeat / RhythmPattern.HexPerBeat);
        }

        public void Remove(PlacedDrum pd)
        {
            if (EditingLocked || pd == null || pd.Locked || !_placed.Remove(pd)) return;
            _undo.Push(new UndoAction { Type = UndoType.Remove, Drum = pd });
            pd.ConnectedNeighbor = null;
            pd.Rect.gameObject.SetActive(false);
            RefreshConnections();
        }

        /// <summary>刷新相邻鼓的「尾巴待连接」状态：某鼓后面紧跟了不同角色 → 其尾巴画暗（未连接）；没接/接自己 → 原样。</summary>
        private void RefreshConnections()
        {
            // 连接/融合只发生在同一条轨内：按轨分组，再在组内按拍排序
            for (int t = 0; t < TrackCount; t++)
            {
                var sorted = new List<PlacedDrum>();
                foreach (var p in _placed)
                    if (p.LaneIndex == t) sorted.Add(p);
                sorted.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));

                for (int i = 0; i < sorted.Count; i++)
                {
                    var cur = sorted[i];
                    PlacedDrum neighbor = null;
                    List<HexCoord> bridge = null;
                    Color fuseColor = Color.white; // 融合色（前后角色颜色混合），已连接时给尾巴/桥用
                    if (i + 1 < sorted.Count)
                    {
                        var next = sorted[i + 1];
                        if (CanConnect(cur, next)) neighbor = next;
                        fuseColor = Color.Lerp(cur.Waveform.color, next.Waveform.color, 0.5f);
                        if (neighbor != null)
                        {
                            // 端点错位（纵向高度不同）→ 在接缝处补一段垂直的桥，把前后连上
                            int la = cur.Character.Level;
                            int lb = next.Character.Level;
                            int d = lb - la;
                            if (Mathf.Abs(d) >= 2)
                            {
                                int seamQ = Mathf.RoundToInt(RhythmPattern.HexPerBeat * cur.DurationBeats);
                                int step = d > 0 ? 1 : -1;
                                bridge = new List<HexCoord>();
                                for (int r = la + step; r != lb; r += step)
                                    bridge.Add(new HexCoord(seamQ, r));
                            }
                        }
                    }

                    bool pending = neighbor != null;
                    if (cur.ConnectedNeighbor != neighbor) cur.ConnectedNeighbor = null;
                    cur.Waveform.SetTailPending(pending && !cur.ConnectedToNext);
                    cur.Waveform.SetBridge(bridge, cur.ConnectedToNext);
                    cur.Waveform.SetTailFused(pending && cur.ConnectedToNext, fuseColor); // 已连接 → 尾巴/桥融合色

                    if (pending)
                    {
                        if (cur.SeamNode == null) cur.SeamNode = CreateSeamNode(cur);
                        var target = neighbor;
                        var button = cur.SeamNode.GetComponent<Button>();
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => ToggleConnection(cur, target));
                        var img = cur.SeamNode.GetComponent<Image>();
                        if (img != null) img.color = cur.ConnectedToNext
                            ? new Color(0.46f, 0.86f, 0.56f, 0.95f)   // 绿：已连接
                            : new Color(1f, 0.82f, 0.35f, 0.95f);      // 金黄：待连接
                    }
                    else if (cur.SeamNode != null)
                    {
                        cur.SeamNode.SetActive(false);
                        Object.Destroy(cur.SeamNode);
                        cur.SeamNode = null;
                    }
                }
            }
        }

        /// <summary>在 cur 的右缘创建一个可点的「连接点」，点击切换与后一个鼓的连接标记。</summary>
        private GameObject CreateSeamNode(PlacedDrum cur)
        {
            var go = new GameObject("Seam", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(cur.Rect, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(22f, 22f);
            rt.anchoredPosition = new Vector2(-12f, 0f); // 落在 cur 右缘内侧，避免被后一个鼓的 blob 盖住

            var img = go.GetComponent<Image>();
            UIStyle.ApplyFrame(img, new Color(1f, 0.82f, 0.35f, 0.95f)); // 金黄：待连接

            // 中间放个「+」字形，表明这是可点的连接点；状态（待连接/已连接）由颜色表达
            var glyph = PixelText.Create("Glyph", go.transform, "+", 1.2f,
                                         UIStyle.Ink, PixelText.Align.Center);
            glyph.raycastTarget = false; // 让点击落在按钮上，不被文字挡住

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            return go;
        }

        /// <summary>只有同轨、紧邻且不同角色的片段可连接。</summary>
        internal static bool CanConnect(PlacedDrum current, PlacedDrum next)
        {
            return !current.Locked && !next.Locked && current.LaneIndex == next.LaneIndex
                && Mathf.Abs(next.StartBeat - (current.StartBeat + current.DurationBeats)) < 1e-3f
                && next.Character.Name != current.Character.Name;
        }

        private void ToggleConnection(PlacedDrum cur, PlacedDrum next)
        {
            if (EditingLocked) return;
            cur.ConnectedNeighbor = cur.ConnectedToNext ? null : next;
            RefreshConnections();
        }

        public void Clear()
        {
            if (EditingLocked) return;
            HideDropPreview();
            for (int i = _placed.Count - 1; i >= 0; i--)
            {
                var p = _placed[i];
                if (p.Locked) continue;
                p.Rect.gameObject.SetActive(false);
                Object.Destroy(p.Rect.gameObject);
                _placed.RemoveAt(i);
            }
            foreach (var action in _undo)
                if (action.Type == UndoType.Remove) Object.Destroy(action.Drum.Rect.gameObject);
            _undo.Clear();
            RefreshConnections();
        }

        /// <summary>撤销上一步（放置 / 移动 / 收回）。</summary>
        public void Undo()
        {
            if (EditingLocked || _undo.Count == 0) return;
            var a = _undo.Pop();
            switch (a.Type)
            {
                case UndoType.Place:
                    _placed.Remove(a.Drum);
                    a.Drum.Rect.gameObject.SetActive(false);
                    Object.Destroy(a.Drum.Rect.gameObject);
                    RefreshConnections();
                    break;
                case UndoType.Remove:
                    _placed.Add(a.Drum);
                    LayoutPlaced(a.Drum);
                    a.Drum.Rect.gameObject.SetActive(true);
                    RefreshConnections();
                    break;
                case UndoType.Move: MoveTo(a.Drum, a.OldBeat, a.OldTrack); break;
            }
        }

        /// <summary>导出当前编排为存档数据。</summary>
        public BoardSaveData ToSaveData()
        {
            var data = new BoardSaveData();
            data.trackLengthBeats = (int)TrackLengthBeats;
            foreach (var p in _placed)
            {
                var points = System.Array.ConvertAll(p.Character.PlaybackPoints,
                    point => new SavedRhythmPoint { beat = point.Beat, level = point.Level, accent = point.Accent });
                data.drums.Add(new PlacedDrumEntry
                {
                    waveformType = p.Character.Waveform.Type.ToString(),
                    startBeat = p.StartBeat,
                    LaneIndex = p.LaneIndex,
                    durationBeats = p.DurationBeats,
                    track = TrackCount - 1 - p.LaneIndex, // 旧字段使用从下向上的编号
                    characterName = p.Character.Name,
                    sampleName = p.Character.Sample.FileName,
                    category = p.Character.Category,
                    locked = p.Locked,
                    connectedToNext = p.ConnectedToNext,
                    rhythmPoints = points
                });
            }
            return data;
        }

        private enum UndoType { Place, Move, Remove }

        private struct UndoAction
        {
            public UndoType Type;
            public PlacedDrum Drum;
            public float OldBeat;           // Move 旧拍
            public int OldTrack;            // Move 旧轨
        }

        private PlacedDrum CreatePlaced(DrumCharacter character, float startBeat, int track, bool locked)
        {
            // 程序层面是一「方块」（rect 占 [startBeat, startBeat+时值)），玩家看到的只是这个方块里的一坨六边形 blob，无外框
            var go = new GameObject("Drum_" + character.Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(HexWaveformRenderer));
            go.transform.SetParent(_drumRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);

            var wr = go.GetComponent<HexWaveformRenderer>();
            wr.SetPattern(character.EffectivePattern, RhythmPattern.StableHash(character.Name));
            wr.SetVerticalLimit(TrackHeight - 8f);
            wr.SetBaselineAnchored(true); // 锚定 r=0 到轨心，让不同角色的纵向高度差异（端点能否接上）可见
            wr.color = Color.white;                       // 颜色由音色大类贴图提供，不再叠角色专属色
            wr.SetCategory(character.Category);           // 六边形外观 = 音色大类贴图（同色系边缘设计）
            wr.raycastTarget = true;                     // 可点击 → 双击打开细节预览（加点/洗点）

            if (locked)
            {
                var badge = UIFactory.CreateText("LockedBadge", go.transform, "锁定 · " + character.Waveform.Name,
                    15, UIStyle.TextDim, TextAnchor.MiddleCenter);
                badge.raycastTarget = false;
                badge.rectTransform.anchorMin = new Vector2(0f, 1f);
                badge.rectTransform.anchorMax = new Vector2(1f, 1f);
                badge.rectTransform.pivot = new Vector2(0.5f, 1f);
                badge.rectTransform.sizeDelta = new Vector2(0f, 20f);
                badge.rectTransform.anchoredPosition = Vector2.zero;
            }

            var pd = new PlacedDrum(character, startBeat, track, character.Pattern.TotalBeats, rt, wr, locked);
            LayoutPlaced(pd);
            PlacedDrumHandle.Attach(go, this, pd);
            return pd;
        }

    }
}
