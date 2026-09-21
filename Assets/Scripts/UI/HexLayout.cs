using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 六边形 ↔ 像素换算（尖顶朝上下，R 增 = 屏幕上方，即 UI 局部坐标 y 向上）。
    /// 尺寸约定：1 拍 = 8 格，故 Pitch = 每拍像素 / 8（集成时用 DrumTrackBoard.DefaultPixelsPerBeat / RhythmPattern.HexPerBeat）。
    ///
    /// 方向约定（y 向上，注意与 Core/HexCoord 里注释的罗盘名在垂直方向相反，按向量读）：
    ///   E        = (1, 0)  → 右
    ///   (0, 1)   = 右上    （HexCoord 里标为 SE）
    ///   (1, -1)  = 右下    （HexCoord 里标为 NE）
    /// 波形自左向右流，前进方向即 {右, 右上, 右下} 三种，斜向走位带来「方向性」。
    /// </summary>
    public readonly struct HexLayout
    {
        /// <summary>相邻两列圆心的水平距离（= 一个右向步的水平位移）。</summary>
        public readonly float Pitch;

        public HexLayout(float pitch)
        {
            Pitch = pitch;
        }

        /// <summary>外接圆半径（中心到顶点）。</summary>
        public float Size { get { return Pitch / Mathf.Sqrt(3f); } }

        /// <summary>相邻两行圆心的垂直距离（= 一个斜向步的垂直位移）。</summary>
        public float VerticalPitch { get { return Size * 1.5f; } }

        /// <summary>尖顶朝上下：x = Pitch*(q + r/2)，y = 1.5*Size*r（R 增 = 上）。</summary>
        public Vector2 HexToPixel(HexCoord h)
        {
            return new Vector2(Pitch * (h.Q + h.R * 0.5f), VerticalPitch * h.R);
        }

        /// <summary>像素 → 最接近的六边形（轴向小数坐标 → 立方取整）。</summary>
        public HexCoord PixelToHex(Vector2 p)
        {
            // 由 HexToPixel 反解：px/Pitch = q + r/2，py/VerticalPitch = r，
            // 故分数轴向 q = x - z/2、r = z，再转立方坐标取整。
            float x = p.x / Pitch;
            float z = p.y / VerticalPitch;
            float fq = x - z * 0.5f;
            float fr = z;
            float fx = fq;
            float fz = fr;
            float fy = -fx - fz;
            int rx = Mathf.RoundToInt(fx);
            int ry = Mathf.RoundToInt(fy);
            int rz = Mathf.RoundToInt(fz);
            float dx = Mathf.Abs(rx - fx);
            float dy = Mathf.Abs(ry - fy);
            float dz = Mathf.Abs(rz - fz);
            if (dx > dy && dx > dz) rx = -ry - rz;
            else if (dy > dz) ry = -rx - rz;
            else rz = -rx - ry;
            return new HexCoord(rx, rz);
        }

        /// <summary>六边形 6 个顶点（尖顶朝上），i=0 从右上顶点起顺时针；供渲染画多边形。</summary>
        public Vector2[] Corners(Vector2 center)
        {
            var pts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float ang = Mathf.Deg2Rad * (60f * i + 30f);
                pts[i] = new Vector2(center.x + Size * Mathf.Cos(ang), center.y + Size * Mathf.Sin(ang));
            }
            return pts;
        }
    }
}
