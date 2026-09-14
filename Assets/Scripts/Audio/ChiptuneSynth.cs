using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.Audio
{
    /// <summary>
    /// 极简 chiptune 合成器：程序化生成方波/正弦音符，无需音频资源文件。
    /// 急躁/固执 → 方波（硬），冷静/灵活 → 正弦（软）。
    /// </summary>
    public sealed class ChiptuneSynth : MonoBehaviour
    {
        private AudioSource _source;
        private const int SampleRate = 44100;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        /// <summary>按音高与特质播放一个音符，持续 seconds 秒。</summary>
        public void PlayNote(int pitch, Trait trait, float seconds)
        {
            float freq = PitchToFrequency(pitch);
            AudioClip clip = GenerateClip(freq, seconds, trait);
            _source.PlayOneShot(clip, 0.4f);
            Destroy(clip, seconds + 0.1f); // 用完即毁，避免积压
        }

        private static float PitchToFrequency(int pitch)
        {
            // 以 A4=440Hz 为基准，音高每 +1 升一个半音
            return 440f * Mathf.Pow(2f, pitch / 12f);
        }

        private static AudioClip GenerateClip(float freq, float seconds, Trait trait)
        {
            int samples = Mathf.Max(1, (int)(seconds * SampleRate));
            var data = new float[samples];
            bool square = trait == Trait.Impatient || trait == Trait.Stubborn;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, (samples - i) / (SampleRate * 0.05f)); // 简单淡出
                float v = square
                    ? (Mathf.Sin(2f * Mathf.PI * freq * t) >= 0f ? 1f : -1f)
                    : Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = v * 0.35f * env;
            }
            var clip = AudioClip.Create("note", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
