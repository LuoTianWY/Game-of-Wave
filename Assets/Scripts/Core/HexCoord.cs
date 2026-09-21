using System;

namespace WaveTeam.Core
{
    /// <summary>
    /// 六边形轴向坐标（尖顶朝上下）。波形的最小单位 = 一个六边形格；
    /// 六方向邻接是「端点方向相接」共鸣判定的基础（方向性）。
    /// 方向索引约定（逆时针）：0=E, 1=NE, 2=NW, 3=W, 4=SW, 5=SE。
    /// 纯数据、无 UnityEngine，可在 Core 层做连接/距离运算。
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q; // 横轴
        public readonly int R; // 纵轴

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// <summary>6 个轴向方向向量，索引与上方约定一致。</summary>
        public static readonly HexCoord[] Directions = new HexCoord[]
        {
            new HexCoord(1, 0),   // E
            new HexCoord(1, -1),  // NE
            new HexCoord(0, -1),  // NW
            new HexCoord(-1, 0),  // W
            new HexCoord(-1, 1),  // SW
            new HexCoord(0, 1),   // SE
        };

        /// <summary>取某个方向的邻居（dir 自动归一化到 0..5）。</summary>
        public HexCoord Neighbor(int dir)
        {
            int d = ((dir % 6) + 6) % 6;
            var v = Directions[d];
            return new HexCoord(Q + v.Q, R + v.R);
        }

        /// <summary>是否与另一格六方向相邻（= 相接）。</summary>
        public bool IsAdjacent(HexCoord other)
        {
            return Distance(other) == 1;
        }

        /// <summary>若 other 相邻，返回方向索引 0..5；否则返回 -1（用于「方向相接」判定）。</summary>
        public int DirectionTo(HexCoord other)
        {
            int dq = other.Q - Q;
            int dr = other.R - R;
            for (int i = 0; i < 6; i++)
            {
                if (Directions[i].Q == dq && Directions[i].R == dr) return i;
            }
            return -1;
        }

        /// <summary>六边形距离（立方距离）= 最少步数。</summary>
        public int Distance(HexCoord other)
        {
            int dq = Q - other.Q;
            int dr = R - other.R;
            return (Math.Abs(dq) + Math.Abs(dq + dr) + Math.Abs(dr)) / 2;
        }

        public bool Equals(HexCoord other) { return Q == other.Q && R == other.R; }
        public override bool Equals(object obj) { return obj is HexCoord h && Equals(h); }
        public override int GetHashCode() { unchecked { return (Q * 397) ^ R; } }
        public override string ToString() { return "(" + Q + "," + R + ")"; }

        public static HexCoord operator +(HexCoord a, HexCoord b) { return new HexCoord(a.Q + b.Q, a.R + b.R); }
        public static HexCoord operator -(HexCoord a, HexCoord b) { return new HexCoord(a.Q - b.Q, a.R - b.R); }
        public static bool operator ==(HexCoord a, HexCoord b) { return a.Equals(b); }
        public static bool operator !=(HexCoord a, HexCoord b) { return !a.Equals(b); }
    }
}
