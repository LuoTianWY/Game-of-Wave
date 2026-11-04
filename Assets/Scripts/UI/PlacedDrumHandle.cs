using UnityEngine;
using UnityEngine.EventSystems;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 已放鼓单元的交互：左键拖拽移动、右键收回。
    /// 拖拽期间显示吸附预览，松开时才修改片段位置。
    /// </summary>
    public sealed class PlacedDrumHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private DrumTrackBoard _board;
        private PlacedDrum _placed;

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
        }

        private void OnDestroy()
        {
            if (_placed != null && _placed.Character != null)
                _placed.Character.Changed -= Refresh;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_placed.Locked && e.button == PointerEventData.InputButton.Left)
                _board.ShowDropPreview(_placed.Character, e.position, _placed);
        }

        public void OnEndDrag(PointerEventData e)
        {
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
            else if (e.button == PointerEventData.InputButton.Left && e.clickCount >= 2)
            {
                _board.OpenDetail(_placed); // 左键双击：打开细节预览（加点/洗点）；单击留给拖拽
            }
        }
    }
}
