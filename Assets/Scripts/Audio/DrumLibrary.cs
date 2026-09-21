using System.Collections.Generic;
using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.Audio
{
    /// <summary>
    /// 一个已加载的鼓音色样本：波形定义 + 音频片段（对应波形映射表.md 的一行）。
    /// </summary>
    public sealed class DrumSample
    {
        public readonly WaveformDefinition Definition;
        public readonly AudioClip Clip;
        public readonly string FileName;

        public DrumSample(WaveformDefinition definition, AudioClip clip, string fileName)
        {
            Definition = definition;
            Clip = clip;
            FileName = fileName;
        }
    }

    /// <summary>
    /// 扫描 Resources/Audio/drums 下的所有 ogg，按文件名解析波形类型，
    /// 建立「文件名 → 波形 + 音效」映射（C3）。
    /// </summary>
    public static class DrumLibrary
    {
        public const string ResourcePath = "Audio/drums";

        /// <summary>扫描并返回所有可识别的鼓样本；无法解析文件名的会被跳过并告警。</summary>
        public static List<DrumSample> Load()
        {
            var result = new List<DrumSample>();
            var clips = Resources.LoadAll<AudioClip>(ResourcePath);
            if (clips == null) return result;

            foreach (var clip in clips)
            {
                WaveformType type;
                if (!TryParse(clip.name, out type))
                {
                    Debug.LogWarning("[波形小队] 无法识别的鼓音色文件名：" + clip.name + "（请对照波形映射表.md 命名）");
                    continue;
                }
                var def = WaveformCatalog.Get(type);
                if (def == null) continue;
                result.Add(new DrumSample(def, clip, clip.name));
            }
            return result;
        }

        /// <summary>
        /// 文件名 → 波形类型。忽略大小写，并忽略 '_' 与 '-'，
        /// 因此 kick / hi_hat / hi-hat / HiHat 都能匹配到对应类型。
        /// </summary>
        public static bool TryParse(string fileName, out WaveformType type)
        {
            type = default(WaveformType);
            if (string.IsNullOrEmpty(fileName)) return false;

            string norm = fileName.ToLowerInvariant()
                .Replace("_", "").Replace("-", "").Trim();

            foreach (WaveformType t in System.Enum.GetValues(typeof(WaveformType)))
            {
                if (t.ToString().ToLowerInvariant() == norm)
                {
                    type = t;
                    return true;
                }
            }
            return false;
        }
    }
}
