using System;
using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.Audio
{
    /// <summary>一条「性格词条 → 音乐表现」映射（trait_music.json 里 entries 的一行）。</summary>
    [Serializable]
    public sealed class TraitMusicEntry
    {
        public string id;       // 稳定键（代码查表用）
        public string name;     // 性格词条（中文展示）
        public string trait;    // 波形形状（占位：未来由「形状文件」解析，暂时自由填形状名）
        public string waveform; // 音色键（当前含鼓类九种，后续扩充、不限鼓）
        public string rhythm;   // 节奏型：FourOnFloor / Offbeat / Backbeat
        public int bpm;         // 速度（BPM）
        public int level;       // 纵向层 -2..+2
        public string color;    // 颜色 #RRGGBB
        public string desc;     // 描述（仅供人看，程序忽略）

        /// <summary>节奏型 → RhythmPattern；未知/空则回落 FourOnFloor。</summary>
        public RhythmPattern RhythmValue()
        {
            if (string.Equals(rhythm, "Offbeat", StringComparison.OrdinalIgnoreCase)) return RhythmPattern.Offbeat();
            if (string.Equals(rhythm, "Backbeat", StringComparison.OrdinalIgnoreCase)) return RhythmPattern.Backbeat();
            return RhythmPattern.FourOnFloor();
        }

        /// <summary>颜色 → Color（#RRGGBB）；解析失败则返回灰色。</summary>
        public Color ColorValue()
        {
            Color c;
            return ColorUtility.TryParseHtmlString(color, out c) ? c : Color.gray;
        }
    }

    /// <summary>性格词条映射表的根对象（trait_music.json 的反序列化目标）。</summary>
    [Serializable]
    public sealed class TraitMusicTable
    {
        public TraitMusicEntry[] entries;

        /// <summary>按 id 找一条词条；找不到返回 null。</summary>
        public TraitMusicEntry Find(string id)
        {
            if (entries == null || string.IsNullOrEmpty(id)) return null;
            foreach (var e in entries)
                if (e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }
    }

    /// <summary>从 Resources/Characters/trait_music.json 读取性格→音乐映射。</summary>
    public static class TraitMusic
    {
        public const string ResourcePath = "Characters/trait_music";

        public static TraitMusicTable Load()
        {
            var text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
            {
                Debug.LogWarning("[波形小队] 未找到性格映射表（" + ResourcePath + "）。");
                return new TraitMusicTable();
            }
            try
            {
                return JsonUtility.FromJson<TraitMusicTable>(text.text) ?? new TraitMusicTable();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[波形小队] 性格映射表解析失败：" + e.Message);
                return new TraitMusicTable();
            }
        }
    }
}
