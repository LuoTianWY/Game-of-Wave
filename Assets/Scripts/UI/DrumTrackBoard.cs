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

        private BeatSpace _space;
        private readonly List<PlacedDrum> _placed = new List<PlacedDrum>();
        private readonly List<RectTransform> _beatLines = new List<RectTransform>();
        private readonly List<float> _beatLineBeats = new List<float>();
        private readonly List<RectTransform> _slots = new List<RectTransform>(); // 格槽（缩放时随格宽变化）
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
            // 水平轨线
            var lane = UIFactory.CreatePanel("LaneLine", root, new Color(1f, 1f, 1f, 0.08f));
            lane.raycastTarget = false;
            var laneRt = lane.rectTransform;
            laneRt.anchorMin = new Vector2(0f, 0.5f);
            laneRt.anchorMax = new Vector2(1f, 0.5f);
            laneRt.offsetMin = new Vector2(0f, -1f);
            laneRt.offsetMax = new Vector2(0f, 1f);

            // 格槽：16 个圆角卡槽，交替底色，让每个可放置格一目了然
            for (int b = 0; b < (int)TrackLengthBeats; b++)
            {
                var slotColor = b % 2 == 0 ? UIStyle.Cell : new Color(0.17f, 0.22f, 0.34f, 1f);
                var slot = UIFactory.CreatePanel("Slot_" + b, root, slotColor);
                slot.raycastTarget = false;
                UIStyle.ApplyRound(slot, slotColor);
                var srt = slot.rectTransform;
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 0.5f);
                srt.anchoredPosition = new Vector2(_space.ToX(b) + SlotGap * 0.5f, 0f);
                srt.sizeDelta = new Vector2(_space.SpanToWidth(1f) - SlotGap, TrackHeight - 8f);
                _slots.Add(srt);
            }

            // 拍线（每 4 拍加粗为小节线）
            for (int b = 0; b <= (int)TrackLengthBeats; b++)
            {
                bool bar = b % 4 == 0;
                var line = UIFactory.CreatePanel("Beat_" + b, root, new Color(1f, 1f, 1f, bar ? 0.20f : 0.08f));
                line.raycastTarget = false;
                var rt = line.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(_space.ToX(b), 0f);
                rt.sizeDelta = new Vector2(bar ? 2f : 1f, TrackHeight + 40f);
                _beatLines.Add(rt);
                _beatLineBeats.Add(b);
            }
        }

        private void AddResetButton()
        {
            var btn = UIFactory.CreateButton("ResetView", transform, "回起点", ResetView);
            UIStyle.ApplyRound(btn.image, new Color(0.30f, 0.34f, 0.44f, 1f));
            UIStyle.OutlineText(btn.GetComponentInChildren<Text>());
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(140, 44);
            rt.anchoredPosition = new Vector2(16, -12);
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
            foreach (var p in _placed)
            {
                p.Rect.anchoredPosition = new Vector2(_space.ToX(p.StartBeat), p.Rect.anchoredPosition.y);
                p.Rect.sizeDelta = new Vector2(_space.SpanToWidth(p.DurationBeats), p.Rect.sizeDelta.y);
                p.Waveform.SetPitch(_space.PixelsPerBeat / RhythmPattern.HexPerBeat);
            }
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

        /// <summary>卡片拖放落点 → 放置；成功返回 true（卡片回侧栏可复用）。</summary>
        public bool TryPlaceFromCard(DrumCharacterCard card, Vector2 screenPos)
        {
            if (_canvas == null) return false;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPos, _canvas.worldCamera, out local)) return false;
            if (Mathf.Abs(local.y) > TrackHeight * 0.5f + 20f) return false; // 需落在轨道竖向范围内
            float beat = Snap(_space.ToBeat(local.x));
            if (!IsValidBeatRange(beat, card.Character.Pattern.TotalBeats)) return false;
            return Place(card.Character, beat) != null;
        }

        /// <summary>把角色卡拖放接到本板（供启动/接线层调用）。</summary>
        public void AttachCard(DrumCharacterCard card)
        {
            card.OnDragEnd = (c, pos) => TryPlaceFromCard(c, pos);
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
            return pd;
        }

        /// <summary>移动（裸重定位，重叠校验由交互层在移动前判断）。</summary>
        public void MoveTo(PlacedDrum pd, float newBeat)
        {
            pd.StartBeat = newBeat;
            pd.Rect.anchoredPosition = new Vector2(_space.ToX(newBeat), pd.Rect.anchoredPosition.y);
        }

        public void Remove(PlacedDrum pd)
        {
            _undo.Push(new UndoAction { Type = UndoType.Remove, Character = pd.Character, OldBeat = pd.StartBeat });
            RemoveInternal(pd);
        }

        private void RemoveInternal(PlacedDrum pd)
        {
            if (_placed.Remove(pd)) Object.Destroy(pd.Rect.gameObject);
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
            wr.color = new Color(0.45f, 0.85f, 1f, 1f); // 已放置角色：醒目青色
            wr.raycastTarget = true;                     // 可点击 → 双击打开细节预览（加点/洗点）

            var pd = new PlacedDrum(character, startBeat, character.Pattern.TotalBeats, rt, wr);
            PlacedDrumHandle.Attach(go, this, pd);
            return pd;
        }
    }
}
