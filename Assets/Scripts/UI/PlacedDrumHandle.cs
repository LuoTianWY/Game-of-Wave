using UnityEngine;
using UnityEngine.EventSystems;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 已放鼓单元的交互：左键拖拽移动、右键收回。
    /// 拖拽期间提升到画布顶层跟随鼠标；松开时经 Board.TryMove 校验，失败回原位。
    /// </summary>
    public sealed class PlacedDrumHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private DrumTrackBoard _board;
        private PlacedDrum _placed;
        private Canvas _rootCanvas;
        private Transform _originalParent;
        private Vector2 _originalPivot;

        public static PlacedDrumHandle Attach(GameObject cell, DrumTrackBoard board, PlacedDrum placed)
        {
            var h = cell.GetComponent<PlacedDrumHandle>();
            if (h == null) h = cell.AddComponent<PlacedDrumHandle>();
            h._board = board;
            h._placed = placed;
            h._originalPivot = placed.Rect.pivot;
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
            _originalParent = transform.parent;
            _originalPivot = _placed.Rect.pivot;

            _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas != null)
            {
                transform.SetParent(_rootCanvas.transform, true);
                transform.SetAsLastSibling();
            }
            _placed.Rect.pivot = new Vector2(0.5f, 0.5f);
            FollowPointer(e);
        }

        public void OnDrag(PointerEventData e)
        {
            FollowPointer(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_originalParent != null) transform.SetParent(_originalParent, false);
            _placed.Rect.pivot = _originalPivot;

            float beat = _board.ScreenToBeat(e.position);
            if (!_board.TryMove(_placed, beat))
            {
                _board.MoveTo(_placed, _placed.StartBeat); // 回原位
            }
            _rootCanvas = null;
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

        private void FollowPointer(PointerEventData e)
        {
            if (_rootCanvas == null) return;
            var canvasRect = (RectTransform)_rootCanvas.transform;
            Vector3 world;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, e.position, _rootCanvas.worldCamera, out world))
            {
                _placed.Rect.position = world;
            }
        }
    }
}
