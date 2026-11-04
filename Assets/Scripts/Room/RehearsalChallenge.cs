using System;
using System.Collections.Generic;
using WaveTeam.Audio;
using WaveTeam.Core;

namespace WaveTeam.Room
{
    public readonly struct RehearsalRegion
    {
        public readonly int LaneIndex;
        public readonly float StartBeat;
        public readonly float EndBeat;

        public RehearsalRegion(int laneIndex, float startBeat, float endBeat)
        {
            LaneIndex = laneIndex;
            StartBeat = startBeat;
            EndBeat = endBeat;
        }

        public bool Contains(int laneIndex, float startBeat, float duration)
        {
            return laneIndex == LaneIndex && startBeat >= StartBeat && startBeat + duration <= EndBeat;
        }
    }

    public sealed class RehearsalPattern
    {
        public readonly string Name;
        public readonly WaveformType Sound;
        public readonly RhythmPattern Rhythm;

        public RehearsalPattern(string name, WaveformType sound, float duration, params float[] beats)
        {
            Name = name;
            Sound = sound;
            var points = Array.ConvertAll(beats, beat => new RhythmPoint(beat, 0, true));
            Rhythm = new RhythmPattern(points, duration);
        }

        public DrumCharacter CreateCharacter(IReadOnlyList<DrumSample> samples)
        {
            foreach (var sample in samples)
                if (sample.Definition.Type == Sound && sample.Clip != null)
                    return new DrumCharacter(Name, sample, Rhythm, HexCategory.Percussion, fixedRhythm: true);
            return null;
        }
    }

    public sealed class RehearsalChallenge
    {
        public const string MissingBeatId = "MissingBeat";
        public const string NextChallengeId = "Chapter3";

        public static readonly RehearsalChallenge First = new RehearsalChallenge(
            "Rehearsal1", "本轮排练目标", "开场：需要稳定\n发展：增加层次\n整体：避免过度拥挤", 66);

        // 拍数用时间轴编号。四拍小节内的第 2、4 拍对应偏移 1、3。
        private static readonly RehearsalPattern Backbeat =
            new RehearsalPattern("军鼓 A", WaveformType.Snare, 4f, 1f, 3f);
        private static readonly RehearsalPattern Kick =
            new RehearsalPattern("底鼓", WaveformType.Kick, 4f, 0f, 2f);
        private static readonly RehearsalPattern HiHat =
            new RehearsalPattern("踩镲", WaveformType.HiHat, 4f, 0.5f, 1.5f, 2.5f, 3.5f);

        public static readonly RehearsalChallenge MissingBeat = new RehearsalChallenge(
            MissingBeatId, "The Missing Beat",
            "丢失的节拍 · 补齐 Layer 2 的黄色四拍缺口。\n双击锁定波形试听，找出能延续军鼓节奏的片段。", 100,
            new RehearsalRegion(1, 4f, 8f), Backbeat, new RhythmScoring(30, 40, 30,
                "填满 Layer 2 的黄色四拍缺口。",
                "听听缺口两侧。军鼓在小节第 2、4 拍各响一次。",
                "拍点正确。试试与缺口两侧相同的军鼓音色。"),
            new[]
            {
                new Preset(Kick, 0, 0f), new Preset(Kick, 0, 4f),
                new Preset(Kick, 0, 8f), new Preset(Kick, 0, 12f),
                new Preset(Backbeat, 1, 0f), new Preset(Backbeat, 1, 8f), new Preset(Backbeat, 1, 12f),
                new Preset(HiHat, 2, 8f), new Preset(HiHat, 2, 12f)
            },
            new[]
            {
                Backbeat,
                new RehearsalPattern("军鼓 B", WaveformType.Snare, 4f, 0f, 2f),
                new RehearsalPattern("踩镲 C", WaveformType.HiHat, 4f, 1f, 3f)
            });

        public readonly string Id;
        public readonly string Title;
        public readonly string Hint;
        public readonly int PassingScore;
        public readonly RehearsalRegion? EditableRegion;
        public readonly RehearsalPattern Target;
        public readonly RhythmScoring Scoring;
        public readonly IReadOnlyList<Preset> Presets;
        public readonly IReadOnlyList<RehearsalPattern> Candidates;

        private RehearsalChallenge(string id, string title, string hint, int passingScore,
            RehearsalRegion? editableRegion = null, RehearsalPattern target = null,
            RhythmScoring scoring = default,
            Preset[] presets = null, RehearsalPattern[] candidates = null)
        {
            Id = id;
            Title = title;
            Hint = hint;
            PassingScore = passingScore;
            EditableRegion = editableRegion;
            Target = target;
            Scoring = scoring;
            Presets = presets ?? Array.Empty<Preset>();
            Candidates = candidates ?? Array.Empty<RehearsalPattern>();
        }

        public bool IsComplete(int score) { return score >= PassingScore; }

        public readonly struct RhythmScoring
        {
            public readonly int Coverage;
            public readonly int Rhythm;
            public readonly int Continuity;
            public readonly string CoverageHint;
            public readonly string RhythmHint;
            public readonly string ContinuityHint;

            public RhythmScoring(int coverage, int rhythm, int continuity,
                                 string coverageHint, string rhythmHint, string continuityHint)
            {
                Coverage = coverage;
                Rhythm = rhythm;
                Continuity = continuity;
                CoverageHint = coverageHint;
                RhythmHint = rhythmHint;
                ContinuityHint = continuityHint;
            }
        }

        public readonly struct Preset
        {
            public readonly RehearsalPattern Pattern;
            public readonly int LaneIndex;
            public readonly float StartBeat;
            public readonly bool Locked;

            public Preset(RehearsalPattern pattern, int laneIndex, float startBeat)
            {
                Pattern = pattern;
                LaneIndex = laneIndex;
                StartBeat = startBeat;
                Locked = true;
            }
        }
    }
}
