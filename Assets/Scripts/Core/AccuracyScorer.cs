using System;
using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>一次计分结果。</summary>
    public sealed class AccuracyResult
    {
        public readonly int Perfect;   // 完全对齐
        public readonly int Tolerated; // 白名单内偏差（附点/经过音）
        public readonly int Penalized; // 明显偏离

        public AccuracyResult(int perfect, int tolerated, int penalized)
        {
            Perfect = perfect;
            Tolerated = tolerated;
            Penalized = penalized;
        }

        public int TotalSteps { get { return Perfect + Tolerated + Penalized; } }

        /// <summary>准确度 0..1（白名单偏差不扣分）。</summary>
        public float Accuracy
        {
            get { return TotalSteps == 0 ? 0f : (Perfect + Tolerated) / (float)TotalSteps; }
        }
    }

    /// <summary>
    /// 准确度计分：把玩家链条的播放与目标谱对照，用显式白名单 + 分级窗口判定。
    /// 白名单：附点（时值 1.5 倍）、经过音（音高 ±1 且短时值）。其余偏离计罚。
    /// </summary>
    public static class AccuracyScorer
    {
        public static AccuracyResult Score(IReadOnlyList<Tile> target, IReadOnlyList<PlayedNote> played)
        {
            // 以起始拍为键，方便按拍查找
            var byOnset = new Dictionary<int, PlayedNote>();
            foreach (var note in played)
            {
                if (!byOnset.ContainsKey(note.Onset))
                    byOnset[note.Onset] = note;
            }

            int perfect = 0, tolerated = 0, penalized = 0;
            int beat = 0;
            for (int i = 0; i < target.Count; i++)
            {
                var t = target[i];
                PlayedNote note;
                if (byOnset.TryGetValue(beat, out note))
                {
                    if (IsPerfect(t, note)) perfect++;
                    else if (IsTolerated(t, note)) tolerated++;
                    else penalized++;
                }
                else
                {
                    penalized++; // 该拍没有对应音符
                }
                beat += t.Duration;
            }

            return new AccuracyResult(perfect, tolerated, penalized);
        }

        private static bool IsPerfect(Tile target, PlayedNote note)
        {
            return target.Pitch == note.Tile.Pitch && target.Duration == note.Tile.Duration;
        }

        private static bool IsTolerated(Tile target, PlayedNote note)
        {
            // 附点：音高一致，时值为目标的 1.5 倍（用整数比近似，避免浮点误差）
            bool dotted = target.Pitch == note.Tile.Pitch
                          && note.Tile.Duration * 2 == target.Duration * 3;

            // 经过音：音高差 ±1，且短时值（1 拍）
            bool passing = Math.Abs(target.Pitch - note.Tile.Pitch) == 1
                           && note.Tile.Duration <= 1;

            return dotted || passing;
        }
    }
}
