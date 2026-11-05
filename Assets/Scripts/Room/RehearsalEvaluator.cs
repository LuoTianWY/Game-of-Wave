
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
        public string OpeningAdvice;
        public string DevelopmentAdvice;
        public string DensityAdvice;

        public string ChallengeTitle;
        public string Grade;
        public string Feedback;
        public string Advice;
        public string Progress;
        public bool ChallengePassed;
    }

    public static class RehearsalEvaluator
    {
        public static RehearsalScoreResult Evaluate(DrumTrackBoard board, RehearsalChallenge challenge)
        {
            if (challenge == RehearsalChallenge.OurOwnSound) return EvaluateComposition(board.Placed);
            return !challenge.IsRhythmChallenge ? Evaluate(board) : EvaluateRhythm(board.Placed, challenge);
        }

        internal static RehearsalScoreResult EvaluateComposition(IReadOnlyList<PlacedDrum> arrangement)
        {
            var music = Measure(arrangement);
            bool opening = music.Covered[0] >= 3.99f && music.Lanes[0].Count == 1 &&
                music.Events[0] >= 2 && music.Events[0] <= 6 &&
                music.IntegerOpeningEvents >= music.Events[0] * 0.75f;
            bool development = music.Lanes[1].Count >= 2 && music.Lanes[1].Count > music.Lanes[0].Count &&
                music.Sounds[1].Count >= 2;
            bool climax = music.Events[2] / 8f > music.Events[1] / 4f &&
                music.Lanes[2].Count >= music.Lanes[1].Count;
            bool density = music.SoundsAll.Count >= 2 && music.TotalEvents >= 10 &&
                music.TotalEvents <= 56 && music.PeakPerBeat <= 5;
            int coverageScore = music.TotalCovered >= 15.99f ? 20 : music.TotalCovered >= 11.99f ? 10 : 0;
            int score = (opening ? 20 : 0) + (development ? 20 : 0) +
                (climax ? 20 : 0) + (density ? 20 : 0) + coverageScore;
            bool passed = opening && development && climax && density && coverageScore >= 10;
            return new RehearsalScoreResult {
                Score = score, ChallengeTitle = RehearsalChallenge.OurOwnSound.Title,
                ChallengePassed = passed,
                Grade = passed ? score == 100 ? "完美创作" : "合格创作" : "继续打磨",
                Feedback = "开场稳定    " + Describe(opening, 20) +
                    "\n发展增层    " + Describe(development, 20) +
                    "\n高潮推进    " + Describe(climax, 20) +
                    "\n整体密度    " + Describe(density, 20) +
                    "\n结构覆盖    " + coverageScore + " / 20",
                Advice = !opening ? "开场保留一条整拍声部，四拍内 2–6 击。"
                    : !development ? "发展段加入第二条声部和另一种音色。"
                    : !climax ? "高潮的平均击打密度要高于发展段，保留已有的声部。"
                    : !density ? "至少两种音色，总击打 10–56 次，每拍最多 5 次。"
                    : coverageScore < 10 ? "延长音乐，让声部总共覆盖至少十二拍。"
                    : coverageScore < 20 ? "已能保存正式作品。补齐十六拍可获得完美创作。"
                    : "这是你和乐队共同编排的声音。",
                Progress = passed ? "作品合格，可以保存并与乐队分享。" : "四项音乐条件达标并覆盖十二拍后，可以保存正式作品。"
            };
        }

        internal static RehearsalScoreResult EvaluateRhythm(IReadOnlyList<PlacedDrum> arrangement,
                                                            RehearsalChallenge challenge)
        {
            var gapA = EvaluateGap(arrangement, challenge.EditableRegions[0]);
            var gapB = EvaluateGap(arrangement, challenge.EditableRegions[1]);
            // 军鼓每次落在整拍，随后半拍应有一击踩镲。
            bool groove = gapA.Matches && gapB.Matches;
            foreach (float snareBeat in gapA.Beats)
                groove &= gapB.Beats.Exists(hatBeat => SameBeat(hatBeat, snareBeat + 0.5f));

            int position = (gapA.Covered ? 5 : 0) + (gapB.Covered ? 5 : 0);
            int score = position + (gapA.Matches ? 35 : 0) + (gapB.Matches ? 35 : 0) + (groove ? 20 : 0);
            bool passed = challenge.IsComplete(score);
            return new RehearsalScoreResult
            {
                Score = score,
                ChallengeTitle = challenge.Title,
                ChallengePassed = passed,
                Grade = passed ? "完整修复" : gapA.Matches || gapB.Matches ? "部分修复" : "继续调整",
                Feedback = "位置与长度    " + position + " / 10\n"
                    + "Gap A 军鼓    " + Describe(gapA.Matches, 35) + "\n"
                    + "Gap B 踩镲    " + Describe(gapB.Matches, 35) + "\n"
                    + "节奏配合    " + Describe(groove, 20),
                Advice = !gapA.Matches && !gapB.Matches ? "先分别双击试听军鼓和踩镲参考片段。"
                    : !gapA.Matches ? "注意军鼓在小节第 2、4 拍各响一次。"
                    : !gapB.Matches ? "注意踩镲落在每个整拍之后的半拍。"
                    : "军鼓和踩镲已接回录音。",
                Progress = passed ? "关卡已完成。" : "修复两处缺口并让节奏配合，才能通关。"
            };
        }

        private static string Describe(bool matches, int weight)
        {
            return matches ? "正确  " + weight + " / " + weight : "不正确  0 / " + weight;
        }

        private static (bool Covered, bool Matches, List<float> Beats) EvaluateGap(
            IReadOnlyList<PlacedDrum> arrangement, RehearsalRegion region)
        {
            var clips = new List<PlacedDrum>();
            foreach (var clip in arrangement)
                if (!clip.Locked && clip.LaneIndex == region.LaneIndex)
                    clips.Add(clip);
            clips.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));

            float end = region.StartBeat;
            bool covered = clips.Count > 0;
            bool soundMatches = true;
            var beats = new List<float>();
            foreach (var clip in clips)
            {
                covered &= SameBeat(clip.StartBeat, end) &&
                    region.Contains(clip.LaneIndex, clip.StartBeat, clip.DurationBeats);
                end = clip.StartBeat + clip.DurationBeats;
                var points = clip.Character.PlaybackPoints;
                soundMatches &= points.Length > 0 && clip.Character.Sample.Definition.Type == region.Target.Sound;
                foreach (var point in points)
                    beats.Add(clip.StartBeat + point.Beat);
            }
            covered &= SameBeat(end, region.EndBeat);
            beats.Sort();
            var target = region.Target.Rhythm.Points;
            bool matches = covered && soundMatches && beats.Count == target.Length;
            for (int i = 0; matches && i < beats.Count; i++)
                matches = SameBeat(beats[i], region.StartBeat + target[i].Beat);
            return (covered, matches, beats);
        }

        private static bool SameBeat(float a, float b) { return Math.Abs(a - b) < 0.001f; }

        public static RehearsalScoreResult Evaluate(DrumTrackBoard board)
        {
            var music = Measure(board.Placed);
            bool opening = music.Covered[0] >= 3.99f && music.Lanes[0].Count == 1 &&
                music.Events[0] >= 2 && music.Events[0] <= 6 &&
                music.IntegerOpeningEvents >= music.Events[0] * 0.75f;
            bool development = music.Covered[1] >= 3.99f && music.Lanes[1].Count >= 2 &&
                music.Sounds[1].Count >= 2 && music.Lanes[1].Count > music.Lanes[0].Count;
            bool density = music.TotalCovered >= 15.99f && music.SoundsAll.Count >= 2 &&
                music.Events[2] > 0 && music.TotalEvents >= 8 && music.TotalEvents <= 48 && music.PeakPerBeat <= 5;
            int score = (opening ? 33 : 0) + (development ? 33 : 0) + (density ? 34 : 0);
            string openingAdvice = music.Covered[0] < 3.99f ? "让开场声部覆盖完整四拍。"
                : music.Lanes[0].Count != 1 ? "开场先保留一条声部。"
                : music.Events[0] < 2 || music.Events[0] > 6 ? "开场四拍保留 2–6 次击打。"
                : "至少四分之三的开场击打应落在整拍。";
            string developmentAdvice = music.Covered[1] < 3.99f ? "先让发展段覆盖完整四拍。"
                : music.Sounds[1].Count < 2 ? "发展段加入第二种音色与声部。"
                : "发展段至少两条声部，并比开场增加层次。";
            string densityAdvice = music.TotalCovered < 15.99f ? "先让声部覆盖完整十六拍。"
                : music.SoundsAll.Count < 2 ? "全曲至少使用两种音色。"
                : music.Events[2] == 0 ? "高潮段 Beat 8–16 也需要声音。"
                : music.TotalEvents < 8 ? "全曲至少安排 8 次击打。"
                : "当前过密：全曲最多 48 击，每拍最多 5 击。";
            return new RehearsalScoreResult
            {
                Score = score, OpeningStableGood = opening, DevelopmentLayeredGood = development, DensityGood = density,
                OpeningAdvice = opening ? "一条声部建立简洁的整拍开场。" : openingAdvice,
                DevelopmentAdvice = development ? "声部和音色比开场更丰富。" : developmentAdvice,
                DensityAdvice = density ? "结构完整，音色与击打密度适当。" : densityAdvice,
                Feedback = "开场稳定    " + Describe(opening, 33) +
                    "\n发展层次    " + Describe(development, 33) +
                    "\n整体密度    " + Describe(density, 34),
                Advice = !opening ? openingAdvice
                    : !development ? developmentAdvice
                    : !density ? densityAdvice
                    : "从简洁开场到多层合奏，第一次排练已成形。"
            };
        }

        internal sealed class MusicStructure
        {
            public readonly int[] Events = new int[3];
            public readonly float[] Covered = new float[3];
            public readonly HashSet<int>[] Lanes = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
            public readonly HashSet<WaveformType>[] Sounds = {
                new HashSet<WaveformType>(), new HashSet<WaveformType>(), new HashSet<WaveformType>() };
            public readonly HashSet<WaveformType> SoundsAll = new HashSet<WaveformType>();
            public int IntegerOpeningEvents;
            public int PeakPerBeat;
            public int TotalEvents => Events[0] + Events[1] + Events[2];
            public float TotalCovered => Covered[0] + Covered[1] + Covered[2];
        }

        internal static MusicStructure Measure(IReadOnlyList<PlacedDrum> arrangement)
        {
            var result = new MusicStructure();
            var intervals = new List<(float Start, float End)>();
            var buckets = new int[16];
            foreach (var clip in arrangement)
            {
                var points = clip.Character.PlaybackPoints;
                if (points.Length == 0) continue;
                intervals.Add((clip.StartBeat, clip.StartBeat + clip.DurationBeats));
                foreach (var point in points)
                {
                    float beat = clip.StartBeat + point.Beat;
                    if (!(beat >= 0 && beat < 16)) continue;
                    int section = beat < 4 ? 0 : beat < 8 ? 1 : 2;
                    result.Events[section]++;
                    result.Lanes[section].Add(clip.LaneIndex);
                    result.Sounds[section].Add(clip.Character.Sample.Definition.Type);
                    result.SoundsAll.Add(clip.Character.Sample.Definition.Type);
                    if (section == 0 && SameBeat(beat, (float)Math.Round(beat))) result.IntegerOpeningEvents++;
                    int index = (int)Math.Floor(beat);
                    buckets[index]++;
                    result.PeakPerBeat = Math.Max(result.PeakPerBeat, buckets[index]);
                }
            }
            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
            float[] edges = { 0f, 4f, 8f, 16f };
            for (int section = 0; section < 3; section++)
            {
                float end = edges[section];
                foreach (var interval in intervals)
                {
                    float start = Math.Max(edges[section], interval.Start);
                    float next = Math.Min(edges[section + 1], interval.End);
                    result.Covered[section] += Math.Max(0, next - Math.Max(end, start));
                    end = Math.Max(end, next);
                }
            }
            return result;
        }
    }
}
