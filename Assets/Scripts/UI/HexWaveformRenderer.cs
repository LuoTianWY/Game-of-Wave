using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 把 RhythmPattern 展开成的六边形路径画出来（uGUI 程序化图形）。
    /// 规则：
    ///  - 固定格距（不随加点缩放），六边形大小始终一致；
    ///  - 水平锚定在 rect 左缘：格子 q=0 的中心即 rect 左缘（对应拍线），第一格锁死为吸附锚点；
    ///  - 垂直居中于 rect，路径在 1 拍内直线距离固定 = HexPerBeat 格宽；
    ///  - 节奏点格更亮发光（重拍加 halo），普通格暗淡、略小；未解锁格画成暗灰轮廓。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HexWaveformRenderer : MaskableGraphic, IPointerClickHandler
    {
        private const float DefaultPitch = 60f;

        private float _pitch = DefaultPitch;
        private HexLayout _layout = new HexLayout(DefaultPitch);
        private List<HexCell> _path;          // 展开后的六边形路径
        private float _pathMinX, _pathMaxX;   // 路径横向包围盒（HexLayout 空间，用于内容宽度）
        private float _pathMinY, _pathMaxY;   // 路径纵向包围盒（HexLayout 空间，用于垂直居中）
        private float _tremble;               // 共振颤动强度 0..1（脉冲放大）
        private Coroutine _trembleCo;
        private int _unlockedCells = int.MaxValue; // 已解锁路径格数（前缀）；≥此下标的格子画成暗灰「未解锁」；默认全解锁

        /// <summary>点击到某个路径格时触发（参数 = 路径格下标、是否右键；供细节预览点击加点/取消用）。</summary>
        public event System.Action<int, bool> CellClicked;

        /// <summary>设置节奏型与随机种子，重新展开路径并刷新。</summary>
        public void SetPattern(RhythmPattern pattern, int seed)
        {
            _path = pattern != null ? pattern.BuildPath(seed) : null;
            ComputeBounds();
            SetVerticesDirty();
        }

        /// <summary>固定格距（1 格 = 相邻六边形中心的水平间距）。板上 = 每拍像素 / HexPerBeat；卡/预览 = 固定值。</summary>
        public void SetPitch(float pitch)
        {
            if (pitch <= 0.01f) pitch = 0.01f;
            if (Mathf.Abs(_pitch - pitch) < 1e-4f) return;
            _pitch = pitch;
            _layout = new HexLayout(pitch);
            ComputeBounds(); // 纵向包围盒随 pitch 缩放，需重算
            SetVerticesDirty();
        }

        /// <summary>设置已解锁的路径格数（前缀长度，细节预览用）；≥此下标的格子画成暗灰轮廓。默认全解锁。</summary>
        public void SetUnlocked(int cellCount)
        {
            _unlockedCells = cellCount;
            SetVerticesDirty();
        }

        /// <summary>波形内容在本地像素空间的理想尺寸（含一圈格半径边距），供外层滚动区域铺内容。</summary>
        public Vector2 GetContentSize()
        {
            if (_path == null || _path.Count == 0) return Vector2.zero;
            float size = _layout.Size;
            return new Vector2(_pathMaxX - _pathMinX + size * 2f, _pathMaxY - _pathMinY + size * 2f);
        }

        private void ComputeBounds()
        {
            if (_path == null || _path.Count == 0)
            {
                _pathMinX = _pathMaxX = _pathMinY = _pathMaxY = 0f;
                return;
            }
            _pathMinX = _pathMinY = float.PositiveInfinity;
            _pathMaxX = _pathMaxY = float.NegativeInfinity;
            foreach (var c in _path)
            {
                Vector2 p = _layout.HexToPixel(c.Hex);
                if (p.x < _pathMinX) _pathMinX = p.x;
                if (p.x > _pathMaxX) _pathMaxX = p.x;
                if (p.y < _pathMinY) _pathMinY = p.y;
                if (p.y > _pathMaxY) _pathMaxY = p.y;
            }
        }

        /// <summary>触发一次「共振颤动」：强度 0..1，约 duration 秒内衰减归零（供 PerformancePlayer 调用）。</summary>
        public void Tremble(float intensity, float duration = 0.4f)
        {
            if (_trembleCo != null) StopCoroutine(_trembleCo);
            _trembleCo = StartCoroutine(TrembleRoutine(Mathf.Clamp01(intensity), duration));
        }

        private System.Collections.IEnumerator TrembleRoutine(float intensity, float duration)
        {
            _tremble = intensity;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _tremble = Mathf.Lerp(intensity, 0f, t / duration);
                SetVerticesDirty();
                yield return null;
            }
            _tremble = 0f;
            _trembleCo = null;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f || _path == null || _path.Count == 0) return;

            // 固定格距，水平锚定 rect 左缘（q=0 中心 = 拍线），垂直居中；不做等比缩放，保证格子大小恒定
            float originY = rect.center.y - (_pathMinY + _pathMaxY) * 0.5f;
            float size = _layout.Size;
            float pulse = 1f + _tremble * 0.15f;

            var normalColor = new Color(color.r, color.g, color.b, color.a * 0.45f); // 普通格：暗淡
            var rhythmColor = color;                                                  // 节奏点：全亮

            for (int i = 0; i < _path.Count; i++)
            {
                var cell = _path[i];
                Vector2 p = _layout.HexToPixel(cell.Hex);
                Vector2 c = new Vector2(p.x, p.y + originY);
                bool locked = i >= _unlockedCells;

                if (locked)
                {
                    // 未解锁：暗灰「骨架轮廓」，无光晕、略小，与已解锁亮块形成对比
                    var lc = cell.IsRhythm
                        ? new Color(0.42f, 0.44f, 0.52f, 0.50f)   // 未解锁节奏点
                        : new Color(0.42f, 0.44f, 0.52f, 0.22f);  // 未解锁填充
                    DrawHex(vh, c, size * (cell.IsRhythm ? 0.92f : 0.8f), lc);
                }
                else if (cell.IsRhythm)
                {
                    // 光效：先画一圈放大低透明度的 halo，再画实心（重拍用纯白提亮）
                    var halo = new Color(rhythmColor.r, rhythmColor.g, rhythmColor.b, rhythmColor.a * 0.35f);
                    DrawHex(vh, c, size * 1.30f * pulse, halo);
                    DrawHex(vh, c, size * pulse, cell.Accent ? Color.white : rhythmColor);
                }
                else
                {
                    DrawHex(vh, c, size * 0.92f * pulse, normalColor); // 略小留缝
                }
            }
        }

        /// <summary>把本组件局部坐标命中到路径格下标（供细节预览点击加点/取消）；未命中返回 -1。</summary>
        public int HitPoint(Vector2 localPoint)
        {
            if (_path == null || _path.Count == 0) return -1;
            var rect = GetPixelAdjustedRect();
            float originY = rect.center.y - (_pathMinY + _pathMaxY) * 0.5f;
            var h = _layout.PixelToHex(new Vector2(localPoint.x, localPoint.y - originY));
            for (int i = 0; i < _path.Count; i++)
                if (_path[i].Hex == h) return i;
            return -1;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (CellClicked == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, e.position, canvas != null ? canvas.worldCamera : null, out local))
            {
                int idx = HitPoint(local);
                if (idx >= 0) CellClicked(idx, e.button == PointerEventData.InputButton.Right);
            }
        }

        // 画一个尖顶朝上的实心六边形（中心 + 外接圆半径），从顶点 0 做凸扇形三角剖分
        private static void DrawHex(VertexHelper vh, Vector2 center, float radius, Color c)
        {
            int s = vh.currentVertCount;
            for (int i = 0; i < 6; i++)
            {
                float ang = Mathf.Deg2Rad * (60f * i + 30f);
                vh.AddVert(new Vector3(center.x + radius * Mathf.Cos(ang), center.y + radius * Mathf.Sin(ang), 0f), c, Vector2.zero);
            }
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
            vh.AddTriangle(s, s + 3, s + 4);
            vh.AddTriangle(s, s + 4, s + 5);
        }
    }
}
