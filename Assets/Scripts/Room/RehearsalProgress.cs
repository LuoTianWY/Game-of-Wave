using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WaveTeam.Audio;
using WaveTeam.Core;
using WaveTeam.UI;

namespace WaveTeam.Room
{
    public static class RehearsalProgress
    {
        // 旧 Completed.Rehearsal1 也曾由普通通过写入，不能用于满分门槛。
        private const string PerfectFirstKey = "WaveTeam.Rehearsal.Level1.Perfect";
        private const string IntroductionKey = "WaveTeam.Rehearsal.MissingBeat.IntroductionSeen.V2";
        private const string CurrentChallengeKey = "WaveTeam.Rehearsal.CurrentChallengeId";
        private const string TalkedKey = "WaveTeam.Rehearsal.Talked.";
        private const string FirstPassedKey = "WaveTeam.Rehearsal.Level1.Passed";
        private const string ThirdIntroductionKey = "WaveTeam.Rehearsal.OurSound.IntroductionSeen";
        private const string EndingKey = "WaveTeam.Rehearsal.OurFirstSong.EndingSeen";
        public const string MissingBeatWorkFile = "missing_beat_arrangement.json";
        public const string MissingBeatDraftFile = "missing_beat_draft.json";
        public const string OurSoundDraftFile = "our_sound_draft.json";
        private const string LibraryFile = "song_library.json";

        public static bool IsFirstComplete => PlayerPrefs.GetInt(PerfectFirstKey) == 1;
        public static bool Level2IntroductionSeen => PlayerPrefs.GetInt(IntroductionKey) == 1;
        public static bool Level2Unlocked => IsFirstComplete && Level2IntroductionSeen;
        public static bool Level2Completed => LoadCompletedMissingBeat() != null;
        public static string CurrentChallengeId => PlayerPrefs.GetString(CurrentChallengeKey, RehearsalChallenge.First.Id);
        public static bool FirstPassed => PlayerPrefs.GetInt(FirstPassedKey) == 1 || IsFirstComplete;
        public static bool HasTalked(string name) => PlayerPrefs.GetInt(TalkedKey + name) == 1;
        public static bool ReadyForFirst => HasTalked("阿宁") && HasTalked("小川") && HasTalked("石头");
        public static bool ThirdIntroductionSeen => PlayerPrefs.GetInt(ThirdIntroductionKey) == 1;
        public static bool Level3Unlocked => Level2Unlocked && Level2Completed && ThirdIntroductionSeen;
        public static bool Level3Completed => LoadSongs().Count > 0;
        public static bool EndingSeen => PlayerPrefs.GetInt(EndingKey) == 1;

        internal static void CompleteEnding()
        {
            if (!Level3Completed || EndingSeen) return;
            PlayerPrefs.SetInt(EndingKey, 1);
            PlayerPrefs.Save();
        }

        internal static void CompleteThirdIntroduction()
        {
            if (!Level2Unlocked || !Level2Completed || ThirdIntroductionSeen) return;
            PlayerPrefs.SetInt(ThirdIntroductionKey, 1);
            PlayerPrefs.Save();
        }

        internal static void RecordTalk(string name)
        {
            PlayerPrefs.SetInt(TalkedKey + name, 1);
            PlayerPrefs.Save();
        }

        internal static void RecordFirstScore(int score)
        {
            if (score >= 66) PlayerPrefs.SetInt(FirstPassedKey, 1);
            PlayerPrefs.Save();
            if (score != 100 || IsFirstComplete) return;
            PlayerPrefs.SetInt(PerfectFirstKey, 1);
            PlayerPrefs.Save();
        }

        internal static void CompleteIntroduction()
        {
            if (!IsFirstComplete || Level2IntroductionSeen) return;
            PlayerPrefs.SetInt(IntroductionKey, 1);
            PlayerPrefs.Save();
        }

        internal static void SelectChallenge(string id)
        {
            if (CurrentChallengeId == id) return;
            PlayerPrefs.SetString(CurrentChallengeKey, id);
            PlayerPrefs.Save();
        }

        public static BoardSaveData LoadCompletedMissingBeat()
        {
            var work = Read(MissingBeatWorkFile);
            return work != null && work.challengeCompleted ? work : null;
        }

        public static bool IsThirdUnlocked
        {
            get
            {
                return Level3Unlocked;
            }
        }

