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
        private const string CompletionKey = "WaveTeam.Rehearsal.Completed.";
        public const string MissingBeatWorkFile = "missing_beat_arrangement.json";
        public const string MissingBeatDraftFile = "missing_beat_draft.json";

        public static bool IsFirstComplete => PlayerPrefs.GetInt(CompletionKey + RehearsalChallenge.First.Id) == 1;

        public static void CompleteFirst()
        {
            PlayerPrefs.SetInt(CompletionKey + RehearsalChallenge.First.Id, 1);
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
                return LoadCompletedMissingBeat()?.unlockedChallengeId == RehearsalChallenge.NextChallengeId;
            }
        }

        internal static BoardSaveData Read(string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<BoardSaveData>(File.ReadAllText(path));
                if (data?.challengeId == RehearsalChallenge.MissingBeatId &&
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

        internal static bool TryWrite(string fileName, BoardSaveData data, out string path)
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

        internal static void RestoreEdits(DrumTrackBoard board, BoardSaveData data, IReadOnlyList<DrumSample> samples)
        {
            if (data == null) return;
            var seams = new List<PlacedDrum>();
            foreach (var entry in data.drums)
            {
                if (entry == null || entry.locked) continue; // 锁定内容由关卡定义生成一次
                if (!TryRestoreCharacter(entry, samples, out var character) ||
                    board.RestoreEditable(character, entry.startBeat, entry.LaneIndex) is not PlacedDrum clip)
                {
                    Debug.LogWarning("[波形小队] 跳过无法恢复的编排片段。");
                    continue;
                }
                if (entry.connectedToNext) seams.Add(clip);
            }
            foreach (var clip in seams) board.RestoreConnection(clip);
        }

        private static bool TryRestoreCharacter(PlacedDrumEntry entry, IReadOnlyList<DrumSample> samples,
                                                out DrumCharacter character)
        {
            character = null;
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
