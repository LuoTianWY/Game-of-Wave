namespace WaveTeam.Core
{
    /// <summary>
    /// 鼓轨时间轴坐标：把「拍」映射到「单位 x」（UI 里单位即像素）。
    /// 线性变换：x = OffsetX + beat * PixelsPerBeat。
    /// 所有纵向音轨共享这一维时间坐标。
    /// </summary>
    public struct BeatSpace
    {
        public float PixelsPerBeat; // 缩放：每拍单位数
        public float OffsetX;       // 平移：拍 0 对应的单位 x

        public BeatSpace(float pixelsPerBeat, float offsetX)
        {
            PixelsPerBeat = pixelsPerBeat;
            OffsetX = offsetX;
        }

        /// <summary>拍 → x。</summary>
        public float ToX(float beat) { return OffsetX + beat * PixelsPerBeat; }

        /// <summary>x → 拍。</summary>
        public float ToBeat(float x) { return (x - OffsetX) / PixelsPerBeat; }

        /// <summary>拍数 → 宽度。</summary>
        public float SpanToWidth(float beats) { return beats * PixelsPerBeat; }

        /// <summary>宽度 → 拍数。</summary>
        public float WidthToSpan(float units) { return units / PixelsPerBeat; }

        /// <summary>以 anchorBeat 为不动点缩放（factor > 0）。</summary>
        public void Zoom(float factor, float anchorBeat)
        {
            float anchorX = ToX(anchorBeat);
            PixelsPerBeat *= factor;
            OffsetX = anchorX - anchorBeat * PixelsPerBeat;
        }

        /// <summary>平移（正值向右）。</summary>
        public void Pan(float dx) { OffsetX += dx; }
    }
}
