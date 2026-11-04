
using System;
using System.Collections.Generic;
using WaveTeam.Core;
using WaveTeam.UI;

namespace WaveTeam.Room
{
    public sealed class RehearsalScoreResult
    {
        public int Score;

        // 三个排练目标是否达成
        public bool OpeningStableGood;
        public bool DevelopmentLayeredGood;
        public bool DensityGood;

        public string ChallengeTitle;
        public string Feedback;
        public string Advice;
        public string Progress;
        public bool ChallengePassed;
    }

    public static class RehearsalEvaluator
    {
        public static RehearsalScoreResult Evaluate(DrumTrackBoard board, RehearsalChallenge challenge)
        {
            return challenge.Target == null ? Evaluate(board) : EvaluateRhythm(board.Placed, challenge);
        }

        internal static RehearsalScoreResult EvaluateRhythm(IReadOnlyList<PlacedDrum> arrangement,
                                                            RehearsalChallenge challenge)
        {
            var region = challenge.EditableRegion.Value;
            var playerClips = new List<PlacedDrum>();
            PlacedDrum before = null, after = null;
            foreach (var clip in arrangement)
            {
                if (clip.LaneIndex != region.LaneIndex) continue;
                if (!clip.Locked)
                    playerClips.Add(clip);
                else if (SameBeat(clip.StartBeat + clip.DurationBeats, region.StartBeat))
                    before = clip;
                else if (SameBeat(clip.StartBeat, region.EndBeat))
                    after = clip;
            }

            playerClips.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));
            float end = region.StartBeat;
            bool covered = playerClips.Count > 0;
            var beats = new List<float>();
            var adjacentSample = before?.Character.Sample;
            bool sameSound = adjacentSample?.Clip != null && adjacentSample.Clip == after?.Character.Sample?.Clip
                && adjacentSample.Definition.Type == challenge.Target.Sound;
            foreach (var clip in playerClips)
            {
                covered &= SameBeat(clip.StartBeat, end);
                end = clip.StartBeat + clip.DurationBeats;
                var points = clip.Character.PlaybackPoints;
                sameSound &= points.Length > 0 && clip.Character.Sample.Clip == adjacentSample?.Clip;
                foreach (var point in points)
                    beats.Add(clip.StartBeat + point.Beat - region.StartBeat);
            }
            covered &= SameBeat(end, region.EndBeat);

            beats.Sort();
            var target = challenge.Target.Rhythm.Points;
            bool rhythmMatches = beats.Count == target.Length;
            for (int i = 0; rhythmMatches && i < beats.Count; i++)
                rhythmMatches = SameBeat(beats[i], target[i].Beat);

            bool continuous = covered && sameSound;
            var weights = challenge.Scoring;
            int coverageScore = covered ? weights.Coverage : 0;
            int rhythmScore = rhythmMatches ? weights.Rhythm : 0;
            int continuityScore = continuous ? weights.Continuity : 0;
            int score = coverageScore + rhythmScore + continuityScore;
            bool passed = challenge.IsComplete(score);
            return new RehearsalScoreResult
            {
                Score = score,
                ChallengeTitle = challenge.Title,
                ChallengePassed = passed,
                Feedback = "缺失片段补全    " + coverageScore + " / " + weights.Coverage
                    + "\n\n节奏匹配    " + rhythmScore + " / " + weights.Rhythm
                    + "\n\n音乐连续性    " + continuityScore + " / " + weights.Continuity,
                Advice = !covered ? weights.CoverageHint
                    : !rhythmMatches ? weights.RhythmHint
                    : !continuous ? weights.ContinuityHint
                    : "缺失片段已接回原有节奏。",
                Progress = passed ? "关卡已完成。" : "达到 " + challenge.PassingScore + " 分即可完成本关。"
            };
        }

        private static bool SameBeat(float a, float b) { return Math.Abs(a - b) < 0.001f; }

        public static RehearsalScoreResult Evaluate(DrumTrackBoard board)
        {
            var result = new RehearsalScoreResult();

            if (board == null || board.Placed.Count == 0)
                return result;

            int highDensityCount = 0;

            foreach (var drum in board.Placed)
            {
                // 从 RehearsalTraits 获取角色属性
                var stats = RehearsalTraits.GetStats(
                    drum.Character.Noun
                );

                float startBeat = drum.StartBeat;

                // 目标1：开场（0-3拍）需要稳定
                if (startBeat >= 0f &&
                    startBeat < 4f &&
                    stats.Stability >= 3)
                {
                    result.OpeningStableGood = true;
                }

                // 目标2：发展（4-7拍）需要层次
                if (startBeat >= 4f &&
                    startBeat < 8f &&
                    stats.Layering >= 3)
                {
                    result.DevelopmentLayeredGood = true;
                }

                // 目标3：高密度波形不宜太多
                if (stats.Density >= 3)
                {
                    highDensityCount++;
                }
            }

            // 至少安排两段，高密度波形最多一段
            result.DensityGood =
                board.Placed.Count >= 2 &&
                highDensityCount <= 1;

            // 统一排练评分
            result.Score =
                (result.OpeningStableGood ? 33 : 0) +
                (result.DevelopmentLayeredGood ? 33 : 0) +
                (result.DensityGood ? 34 : 0);

            return result;
        }
    }
}
