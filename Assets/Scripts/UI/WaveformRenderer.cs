using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 把 WaveformDefinition 画成一条波形曲线（uGUI 程序化图形）。
    /// 画在固定大小单元内：从上到下 4 段，头/尾端点落在对应段中心；
    /// 曲线 = 包络（Attack 起振 / Decay 衰减）× 正弦（Cycles 周期数），
    /// 基线从头端点段插值到尾端点段（让端点位置与契合度判定对齐）。
    /// </summary>
    public sealed class WaveformRenderer : MaskableGraphic
    {
        private const int Samples = 64;      // 曲线采样点数
        private const float AmpNorm = 0.16f; // 最大振幅（相对单元高度）
        private const float Thickness = 3f;  // 曲线线宽（像素）
        private const float DotRadius = 5f;  // 端点圆点半径
        private const float TrembleFreq = 60f; // 颤动相位推进速度（弧度/秒）

        private float _tremble;       // 当前颤动强度 0..1
        private float _tremblePhase;  // 颤动相位
        private Coroutine _trembleCo;

        public WaveformDefinition Definition { get; private set; }

        public void SetDefinition(WaveformDefinition def)
        {
            Definition = def;
            SetVerticesDirty();
        }

        /// <summary>触发一次「共振颤动」：强度 0..1，约 duration 秒内衰减归零。</summary>
        public void Tremble(float intensity, float duration = 0.4f)
        {
            if (_trembleCo != null) StopCoroutine(_trembleCo);
            _trembleCo = StartCoroutine(TrembleRoutine(Mathf.Clamp01(intensity), duration));
        }

        private IEnumerator TrembleRoutine(float intensity, float duration)
        {
            _tremble = intensity;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _tremblePhase += Time.deltaTime * TrembleFreq;
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
            if (rect.width <= 0f || rect.height <= 0f) return;

            DrawBandLines(vh, rect);
            if (Definition != null)
            {
                DrawCurve(vh, rect);
                DrawEndpoints(vh, rect);
            }
        }

        // 4 段 → 3 条内部分隔线（v = 0.25 / 0.5 / 0.75，底→顶）
        private void DrawBandLines(VertexHelper vh, Rect rect)
        {
            var lineColor = new Color(1f, 1f, 1f, 0.12f);
            for (int i = 1; i <= 3; i++)
            {
                float v = i * 0.25f;
                float y = Mathf.Lerp(rect.yMin, rect.yMax, v);
                AddLine(vh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), 1.5f, lineColor);
            }
        }

        private void DrawCurve(VertexHelper vh, Rect rect)
        {
            var def = Definition;
            float headV = SegmentCenterV(def.HeadSegment);
            float tailV = SegmentCenterV(def.TailSegment);

            float attackLen = 0.1f + (1f - def.Attack) * 0.3f;
            float decayScale = 1f + def.Decay * 8f;

            int start = vh.currentVertCount;
            for (int i = 0; i <= Samples; i++)
            {
                float u = i / (float)Samples;
                float env = Envelope(u, attackLen, decayScale);
                float v = Mathf.Lerp(headV, tailV, u)
                        + env * Mathf.Sin(2f * Mathf.PI * def.Cycles * u) * AmpNorm
                        + _tremble * Mathf.Sin(_tremblePhase) * AmpNorm;
                v = Mathf.Clamp01(v);
                float x = Mathf.Lerp(rect.xMin, rect.xMax, u);
                float y = Mathf.Lerp(rect.yMin, rect.yMax, v);

                vh.AddVert(new Vector3(x, y + Thickness * 0.5f, 0f), color, Vector2.zero);
                vh.AddVert(new Vector3(x, y - Thickness * 0.5f, 0f), color, Vector2.zero);
            }
            for (int i = 0; i < Samples; i++)
            {
                int a = start + i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                vh.AddTriangle(a, b, c);
                vh.AddTriangle(b, d, c);
            }
        }

        private void DrawEndpoints(VertexHelper vh, Rect rect)
        {
            var dot = new Color(1f, 1f, 1f, 0.9f);
            float headV = SegmentCenterV(Definition.HeadSegment);
            float tailV = SegmentCenterV(Definition.TailSegment);
            AddDot(vh, new Vector2(rect.xMin + 2f, Mathf.Lerp(rect.yMin, rect.yMax, headV)), DotRadius, dot);
            AddDot(vh, new Vector2(rect.xMax - 2f, Mathf.Lerp(rect.yMin, rect.yMax, tailV)), DotRadius, dot);
        }

        // 段号 1..4 → 段中心 v（0..1，底→顶）：1=顶 0.875 … 4=底 0.125
        private static float SegmentCenterV(int segment)
        {
            return (5 - segment) * 0.25f - 0.125f;
        }

        // 包络：u < attackLen 线性上升，之后指数衰减
        private static float Envelope(float u, float attackLen, float decayScale)
        {
            if (u < attackLen) return u / attackLen;
            return Mathf.Exp(-decayScale * (u - attackLen));
        }

        // 画一条水平线（沿法线方向加厚的薄矩形）
        private void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color c)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len * (thickness * 0.5f);

            int s = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x + n.x, a.y + n.y, 0f), c, Vector2.zero);
            vh.AddVert(new Vector3(a.x - n.x, a.y - n.y, 0f), c, Vector2.zero);
            vh.AddVert(new Vector3(b.x + n.x, b.y + n.y, 0f), c, Vector2.zero);
            vh.AddVert(new Vector3(b.x - n.x, b.y - n.y, 0f), c, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s + 1, s + 3, s + 2);
        }

        // 画一个端点圆点（方形近似）
        private void AddDot(VertexHelper vh, Vector2 center, float radius, Color c)
        {
            int s = vh.currentVertCount;
            vh.AddVert(new Vector3(center.x - radius, center.y - radius, 0f), c, Vector2.zero);
            vh.AddVert(new Vector3(center.x - radius, center.y + radius, 0f), c, Vector2.zero);
            vh.AddVert(new Vector3(center.x + radius, center.y + radius, 0f), c, Vector2.zero);
            vh.AddVert(new Vector3(center.x + radius, center.y - radius, 0f), c, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
        }
    }
}
