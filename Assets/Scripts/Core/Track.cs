using System.Collections.Generic;

namespace WaveTeam.Core
{
    public enum TrackRole { Melody, Harmony, Bass, Rhythm }

    /// <summary>
    /// 一条轨道（一个声部）：声部角色 + 目标谱 + 玩家拼成的链条。
    /// </summary>
    public sealed class Track
    {
        public readonly TrackRole Role;
        public readonly IReadOnlyList<Tile> TargetScore;    // 待复现的目标谱
        public readonly List<Character> Chain = new List<Character>(); // 玩家编排（有序）

        public Track(TrackRole role, params Tile[] target)
        {
            Role = role;
            TargetScore = target;
        }

        public string RoleName
        {
            get
            {
                switch (Role)
                {
                    case TrackRole.Melody: return "主旋律";
                    case TrackRole.Harmony: return "和声";
                    case TrackRole.Bass: return "低音";
                    case TrackRole.Rhythm: return "节奏";
                    default: return "?";
                }
            }
        }
    }
}
