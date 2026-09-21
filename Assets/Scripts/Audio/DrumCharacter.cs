using System.Collections.Generic;
using WaveTeam.Core;

namespace WaveTeam.Audio
{
    /// <summary>
    /// 鼓原型里的一名角色：一个名字 + 随机绑定的一种鼓音色（波形 + AudioClip）。
    /// 波形即人格：性格名词跟随当前波形。
    /// </summary>
    public sealed class DrumCharacter
    {
        public readonly string Name;
        public readonly DrumSample Sample; // 音色源：基准波形定义 + AudioClip + 文件名

        /// <summary>当前波形（原型期 = 基准定义；练习/磨合改变波形时再替换）。</summary>
        public WaveformDefinition Waveform { get { return Sample.Definition; } }

        /// <summary>性格名词（跟随波形）。</summary>
        public string Noun { get { return Sample.Definition.Noun; } }

        public DrumCharacter(string name, DrumSample sample)
        {
            Name = name;
            Sample = sample;
        }
    }

    /// <summary>
    /// 角色分队的随机分配器：每次启动把鼓样本洗牌分配给角色（C4）。
    /// </summary>
    public static class Roster
    {
        /// <summary>默认角色名（沿用现有 6 名角色）。</summary>
        public static readonly IReadOnlyList<string> DefaultNames = new List<string>
        {
            "阿宁", "小川", "老周", "石头", "小雨", "神秘嘉宾"
        };

        /// <summary>
        /// 给每个名字随机分配一个鼓样本；样本足够时「不重复」（每个鼓音色至多一名角色），
        /// 名字多于样本时才允许重复。每次调用结果不同。
        /// </summary>
        public static List<DrumCharacter> Build(
            IReadOnlyList<string> names, IReadOnlyList<DrumSample> samples, System.Random rng = null)
        {
            rng = rng ?? new System.Random();
            var pool = new List<DrumSample>(samples);
            var result = new List<DrumCharacter>();

            foreach (var name in names)
            {
                DrumSample sample;
                if (pool.Count > 0)
                {
                    int i = rng.Next(pool.Count);
                    sample = pool[i];
                    pool.RemoveAt(i);
                }
                else
                {
                    sample = samples[rng.Next(samples.Count)];
                }
                result.Add(new DrumCharacter(name, sample));
            }
            return result;
        }
    }
}
