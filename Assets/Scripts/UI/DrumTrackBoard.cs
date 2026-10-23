using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>板上一个已放置的鼓（角色 + 起始拍 + 视觉）。</summary>
    public sealed class PlacedDrum
    {
        public readonly DrumCharacter Character;
        public float StartBeat;
        public readonly float DurationBeats; // 时值（拍）：该角色节奏型占的跨度
        public readonly RectTransform Rect;
        public readonly HexWaveformRenderer Waveform; // 视觉 = 六边形 blob（无框）
        public bool ConnectedToNext;                  // 是否与紧随其后的鼓「已连接」（接缝标记）
        public GameObject SeamNode;                   // 接缝处的连接点按钮（待连接时显示）

        public PlacedDrum(DrumCharacter character, float startBeat, float durationBeats, RectTransform rect, HexWaveformRenderer waveform)
        {
            Character = character;
            StartBeat = startBeat;
            DurationBeats = durationBeats;
            Rect = rect;
            Waveform = waveform;
        }
    }

    /// <summary>
    /// 鼓轨编排板：单轨时间轴，负责落位、渲染、放置校验（同轨不重叠）。
    /// 交互：卡片拖放落位、已放鼓左键拖动移动 / 右键收回、滚轮缩放（以鼠标为锚）、
    /// 空白处左键拖动或 A/D 平移、按钮回起点。
    /// </summary>
    public sealed class DrumTrackBoard : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler
    {
        public const float LeftMargin = 80f;
        public const float DefaultPixelsPerBeat = 120f; // 1 拍 = 120px（16 拍 ≈ 一屏），板子只做整体效果演示，微调进细节面板
        public const float SnapBeat = 1f;               // 吸附到整格（整数拍），不再 0.5 拍
        public const float TrackLengthBeats = 16f;
        public const float TrackHeight = 160f;

        public const float MinPixelsPerBeat = 20f;
        public const float MaxPixelsPerBeat = 600f;
        private const float ZoomStep = 1.2f;
        private const float PanSpeed = 600f; // 像素/秒
        private const float SlotGap = 6f;    // 格槽间距

        // 程序化木纹：按 1920×1080 参考分辨率铺板缝（和 RosterSidebar 的参考高一致）
        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
        private const float PlankHeight = 96f;
        private const float PlankWidth = 384f;

        private BeatSpace _space;
        private readonly List<PlacedDrum> _placed = new List<PlacedDrum>();
        private readonly List<RectTransform> _beatLines = new List<RectTransform>();
        private readonly List<float> _beatLineBeats = new List<float>();
        private readonly List<RectTransform> _slots = new List<RectTransform>(); // 格槽（缩放时随格宽变化）
        private readonly List<RectTransform> _sectionLabels = new List<RectTransform>();
        private readonly List<float> _sectionCenters = new List<float>();
        private readonly List<float> _sectionSpans = new List<float>();
        private Transform _drumRoot;
        private Canvas _canvas;
        private float _panLastX;
        private readonly Stack<UndoAction> _undo = new Stack<UndoAction>();

        public IReadOnlyList<PlacedDrum> Placed { get { return _placed; } }
        public RectTransform Rect { get { return (RectTransform)transform; } }

        public static DrumTrackBoard Create(Transform parent)
        {
            var go = new GameObject("DrumTrackBoard", typeof(RectTransform), typeof(Image), typeof(DrumTrackBoard));
            go.transform.SetParent(parent, false);
            var board = go.GetComponent<DrumTrackBoard>();
            board.Build();
            return board;
        }

        private void Build()
        {
            _canvas = GetComponentInParent<Canvas>();

            Rect.anchorMin = Vector2.zero;
            Rect.anchorMax = Vector2.one;
            Rect.offsetMin = Vector2.zero;
            Rect.offsetMax = Vector2.zero;
            Rect.pivot = new Vector2(0f, 0.5f);

            var bg = GetComponent<Image>();
            UIResource.ApplySpriteOrColor(bg, "UI/baseplate", UIStyle.Stage);
            DrawPlanks();

            _space = new BeatSpace(DefaultPixelsPerBeat, LeftMargin);

            var beatRoot = new GameObject("BeatLines", typeof(RectTransform)).transform;
            beatRoot.SetParent(transform, false);
            var br = (RectTransform)beatRoot;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            DrawTrack(beatRoot);

            _drumRoot = new GameObject("Drums", typeof(RectTransform)).transform;
            _drumRoot.SetParent(transform, false);
            var dr = (RectTransform)_drumRoot;
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;

            AddResetButton();
        }

        private void DrawTrack(Transform root)
        {
            CreateSectionBackground(
            root,
            "OpeningArea",
                0f,
                4f,
                new Color(1f, 1f, 1f, 0.025f)
            );

            CreateSectionBackground(
                root,
                "DevelopmentArea",
                4f,
                8f,
                new Color(1f, 1f, 1f, 0.045f)
            );

            CreateSectionBackground(
                root,
                "ClimaxArea",
                8f,
                16f,
                new Color(1f, 1f, 1f, 0.025f)
            );
            // 水平轨线
            var lane = UIFactory.CreatePanel("LaneLine", root, new Color(1f, 1f, 1f, 0.08f));
            lane.raycastTarget = false;
            var laneRt = lane.rectTransform;
            laneRt.anchorMin = new Vector2(0f, 0.5f);
            laneRt.anchorMax = new Vector2(1f, 0.5f);
            laneRt.offsetMin = new Vector2(0f, -1f);
            laneRt.offsetMax = new Vector2(0f, 1f);

            // 格槽：16 个凹槽，交替底色，做出「刻进木头里的槽」的观感
            for (int b = 0; b < (int)TrackLengthBeats; b++)
            {
                var slotColor = b % 2 == 0 ? UIStyle.Cell : UIStyle.CellAlt;
                var slot = UIFactory.CreatePanel("Slot_" + b, root, slotColor);
                slot.raycastTarget = false;
                UIResource.ApplyWellOrColor(slot, "UI/cell_slot", slotColor);
                var srt = slot.rectTransform;
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 0.5f);
                srt.anchoredPosition = new Vector2(_space.ToX(b) + SlotGap * 0.5f, 0f);
                srt.sizeDelta = new Vector2(_space.SpanToWidth(1f) - SlotGap, TrackHeight - 8f);
                _slots.Add(srt);
            }

            // 拍线（每 4 拍加粗提亮为小节线）
            for (int b = 0; b <= (int)TrackLengthBeats; b++)
            {
                bool bar = b % 4 == 0;
                var line = UIFactory.CreatePanel("Beat_" + b, root,
                    bar ? new Color(0.95f, 0.83f, 0.62f, 0.34f) : new Color(1f, 0.94f, 0.84f, 0.10f));
                line.raycastTarget = false;
                var rt = line.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(_space.ToX(b), 0f);
                rt.sizeDelta = new Vector2(bar ? 2f : 1f, TrackHeight + 40f);
                _beatLines.Add(rt);
                _beatLineBeats.Add(b);
            }
            // 音轨区域提示
            CreateSectionLabel(root, "OpeningLabel", "开场", 0f, 4f);
            CreateSectionLabel(root, "DevelopmentLabel", "发展", 4f, 8f);
            CreateSectionLabel(root, "ClimaxLabel", "高潮", 8f, 16f);

            // 时间方向提示
            var timeHint = PixelText.Create(
                "TimeHint",
                root,
                "音乐从左向右播放 →",
                1.4f,
                UIStyle.Text,
                PixelText.Align.Center
            );

            timeHint.raycastTarget = false;

            var timeRt = (RectTransform)timeHint.transform;
            timeRt.anchorMin = timeRt.anchorMax = timeRt.pivot =
                new Vector2(0.5f, 0.5f);

            timeRt.anchoredPosition =
                new Vector2(0f, TrackHeight * 0.5f + 75f);
        }
        private void CreateSectionLabel(
    Transform root,
    string name,
    string text,
    float startBeat,
    float endBeat)
        {
            var label = PixelText.Create(
                name,
                root,
                text,
                1.7f,
                UIStyle.Text,
                PixelText.Align.Center
            );

            label.raycastTarget = false;

            var rt = (RectTransform)label.transform;

            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0f, 0.5f);

            float centerBeat = (startBeat + endBeat) * 0.5f;
            float span = endBeat - startBeat;

            rt.anchoredPosition = new Vector2(
                _space.ToX(centerBeat),
                TrackHeight * 0.5f + 30f
            );

            rt.sizeDelta = new Vector2(
                _space.SpanToWidth(span),
                label.PreferredSize.y
            );

            _sectionLabels.Add(rt);
            _sectionCenters.Add(centerBeat);
            _sectionSpans.Add(span);
        }

        /// <summary>
        /// 程序化木纹底：横向板缝 + 隔一段一条竖缝。
        /// 压在底板（UI/baseplate 或 UIStyle.Stage）之上、格槽之下，
        /// 让面板看起来是张木工台而不是一块纯色布。
        /// </summary>

        private void CreateSectionBackground(
    Transform root,
    string name,
    float startBeat,
    float endBeat,
    Color color)
        {
            var panel = UIFactory.CreatePanel(name, root, color);
            panel.raycastTarget = false;

            var rt = panel.rectTransform;

            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0f, 0.5f);

            float centerBeat = (startBeat + endBeat) * 0.5f;
            float span = endBeat - startBeat;

            rt.anchoredPosition = new Vector2(
                _space.ToX(centerBeat),
                0f
            );

            rt.sizeDelta = new Vector2(
                _space.SpanToWidth(span),
                TrackHeight + 20f
            );

            rt.SetAsFirstSibling();
        }


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
                _space.Pan(h * PanSpeed * Time.deltaTime);
                ApplyViewTransform();
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
            _panLastX = LocalX(e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            float x = LocalX(e.position);
            if (float.IsNaN(x) || float.IsNaN(_panLastX)) { _panLastX = x; return; }
            float dx = x - _panLastX;
            _panLastX = x;
            if (Mathf.Abs(dx) > 0.001f)
            {
                _space.Pan(dx);
                ApplyViewTransform();
            }
        }

        private void ZoomAt(float factor, float anchorBeat)
        {
            float newPpb = Mathf.Clamp(_space.PixelsPerBeat * factor, MinPixelsPerBeat, MaxPixelsPerBeat);
            float eff = newPpb / _space.PixelsPerBeat;
            _space.Zoom(eff, anchorBeat);
            ApplyViewTransform();
        }

        private void ResetView()
        {
            _space = new BeatSpace(DefaultPixelsPerBeat, LeftMargin);
            ApplyViewTransform();
        }

        private float LocalX(Vector2 screenPos)
        {
            Vector2 local;
            if (_canvas != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPos, _canvas.worldCamera, out local))
                return local.x;
            return float.NaN;
        }

        /// <summary>平移 / 缩放后重排拍线与已放鼓。</summary>
        private void ApplyViewTransform()
        {
            for (int i = 0; i < _beatLines.Count; i++)
            {
                var rt = _beatLines[i];
                rt.anchoredPosition = new Vector2(_space.ToX(_beatLineBeats[i]), rt.anchoredPosition.y);
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                var rt = _slots[i];
                rt.anchoredPosition = new Vector2(_space.ToX(i) + SlotGap * 0.5f, rt.anchoredPosition.y);
                rt.sizeDelta = new Vector2(_space.SpanToWidth(1f) - SlotGap, rt.sizeDelta.y);
            }
            for (int i = 0; i < _sectionLabels.Count; i++)
            {
                var rt = _sectionLabels[i];

                rt.anchoredPosition = new Vector2(
                    _space.ToX(_sectionCenters[i]),
                    rt.anchoredPosition.y
                );

                rt.sizeDelta = new Vector2(
                    _space.SpanToWidth(_sectionSpans[i]),
                    rt.sizeDelta.y
                );
            }
            foreach (var p in _placed)
            {
                p.Rect.anchoredPosition = new Vector2(_space.ToX(p.StartBeat), p.Rect.anchoredPosition.y);
                p.Rect.sizeDelta = new Vector2(_space.SpanToWidth(p.DurationBeats), p.Rect.sizeDelta.y);
                p.Waveform.SetPitch(_space.PixelsPerBeat / RhythmPattern.HexPerBeat);
            }
            // 虚影也跟着平移/缩放走，否则拖拽中滚轮一缩放它就飘了
            if (_ghost != null && _ghost.gameObject.activeSelf) PlaceGhost();
        }

        // ---------- 落位 / 校验 ----------

        /// <summary>落位到最近 0.5 拍。</summary>
        public static float Snap(float beat) { return Mathf.Round(beat / SnapBeat) * SnapBeat; }

        /// <summary>屏幕坐标 → 拍（含吸附）。</summary>
        public float ScreenToBeat(Vector2 screenPos)
        {
            float localX = LocalX(screenPos);
            if (float.IsNaN(localX)) return 0f;
            return Snap(_space.ToBeat(localX));
        }

        /// <summary>能否在该拍放置（同轨不重叠）。</summary>
        public bool CanPlace(DrumCharacter character, float startBeat)
        {
            return CanPlace(character, startBeat, null);
        }

        /// <summary>同上，但移动时可把正在移动的鼓排除在重叠判定外。</summary>
        public bool CanPlace(DrumCharacter character, float startBeat, PlacedDrum except)
        {
            // 按拍区间判重叠：角色占 [startBeat, startBeat+时值) 一段，区间相交即冲突
            float dur = character.Pattern.TotalBeats;
            foreach (var p in _placed)
            {
                if (p == except) continue;
                if (startBeat < p.StartBeat + p.DurationBeats && startBeat + dur > p.StartBeat) return false;
            }
            return true;
        }

        /// <summary>落位格是否落在轨道范围内（一格一角色，与时长无关）。</summary>
        public bool IsValidBeatRange(float startBeat, float duration)
        {
            return startBeat >= 0f && startBeat + duration <= TrackLengthBeats;
        }

        /// <summary>
        /// 解算一次落点：屏幕坐标 → 吸附后的拍，以及「能不能放」。
        /// 落点预览和真正落位都走这一份逻辑，从根上杜绝「预览显示绿了却放不下去」。
        /// </summary>
        private bool ResolveDrop(DrumCharacter character, Vector2 screenPos,
                                 out float beat, out bool inLane)
        {
            beat = 0f;
            inLane = false;
            if (_canvas == null || character == null) return false;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    Rect, screenPos, _canvas.worldCamera, out local)) return false;

            inLane = Mathf.Abs(local.y) <= TrackHeight * 0.5f + 20f;   // 需落在轨道竖向范围内
            beat = Snap(_space.ToBeat(local.x));
            return inLane
                && IsValidBeatRange(beat, character.Pattern.TotalBeats)
                && CanPlace(character, beat);
        }

        /// <summary>卡片拖放落点 → 放置；成功返回 true（卡片回侧栏可复用）。</summary>
        public bool TryPlaceFromCard(DrumCharacterCard card, Vector2 screenPos)
        {
            if (card == null) return false;
            float beat;
            bool inLane;
            if (!ResolveDrop(card.Character, screenPos, out beat, out inLane)) return false;
            return Place(card.Character, beat) != null;
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
        private float _ghostBeats = 1f;

        /// <summary>拖拽中每帧刷新落点虚影：绿=能放，红=不能放（重叠 / 越界 / 超出 16 拍）。</summary>
        public void ShowDropPreview(DrumCharacterCard card, Vector2 screenPos)
        {
            if (card == null || card.Character == null) { HideDropPreview(); return; }

            var c = card.Character;

            // 拖到侧栏上方：不显示虚影（这块位置本来也不允许落位）
            if (IsDropBlocked != null && IsDropBlocked(screenPos)) { SetGhostVisible(false); return; }

            float beat;
            bool inLane;
            bool ok = ResolveDrop(c, screenPos, out beat, out inLane);

            // 拖回侧栏（不在轨道竖向范围内）时干脆不显示，免得跟一个红块乱跑
            if (!inLane) { SetGhostVisible(false); return; }

            EnsureGhost(c);
            SetGhostVisible(true);

            _ghostBeat = beat;
            _ghostBeats = c.Pattern.TotalBeats;
            _ghost.color = ok ? GhostOk : GhostBad;
            PlaceGhost();
            if (_ghostLabel != null) _ghostLabel.Text = "拍 " + Mathf.RoundToInt(beat);
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
            _ghost.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.SetAsLastSibling();      // 永远压在已放鼓上面，看得清

            _ghostLabel = UIFactory.PlacePixelText("Beat", go.transform, "拍 0", 1.5f,
                                                   UIStyle.Text, PixelText.Align.Center,
                                                   new Vector2(0f, 1f), new Vector2(8f, 6f));
            _ghostLabel.raycastTarget = false;
        }

        private void PlaceGhost()
        {
            if (_ghost == null) return;
            var rt = (RectTransform)_ghost.transform;
            rt.anchoredPosition = new Vector2(_space.ToX(_ghostBeat), 0f);
            rt.sizeDelta = new Vector2(_space.SpanToWidth(_ghostBeats), TrackHeight);
            _ghost.SetPitch(_space.PixelsPerBeat / RhythmPattern.HexPerBeat);
        }

        /// <summary>把角色卡拖放接到本板（供启动/接线层调用）。</summary>
        public void AttachCard(DrumCharacterCard card)
        {
            card.OnDragEnd = (c, pos) => TryPlaceFromCard(c, pos);
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
            card.OnDragMove = (c, pos) => ShowDropPreview(c, pos);
            card.OnDragEnd = (c, pos) =>
            {
                HideDropPreview();
                if (IsDropBlocked != null && IsDropBlocked(pos)) return false;
                return TryPlaceFromCard(c, pos);
            };
        }

        /// <summary>打开某已放鼓的细节预览（加点/洗点）。</summary>
        public void OpenDetail(PlacedDrum pd)
        {
            if (_canvas == null || pd == null) return;
            DrumDetailPanel.Show(pd.Character, _canvas.transform);
        }

        /// <summary>移动校验 + 重定位；失败返回 false（保持原位）。</summary>
        public bool TryMove(PlacedDrum pd, float newBeat)
        {
            if (!IsValidBeatRange(newBeat, pd.DurationBeats)) return false;
            if (!CanPlace(pd.Character, newBeat, pd)) return false;
            _undo.Push(new UndoAction { Type = UndoType.Move, Drum = pd, OldBeat = pd.StartBeat });
            MoveTo(pd, newBeat);
            return true;
        }

        /// <summary>放置（校验通过才生效，记录撤销），返回新放置的鼓或 null。</summary>
        public PlacedDrum Place(DrumCharacter character, float startBeat)
        {
            if (!CanPlace(character, startBeat)) return null;
            var pd = PlaceInternal(character, startBeat);
            _undo.Push(new UndoAction { Type = UndoType.Place, Drum = pd });
            return pd;
        }

        private PlacedDrum PlaceInternal(DrumCharacter character, float startBeat)
        {
            var pd = CreatePlaced(character, startBeat);
            _placed.Add(pd);
            RefreshConnections();
            return pd;
        }

        /// <summary>移动（裸重定位，重叠校验由交互层在移动前判断）。</summary>
        public void MoveTo(PlacedDrum pd, float newBeat)
        {
            pd.StartBeat = newBeat;
            pd.Rect.anchoredPosition = new Vector2(_space.ToX(newBeat), pd.Rect.anchoredPosition.y);
            RefreshConnections();
        }

        public void Remove(PlacedDrum pd)
        {
            _undo.Push(new UndoAction { Type = UndoType.Remove, Character = pd.Character, OldBeat = pd.StartBeat });
            RemoveInternal(pd);
        }

        private void RemoveInternal(PlacedDrum pd)
        {
            if (_placed.Remove(pd)) Object.Destroy(pd.Rect.gameObject);
            RefreshConnections();
        }

        /// <summary>刷新相邻鼓的「尾巴待连接」状态：某鼓后面紧跟了不同角色 → 其尾巴画暗（未连接）；没接/接自己 → 原样。</summary>
        private void RefreshConnections()
        {
            var sorted = new List<PlacedDrum>(_placed);
            sorted.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));
            for (int i = 0; i < sorted.Count; i++)
            {
                var cur = sorted[i];
                bool pending = false;
                List<HexCoord> bridge = null;
                Color fuseColor = Color.white; // 融合色（前后角色颜色混合），已连接时给尾巴/桥用
                if (i + 1 < sorted.Count)
                {
                    var next = sorted[i + 1];
                    bool contiguous = Mathf.Abs(next.StartBeat - (cur.StartBeat + cur.DurationBeats)) < 1e-3f;
                    bool different = next.Character.Name != cur.Character.Name;
                    pending = contiguous && different;
                    fuseColor = Color.Lerp(cur.Waveform.color, next.Waveform.color, 0.5f);
                    if (pending)
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

                if (!pending) cur.ConnectedToNext = false; // 不再接别人，连接标记作废
                cur.Waveform.SetTailPending(pending && !cur.ConnectedToNext);
                cur.Waveform.SetBridge(bridge, cur.ConnectedToNext);
                cur.Waveform.SetTailFused(pending && cur.ConnectedToNext, fuseColor); // 已连接 → 尾巴/桥融合色

                if (pending)
                {
                    if (cur.SeamNode == null) cur.SeamNode = CreateSeamNode(cur);
                    var img = cur.SeamNode.GetComponent<Image>();
                    if (img != null) img.color = cur.ConnectedToNext
                        ? new Color(0.46f, 0.86f, 0.56f, 0.95f)   // 绿：已连接
                        : new Color(1f, 0.82f, 0.35f, 0.95f);      // 金黄：待连接
                }
                else if (cur.SeamNode != null)
                {
                    Object.Destroy(cur.SeamNode);
                    cur.SeamNode = null;
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
            btn.onClick.AddListener(() => ToggleConnection(cur));
            return go;
        }

        /// <summary>切换 cur 与后一个鼓的连接标记（仅状态，不改加点/发声）。</summary>
        private void ToggleConnection(PlacedDrum cur)
        {
            cur.ConnectedToNext = !cur.ConnectedToNext;
            RefreshConnections();
        }

        public void Clear()
        {
            foreach (var p in _placed) Object.Destroy(p.Rect.gameObject);
            _placed.Clear();
            _undo.Clear();
        }

        /// <summary>撤销上一步（放置 / 移动 / 收回）。</summary>
        public void Undo()
        {
            if (_undo.Count == 0) return;
            var a = _undo.Pop();
            switch (a.Type)
            {
                case UndoType.Place: RemoveInternal(a.Drum); break;
                case UndoType.Remove: PlaceInternal(a.Character, a.OldBeat); break;
                case UndoType.Move: MoveTo(a.Drum, a.OldBeat); break;
            }
        }

        /// <summary>导出当前编排为存档数据。</summary>
        public BoardSaveData ToSaveData()
        {
            var data = new BoardSaveData();
            data.trackLengthBeats = (int)TrackLengthBeats;
            foreach (var p in _placed)
                data.drums.Add(new PlacedDrumEntry { waveformType = p.Character.Waveform.Type.ToString(), startBeat = p.StartBeat });
            return data;
        }

        private enum UndoType { Place, Move, Remove }

        private struct UndoAction
        {
            public UndoType Type;
            public PlacedDrum Drum;         // Place / Move
            public DrumCharacter Character; // Remove
            public float OldBeat;           // Move 旧拍 / Remove 原位
        }

        private PlacedDrum CreatePlaced(DrumCharacter character, float startBeat)
        {
            // 程序层面是一「方块」（rect 占 [startBeat, startBeat+时值)），玩家看到的只是这个方块里的一坨六边形 blob，无外框
            var go = new GameObject("Drum_" + character.Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(HexWaveformRenderer));
            go.transform.SetParent(_drumRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(_space.ToX(startBeat), 0f);
            rt.sizeDelta = new Vector2(_space.SpanToWidth(character.Pattern.TotalBeats), TrackHeight);

            var wr = go.GetComponent<HexWaveformRenderer>();
            wr.SetPattern(character.EffectivePattern, RhythmPattern.StableHash(character.Name));
            wr.SetPitch(_space.PixelsPerBeat / RhythmPattern.HexPerBeat); // 1 拍 = 8 格，格子随轨距缩放
            wr.SetBaselineAnchored(true); // 锚定 r=0 到轨心，让不同角色的纵向高度差异（端点能否接上）可见
            wr.color = Color.white;                       // 颜色由音色大类贴图提供，不再叠角色专属色
            wr.SetCategory(character.Category);           // 六边形外观 = 音色大类贴图（同色系边缘设计）
            wr.raycastTarget = true;                     // 可点击 → 双击打开细节预览（加点/洗点）

            var pd = new PlacedDrum(character, startBeat, character.Pattern.TotalBeats, rt, wr);
            PlacedDrumHandle.Attach(go, this, pd);
            return pd;
        }

    }
}
