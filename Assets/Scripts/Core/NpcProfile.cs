using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 排练室里一个角色的静态资料 —— 走近后信息面板上显示的就是这几项。
    /// 名字沿用 Roster.DefaultNames，这样排练室里遇到的人和音轨板里能拖进轨道的角色是同一批。
    /// </summary>
    public sealed class NpcProfile
    {
        public readonly string Name;
        public readonly Trait Trait;              // 性格（= 波形形状）
        public readonly int Level;                // 等级
        public readonly WaveformType Waveform;    // 目前波形（= 音色）
        public readonly string Task;              // 正在进行的任务
        public readonly string Dialogue;

        public NpcProfile(string name, Trait trait, int level, WaveformType waveform, string task, string dialogue)
        {
            Name = name;
            Trait = trait;
            Level = level;
            Waveform = waveform;
            Task = task;
            Dialogue = dialogue;
        }

        /// <summary>波形的静态定义（乐器名 + 性格名词 + 形状参数）。</summary>
        public WaveformDefinition WaveformDef
        {
            get { return WaveformCatalog.Get(Waveform); }
        }

        /// <summary>面板上用的一行「性格」文案，比如「急躁（尖峰）」。</summary>
        public string TraitText
        {
            get { return Trait.DisplayName(); }
        }

        /// <summary>面板上用的一行「波形」文案，比如「底鼓 · 沉稳」。</summary>
        public string WaveformText
        {
            get
            {
                var def = WaveformDef;
                return def == null ? Waveform.ToString() : def.Name + " · " + def.Noun;
            }
        }
    }

    /// <summary>
    /// 排练室里出场的角色名册。等级 / 波形 / 任务现在都是写死的（还没有成长系统），
    /// 以后接上存档或随机生成时，把这里换成数据源即可，调用方不用动。
    /// </summary>
    public static class NpcRoster
    {
        public static readonly IReadOnlyList<NpcProfile> All = new List<NpcProfile>
        {
            new NpcProfile("阿宁",     Trait.Calm,      12, WaveformType.Kick,   "给底鼓找一段稳得住的开场","我的底鼓在第一拍和第三拍响起。先让我独奏四拍，给大家一个稳定的开场。"),
            new NpcProfile("小川",     Trait.Flexible,   7, WaveformType.HiHat,  "试着把踩镲的密度再推一档","我喜欢在整拍之间加入踩镲。发展段请另一位伙伴加入，高潮再让我进来，层次就会慢慢增加。"),
            new NpcProfile("老周",     Trait.Stubborn,  18, WaveformType.Tom,    "守着通鼓的老节奏，不肯改","我还是觉得原来的通鼓节奏最好，没必要为了别人一直改。"),
            new NpcProfile("石头",     Trait.Impatient,  5, WaveformType.Snare,  "给合奏加入军鼓回应", "我的军鼓强调第二拍和第四拍。音乐要贯穿十六拍，不过别把所有声音都挤在开头。"),
            new NpcProfile("小雨",     Trait.Flexible,   9, WaveformType.Shaker, "找一种更细碎的沙锤颗粒","我想让沙锤轻一点，不抢其他人的声音，但又不能完全听不见。"),
            new NpcProfile("神秘嘉宾", Trait.Calm,      30, WaveformType.Crash,  "还没透露这次要打什么","先听听其他人的想法吧。真正重要的，也许不是谁的声音最大。"),
        };

        /// <summary>面板右上角那几个功能按钮的占位文案（功能本身还没设计）。</summary>
        public static readonly IReadOnlyList<string> ActionPlaceholders = new List<string>
        {
            "聊天", "编队", "赠礼",
        };
    }
}
