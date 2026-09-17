using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 波形类型 = 鼓音色种类。每个角色绑定一种波形，由 ogg 文件名决定。
    /// </summary>
    public enum WaveformType
    {
        Kick,     // 底鼓
        Snare,    // 军鼓
        HiHat,    // 踩镲
        Crash,    // 吊镲
        Tom,      // 通鼓
        Ride,     // 叮叮镲
        Clap,     // 拍手
        Shaker,   // 沙锤
        RimShot   // 鼓边
    }

    /// <summary>
    /// 一种波形类型的静态定义：形状参数 + 头/尾端点段 + 性格名词。
    /// 端点模型：波形占固定大小单元，从上到下分 4 段（1=顶 … 4=底）；
    /// 相邻波形「尾端点段 == 头端点段」即接上（契合）。
    /// </summary>
    public sealed class WaveformDefinition
    {
        public readonly WaveformType Type;
        public readonly string Name;      // 乐器名：底鼓/军鼓/…
        public readonly string Noun;      // 性格名词：沉稳/干脆/…
        public readonly int Cycles;       // 周期数（频段）：低≈2，中≈5，高≈8
        public readonly float Attack;     // 起振 0..1（越大越急）
        public readonly float Decay;      // 衰减 0..1（越大越快）
        public readonly float Duration;   // 时值（拍）
        public readonly int HeadSegment;  // 头端点段 1..4
        public readonly int TailSegment;  // 尾端点段 1..4

        public WaveformDefinition(
            WaveformType type, string name, string noun,
            int cycles, float attack, float decay, float duration,
            int headSegment, int tailSegment)
        {
            Type = type;
            Name = name;
            Noun = noun;
            Cycles = cycles;
            Attack = attack;
            Decay = decay;
            Duration = duration;
            HeadSegment = headSegment;
            TailSegment = tailSegment;
        }
    }

    /// <summary>
    /// 波形类型目录：9 种鼓波形定义（对应波形映射表.md）。
    /// </summary>
    public static class WaveformCatalog
    {
        public static readonly WaveformDefinition Kick = new WaveformDefinition(
            WaveformType.Kick, "底鼓", "沉稳", 2, 0.9f, 0.9f, 0.5f, 1, 3);

        public static readonly WaveformDefinition Snare = new WaveformDefinition(
            WaveformType.Snare, "军鼓", "干脆", 5, 1f, 1f, 0.25f, 3, 1);

        public static readonly WaveformDefinition HiHat = new WaveformDefinition(
            WaveformType.HiHat, "踩镲", "跳脱", 8, 1f, 1f, 0.25f, 1, 4);

        public static readonly WaveformDefinition Crash = new WaveformDefinition(
            WaveformType.Crash, "吊镲", "张扬", 7, 0.8f, 0.25f, 1.5f, 4, 2);

        public static readonly WaveformDefinition Tom = new WaveformDefinition(
            WaveformType.Tom, "通鼓", "温厚", 3, 0.5f, 0.5f, 0.5f, 2, 3);

        public static readonly WaveformDefinition Ride = new WaveformDefinition(
            WaveformType.Ride, "叮叮镲", "通透", 6, 0.7f, 0.3f, 1f, 3, 4);

        public static readonly WaveformDefinition Clap = new WaveformDefinition(
            WaveformType.Clap, "拍手", "爽朗", 5, 1f, 0.85f, 0.25f, 4, 1);

        public static readonly WaveformDefinition Shaker = new WaveformDefinition(
            WaveformType.Shaker, "沙锤", "细碎", 9, 0.6f, 0.6f, 0.25f, 2, 2);

        public static readonly WaveformDefinition RimShot = new WaveformDefinition(
            WaveformType.RimShot, "鼓边", "犀利", 5, 1f, 0.95f, 0.25f, 1, 2);

        /// <summary>全部 9 种定义，供随机分配/遍历。</summary>
        public static readonly IReadOnlyList<WaveformDefinition> All =
            new List<WaveformDefinition>
            {
                Kick, Snare, HiHat, Crash, Tom, Ride, Clap, Shaker, RimShot
            };

        public static WaveformDefinition Get(WaveformType type)
        {
            foreach (var def in All)
                if (def.Type == type) return def;
            return null;
        }
    }
}
