using System;
using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>节奏点（骨架）：波形里代表一次敲击的关键拍。</summary>
    public readonly struct RhythmPoint
    {
        public readonly float Beat;   // 拍位置（单位：拍；1/4 拍分辨率）
        public readonly int Level;    // 纵向层（表现；当前 spike 未用，预留重拍高低）
        public readonly bool Accent;  // 是否重拍（表现：更深更亮）

        public RhythmPoint(float beat, int level, bool accent)
        {
            Beat = beat;
            Level = level;
            Accent = accent;
        }
    }

    /// <summary>波形路径上的一个六边形格（肉）。</summary>
    public readonly struct HexCell
    {
        public readonly HexCoord Hex;
        public readonly bool IsRhythm; // 是否节奏点格
        public readonly bool Accent;   // 节奏点且为重拍
        public readonly int PointIndex; // 所属节奏点下标（填充格 = 指向的下一节奏点）；-1 = 无

        public HexCell(HexCoord hex, bool isRhythm, bool accent, int pointIndex = -1)
        {
            Hex = hex;
            IsRhythm = isRhythm;
            Accent = accent;
            PointIndex = pointIndex;
        }
    }

    /// <summary>
    /// 节奏型：一段节奏点序列，可展开成一条六边形波形路径。
    /// 节奏点是「骨架」——钉在拍网格上（直线距离 = Δ拍 × 单位宽，通用）；
    /// 六边形是「肉」——山/谷交替、斜向走位带来方向性，中间怎么扭都不影响骨架。
    /// 纯数据、无 UnityEngine；像素换算交给 UI/HexLayout。
    /// </summary>
    public sealed class RhythmPattern
    {
        /// <summary>1 拍 = 8 格（与 UI/HexLayout 的 Pitch = 每拍像素 / 8 对应）。直线距离固定为 8 格宽，可精确表达 1/8、1/4、1/2 拍。</summary>
        public const int HexPerBeat = 8;

        public readonly RhythmPoint[] Points;   // 按拍升序
        public readonly float TotalBeats;

        public RhythmPattern(RhythmPoint[] points, float totalBeats)
        {
            var sorted = (RhythmPoint[])points.Clone();
            Array.Sort(sorted, (a, b) => a.Beat.CompareTo(b.Beat));
            Points = sorted;
            TotalBeats = totalBeats;
        }

        /// <summary>
        /// 展开成六边形路径（含节奏点标记）。seed 决定山/谷交替，让不同角色长相不同。
        /// 节奏点是骨架、钉在拍网格上（1 拍 = HexPerBeat 格直线距离）；中间怎么扭（山/谷）都会
        /// 精确落回下一个节奏点，末尾再补一段尾巴连到乐句结束拍，保证整段波形刚好占满整数小节。
        /// </summary>
        public List<HexCell> BuildPath(int seed)
        {
            var cells = new List<HexCell>();
            // 每个角色整体纵向抬升一个不同高度（由 seed 决定），让不同角色的首尾端点可能不在同一高度 → 前后可能接不上
            int level = LevelForSeed(seed); // -2..+2
            // 方向（y 向上，与 HexLayout 一致）：右 / 右上 / 右下
            var right = new HexCoord(1, 0);
            var up = new HexCoord(0, 1);
            var down = new HexCoord(1, -1);

            HexCoord last = default(HexCoord);
            for (int k = 0; k < Points.Length; k++)
            {
                var p = Points[k];
                var hex = PointHex(p);
                cells.Add(new HexCell(hex, true, p.Accent, k)); // 节奏点格
                last = hex;
                if (k == Points.Length - 1) break;

                var next = PointHex(Points[k + 1]);
                int h = next.Q - hex.Q;               // 水平 pitch 数（Level 全 0 时 = HexPerBeat × Δ拍）
                if (h <= 0) continue;                 // 同拍/倒退，防御跳过

                int m = Math.Min(2, h);               // 波峰高度：长段更高，短段保持 1 格尖峰
                bool hill = ((seed + k) & 1) == 0;    // 山/谷交替
                var first = hill ? up : down;
                var second = hill ? down : up;

                for (int i = 0; i < m; i++) { hex += first; cells.Add(new HexCell(hex, false, false, k + 1)); }
                for (int i = 0; i < h - m; i++) { hex += right; cells.Add(new HexCell(hex, false, false, k + 1)); }
                for (int i = 0; i < m; i++) { hex += second; cells.Add(new HexCell(hex, false, false, k + 1)); }
            }

            // 尾巴：从最后一个节奏点连到乐句末尾（TotalBeats），整段波形长度对齐整数小节
            if (Points.Length > 0)
            {
                int endQ = (int)Math.Round(HexPerBeat * TotalBeats);
                int h = endQ - last.Q;
                if (h > 0)
                {
                    int lastIdx = Points.Length - 1;
                    int m = Math.Min(2, h);
                    bool hill = ((seed + lastIdx) & 1) == 0;
                    var first = hill ? up : down;
                    var second = hill ? down : up;
                    for (int i = 0; i < m; i++) { last += first; cells.Add(new HexCell(last, false, false, lastIdx)); }
                    for (int i = 0; i < h - m; i++) { last += right; cells.Add(new HexCell(last, false, false, lastIdx)); }
                    for (int i = 0; i < m; i++) { last += second; cells.Add(new HexCell(last, false, false, lastIdx)); }
                }
            }
            // 整体纵向平移 level，让首尾端点落在不同高度（不同角色可能接不上）
            if (level != 0)
            {
                var off = new HexCoord(0, level);
                for (int i = 0; i < cells.Count; i++)
                {
                    var c = cells[i];
                    cells[i] = new HexCell(c.Hex + off, c.IsRhythm, c.Accent, c.PointIndex);
                }
            }
            return cells;
        }

        /// <summary>节奏点 → 六边形坐标。spike：节奏点全在基线（Level 暂未用于形状）。</summary>
        private static HexCoord PointHex(RhythmPoint p)
        {
            int q = (int)Math.Round(HexPerBeat * p.Beat);
            return new HexCoord(q, 0);
        }

        /// <summary>示例：四落底（4-on-the-floor），每拍一击。</summary>
        public static RhythmPattern FourOnFloor()
        {
            return new RhythmPattern(new[]
            {
                new RhythmPoint(0f, 0, true),
                new RhythmPoint(1f, 0, false),
                new RhythmPoint(2f, 0, false),
                new RhythmPoint(3f, 0, false),
            }, 4f);
        }

        /// <summary>示例：切分（offbeat），节奏点间距不同（0.5 / 1.5 / 0.5 / 1 拍）。</summary>
        public static RhythmPattern Offbeat()
        {
            return new RhythmPattern(new[]
            {
                new RhythmPoint(0f, 0, true),
                new RhythmPoint(0.5f, 0, false),
                new RhythmPoint(2f, 0, false),
                new RhythmPoint(2.5f, 0, false),
                new RhythmPoint(3.5f, 0, true),
            }, 4f);
        }

        /// <summary>示例：后拍（backbeat），第 2 拍一击（spike 占位）。</summary>
        public static RhythmPattern Backbeat()
        {
            return new RhythmPattern(new[]
            {
                new RhythmPoint(0f, 0, true),
                new RhythmPoint(2f, 0, false),
            }, 4f);
        }

        /// <summary>取前 count 个节奏点的子型（加点/洗点用）；越界自动夹取，时值不变。</summary>
        public RhythmPattern WithPoints(int count)
        {
            int n = Math.Min(Math.Max(0, count), Points.Length);
            var sub = new RhythmPoint[n];
            Array.Copy(Points, sub, n);
            return new RhythmPattern(sub, TotalBeats);
        }

        /// <summary>按解锁掩码取子型：只保留已解锁的节奏点（点到哪加到哪），时值不变。</summary>
        public RhythmPattern WithMask(bool[] unlocked)
        {
            var list = new List<RhythmPoint>();
            int n = Math.Min(unlocked.Length, Points.Length);
            for (int i = 0; i < n; i++)
                if (unlocked[i]) list.Add(Points[i]);
            return new RhythmPattern(list.ToArray(), TotalBeats);
        }

        /// <summary>按 seed 挑一个示例节奏型（spike 占位，等「角色 → 节奏型」真实映射替换）。</summary>
        public static RhythmPattern ForSeed(int seed)
        {
            int m = ((seed % 3) + 3) % 3;
            if (m == 0) return FourOnFloor();
            if (m == 1) return Offbeat();
            return Backbeat();
        }

        /// <summary>稳定字符串哈希（String.GetHashCode 跨进程不稳定，勿用作 seed）。</summary>
        public static int StableHash(string s)
        {
            int h = 0;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }

        /// <summary>由 seed 决定角色的纵向抬升档位（-2..+2）；BuildPath 与连接桥共用，保证一致。</summary>
        public static int LevelForSeed(int seed)
        {
            return ((seed % 5) + 5) % 5 - 2;
        }
    }
}
