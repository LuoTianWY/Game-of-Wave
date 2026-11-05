using UnityEngine;
using UnityEngine.EventSystems;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 已放鼓单元的交互：左键拖拽移动、右键收回。
    /// 拖拽期间显示吸附预览，松开时才修改片段位置。
    /// </summary>
    public sealed class PlacedDrumHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private DrumTrackBoard _board;
        private PlacedDrum _placed;
        private bool _dragging;

        public static PlacedDrumHandle Attach(GameObject cell, DrumTrackBoard board, PlacedDrum placed)
        {
            var h = cell.GetComponent<PlacedDrumHandle>();
            if (h == null) h = cell.AddComponent<PlacedDrumHandle>();
            h._board = board;
            h._placed = placed;
            placed.Character.Changed += h.Refresh;
            return h;
        }

        /// <summary>加点/洗点后刷新这条六边形 blob 的形状。</summary>
        private void Refresh()
        {
            if (_placed == null || _placed.Character == null || _placed.Waveform == null) return;
            _placed.Waveform.SetPattern(_placed.Character.EffectivePattern, RhythmPattern.StableHash(_placed.Character.Name));
            _board.NotifyArrangementChanged();
        }

        private void OnDestroy()
        {
            if (_placed != null && _placed.Character != null)
                _placed.Character.Changed -= Refresh;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            _board.HoverPlaced(_placed, false);
            _board.SelectPlaced(_placed);
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_placed.Locked && e.button == PointerEventData.InputButton.Left)
                _board.ShowDropPreview(_placed.Character, e.position, _placed);
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            if (_placed.Locked || e.button != PointerEventData.InputButton.Left) return;
            _board.HideDropPreview();
            _board.TryMoveFromPointer(_placed, e.position);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right)
            {
                _board.Remove(_placed);
            }
            else if (e.button == PointerEventData.InputButton.Left && !e.dragging)
            {
                _board.SelectPlaced(_placed);
                if (e.clickCount >= 2) _board.OpenDetail(_placed);
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!_dragging) _board.HoverPlaced(_placed, true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _board.HoverPlaced(_placed, false);
        }
    }
}
