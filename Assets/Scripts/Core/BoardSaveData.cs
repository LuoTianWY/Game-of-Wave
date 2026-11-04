using System;
using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 单个已放鼓的存档条目：波形类型、拍区间和轨道。
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
    }

    [Serializable]
    public struct SavedRhythmPoint
    {
        public float beat;
        public int level;
        public bool accent;
    }

    /// <summary>鼓轨编排存档（供 JsonUtility 序列化）。</summary>
    [Serializable]
    public sealed class BoardSaveData
    {
        public int trackLengthBeats;
        public string challengeId;
        public float bpm;
        public bool challengeCompleted;
        public string unlockedChallengeId;
        public List<PlacedDrumEntry> drums = new List<PlacedDrumEntry>();
    }
}
