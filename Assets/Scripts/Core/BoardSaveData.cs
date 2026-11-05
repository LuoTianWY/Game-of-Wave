using System;
using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 单个已放 Wave 的存档条目：类型、拍区间、轨道和可恢复的演奏事件。
    /// 波形类型和样本名标识音色，节奏点记录本次作品实际发声的事件。
    /// </summary>
    [Serializable]
    public sealed class PlacedDrumEntry
    {
        public string waveformType;
        public float startBeat;
        public float durationBeats;
        public int LaneIndex; // 从上到下；旧单轨 JSON 缺少该字段时为 0
        // 保留旧版 JSON 中从下到上的轨道编号。
        public int track;
        public string characterName;
        public string sampleName;
        public HexCategory category;
        public bool locked;
        public bool connectedToNext;
        public SavedRhythmPoint[] rhythmPoints;
        public WaveKind waveKind; // Missing in old JSON means Rhythm.
        public string instrumentId;
        public MelodyNote[] melodyNotes;
    }

    [Serializable]
    public struct SavedRhythmPoint
    {
        public float beat;
        public int level;
        public bool accent;
    }

    /// <summary>多轨 Wave 编排存档（供 JsonUtility 序列化）。</summary>
    [Serializable]
    public sealed class BoardSaveData
    {
        public int trackLengthBeats;
        public string challengeId;
        public int challengeRevision;
        public float bpm;
        public bool withoutBgm;
        public bool challengeCompleted;
        public string unlockedChallengeId;
        public string songId;
        public string songName;
        public string createdAtUtc;
        public int score;
        public List<PlacedDrumEntry> drums = new List<PlacedDrumEntry>();
    }

    [Serializable]
    public sealed class SongLibraryData
    {
        public List<BoardSaveData> works = new List<BoardSaveData>();
    }
}
