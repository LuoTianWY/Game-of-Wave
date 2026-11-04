using System;
using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 单个已放鼓的存档条目：波形类型、拍区间和轨道。
    /// 存波形类型而非角色名——角色名每次启动随机分配，波形类型才稳定可复现。
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
    }

    /// <summary>鼓轨编排存档（供 JsonUtility 序列化）。</summary>
    [Serializable]
    public sealed class BoardSaveData
    {
        public int trackLengthBeats;
        public List<PlacedDrumEntry> drums = new List<PlacedDrumEntry>();
    }
}
