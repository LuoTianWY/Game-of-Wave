using System;
using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 单个已放鼓的存档条目：波形类型 + 起始拍。
    /// 存波形类型而非角色名——角色名每次启动随机分配，波形类型才稳定可复现。
    /// </summary>
    [Serializable]
    public sealed class PlacedDrumEntry
    {
        public string waveformType;
        public float startBeat;
    }

    /// <summary>鼓轨编排存档（供 JsonUtility 序列化）。</summary>
    [Serializable]
    public sealed class BoardSaveData
    {
        public int trackLengthBeats;
        public List<PlacedDrumEntry> drums = new List<PlacedDrumEntry>();
    }
}