        internal static BoardSaveData Read(string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<BoardSaveData>(File.ReadAllText(path));
                if (data != null && (data.challengeId == RehearsalChallenge.MissingBeatId ||
                    data.challengeId == RehearsalChallenge.First.Id || data.challengeId == RehearsalChallenge.NextChallengeId) &&
                    data.trackLengthBeats == DrumTrackBoard.TrackLengthBeats && data.drums != null)
                    return data;
                Debug.LogWarning("[波形小队] 编排存档格式不匹配：" + path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Debug.LogWarning("[波形小队] 编排存档读取失败：" + e.Message);
            }
            return null;
        }

        internal static bool TryWrite<T>(string fileName, T data, out string path)
        {
            path = Path.Combine(Application.persistentDataPath, fileName);
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[波形小队] 编排保存失败：" + e.Message);
                return false;
            }
        }

        public static IReadOnlyList<BoardSaveData> LoadSongs()
        {
            if (!TryReadLibrary(out var library)) return Array.Empty<BoardSaveData>();
            return library.works.FindAll(work => work != null &&
                work.challengeId == RehearsalChallenge.NextChallengeId && work.challengeCompleted && work.drums != null);
        }

        private static bool TryReadLibrary(out SongLibraryData library)
        {
            library = new SongLibraryData();
            string path = Path.Combine(Application.persistentDataPath, LibraryFile);
            if (!File.Exists(path)) return true;
            try
            {
                library = JsonUtility.FromJson<SongLibraryData>(File.ReadAllText(path));
                return library?.works != null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Debug.LogWarning("[波形小队] 曲库读取失败：" + e.Message);
                return false;
            }
        }

        internal static bool SaveSong(BoardSaveData work, string name)
        {
            if (!work.challengeCompleted || work.challengeId != RehearsalChallenge.NextChallengeId ||
                string.IsNullOrWhiteSpace(name) || !TryReadLibrary(out var library)) return false;
            work.songName = name.Trim().Replace("\n", " ").Replace("\r", " ");
            if (work.songName.Length > 48) work.songName = work.songName.Substring(0, 48);
            work.songId = Guid.NewGuid().ToString("N");
            work.createdAtUtc = DateTime.UtcNow.ToString("O");
            library.works.Add(work);
            return TryWrite(LibraryFile, library, out _);
        }

        internal static bool RestoreEdits(DrumTrackBoard board, BoardSaveData data, IReadOnlyList<DrumSample> samples)
        {
            if (data == null) return true;
            bool restored = true;
            var seams = new List<PlacedDrum>();
            foreach (var entry in data.drums)
            {
                if (entry == null || entry.locked) continue; // 锁定内容由关卡定义生成一次
                if (!TryRestoreCharacter(entry, samples, out var character) ||
                    board.RestoreEditable(character, entry.startBeat, entry.LaneIndex) is not PlacedDrum clip)
                {
                    Debug.LogWarning("[波形小队] 跳过无法恢复的编排片段。");
                    restored = false;
                    continue;
                }
                if (entry.connectedToNext) seams.Add(clip);
            }
            foreach (var clip in seams) board.RestoreConnection(clip);
            return restored;
        }

        internal static bool TryRestoreCharacter(PlacedDrumEntry entry, IReadOnlyList<DrumSample> samples,
                                                out DrumCharacter character)
        {
            character = null;
            if (!Enum.IsDefined(typeof(WaveKind), entry.waveKind)) return false;
            if (entry.waveKind != WaveKind.Rhythm)
            {
                try
                {
                    character = new DrumCharacter(entry.characterName, entry.waveKind, entry.instrumentId,
                        entry.melodyNotes, entry.durationBeats);
                    return true;
                }
                catch (ArgumentException) { return false; }
            }
            if (!(entry.durationBeats > 0f && entry.durationBeats <= DrumTrackBoard.TrackLengthBeats) ||
                string.IsNullOrEmpty(entry.characterName) || entry.rhythmPoints == null || entry.rhythmPoints.Length == 0 ||
                !Enum.IsDefined(typeof(HexCategory), entry.category))
                return false;

            var sample = default(DrumSample);
            foreach (var candidate in samples)
            {
                if (candidate.FileName != entry.sampleName || candidate.Clip == null) continue;
                sample = candidate;
                break;
            }
            if (sample == null) return false;

            var points = new RhythmPoint[entry.rhythmPoints.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var saved = entry.rhythmPoints[i];
                if (!(saved.beat >= 0f && saved.beat < entry.durationBeats)) return false;
                points[i] = new RhythmPoint(saved.beat, saved.level, saved.accent);
            }
            character = new DrumCharacter(entry.characterName, sample,
                new RhythmPattern(points, entry.durationBeats), entry.category, fixedRhythm: true);
            return true;
        }
    }
}
