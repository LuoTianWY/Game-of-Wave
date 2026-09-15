using System;
using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 节奏契合度：按「落在正拍」的程度给分。
    /// 正拍 = 起始拍为整数（0、1、2、…）；落在半拍记部分权重。
    /// 最终分数 = 加权时值 / 总时值（0~1）。
    /// </summary>
    public static class RhythmScorer
    {
        public const float OnBeatWeight = 1.0f;
        public const float OffBeatWeight = 0.5f;

        public static float Score(IEnumerable<PlayedNote> notes)
        {
            float weighted = 0f, total = 0f;
            foreach (var note in notes)
            {
                float weight = IsOnBeat(note.Onset) ? OnBeatWeight : OffBeatWeight;
                weighted += weight * note.Tile.Duration;
                total += note.Tile.Duration;
            }
            return total <= 0f ? 0f : weighted / total;
        }

        private static bool IsOnBeat(float onset)
        {
            return Math.Abs(onset - Math.Round(onset)) < 0.001f;
        }
    }
}
