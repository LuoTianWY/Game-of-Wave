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
        public readonly string Label;
        public readonly RehearsalPattern Target;

        public RehearsalRegion(int laneIndex, float startBeat, float endBeat, string label, RehearsalPattern target)
        {
            LaneIndex = laneIndex;
            StartBeat = startBeat;
            EndBeat = endBeat;
            Label = label;
            Target = target;
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
        public const int MissingBeatRevision = 2;
        public const string NextChallengeId = "Chapter3";
        public static readonly RehearsalChallenge OurOwnSound = new RehearsalChallenge(
            NextChallengeId, "LEVEL 03 — OUR OWN SOUND",
            "属于我们的声音 · 开场一层整拍，发展增至两层，高潮比发展更密。\n至少两种音色，覆盖至少十二拍，每拍最多 5 击。\n合格创作 90 分；完整覆盖十六拍可获 100 分。", 90, useBackgroundMusic: false);

        public static readonly RehearsalChallenge First = new RehearsalChallenge(
            "Rehearsal1", "LEVEL 01 — FIRST REHEARSAL",
            "第一次排练 · 开场 0–4 拍简洁稳定；发展 4–8 拍增至两层。\n完整安排 16 拍，至少两种音色，保持适当密度。\n100 分后返回排练室找小川。", 66, useBackgroundMusic: false);

        public static List<DrumCharacter> CreateBand(IReadOnlyList<DrumSample> samples)
        {
            var patterns = new[]
            {
                new RehearsalPattern("阿宁", WaveformType.Kick, 4f, 0f, 2f),
                new RehearsalPattern("小川", WaveformType.HiHat, 4f, 0.5f, 1.5f, 2.5f, 3.5f),
                new RehearsalPattern("石头", WaveformType.Snare, 4f, 1f, 3f),
                new RehearsalPattern("神秘嘉宾", WaveformType.Crash, 4f, 0f)
            };
            var band = new List<DrumCharacter>();
            foreach (var pattern in patterns)
            {
                var character = pattern.CreateCharacter(samples);
                if (character != null) band.Add(character);
            }
            return band;
        }

        // 拍数用时间轴编号。四拍小节内的第 2、4 拍对应偏移 1、3。
        private static readonly RehearsalPattern Backbeat =
            new RehearsalPattern("军鼓 A", WaveformType.Snare, 4f, 1f, 3f);
        private static readonly RehearsalPattern OffbeatHiHat =
            new RehearsalPattern("踩镲 A", WaveformType.HiHat, 4f, 0.5f, 1.5f, 2.5f, 3.5f);
        private static readonly RehearsalPattern Kick =
            new RehearsalPattern("底鼓", WaveformType.Kick, 4f, 0f, 1.5f, 2.5f);
        private static readonly RehearsalPattern ClimaxHiHat =
            new RehearsalPattern("踩镲", WaveformType.HiHat, 4f, 0.5f, 1.5f, 2.5f, 3.5f, 3.75f);

        public static readonly RehearsalChallenge MissingBeat = new RehearsalChallenge(
            MissingBeatId, "LEVEL 02 — THE MISSING BEAT",
            "旧录音有两段节奏遗失了。\n通过试听和观察，补全军鼓与踩镲，\n让三条音轨重新形成完整的节奏。", 100,
            new[]
            {
                new RehearsalRegion(1, 4f, 8f, "Gap A · 军鼓", Backbeat),
                new RehearsalRegion(2, 4f, 8f, "Gap B · 踩镲", OffbeatHiHat)
            },
            new[]
            {
                new Preset(Kick, 0, 0f), new Preset(Kick, 0, 4f),
                new Preset(Kick, 0, 8f), new Preset(Kick, 0, 12f),
                new Preset(Backbeat, 1, 0f), new Preset(Backbeat, 1, 8f), new Preset(Backbeat, 1, 12f),
                new Preset(OffbeatHiHat, 2, 8f), new Preset(ClimaxHiHat, 2, 12f)
            },
            new[]
            {
                Backbeat,
                new RehearsalPattern("军鼓 B", WaveformType.Snare, 4f, 0f, 2f),
                new RehearsalPattern("军鼓 C", WaveformType.Snare, 4f, 1f, 2.5f, 3f),
                OffbeatHiHat,
                new RehearsalPattern("踩镲 B", WaveformType.HiHat, 4f, 0f, 1f, 2f, 3f),
                new RehearsalPattern("踩镲 C", WaveformType.HiHat, 4f, 0.5f, 1f, 1.5f, 2.5f, 3f, 3.5f)
            }, useBackgroundMusic: false);

        public readonly string Id;
        public readonly string Title;
        public readonly string Hint;
        public readonly int PassingScore;
        public readonly IReadOnlyList<RehearsalRegion> EditableRegions;
        public bool IsRhythmChallenge => EditableRegions.Count > 0;
        public readonly IReadOnlyList<Preset> Presets;
        public readonly IReadOnlyList<RehearsalPattern> Candidates;
        public readonly bool UseBackgroundMusic;

        private RehearsalChallenge(string id, string title, string hint, int passingScore,
            RehearsalRegion[] editableRegions = null,
            Preset[] presets = null, RehearsalPattern[] candidates = null, bool useBackgroundMusic = true)
        {
            Id = id;
            Title = title;
            Hint = hint;
            PassingScore = passingScore;
            EditableRegions = editableRegions ?? Array.Empty<RehearsalRegion>();
            Presets = presets ?? Array.Empty<Preset>();
            Candidates = candidates ?? Array.Empty<RehearsalPattern>();
            UseBackgroundMusic = useBackgroundMusic;
        }

        public bool IsComplete(int score) { return score >= PassingScore; }

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
