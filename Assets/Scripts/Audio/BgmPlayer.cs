using System;
using UnityEngine;

namespace WaveTeam.Audio
{
    /// <summary>曲目配置：随 BGM 一起放的 JSON（Resources/Audio/bgm_track.json），内容形如 {"bpm": 120, "beatsPerBar": 4}。</summary>
    [Serializable]
    public sealed class BgmConfig
    {
        public float bpm = 120f;
        public int beatsPerBar = 4;
    }

    /// <summary>
    /// 播放用户导入的 BGM（Resources/Audio/bgm）。节奏基准默认 120 BPM、4/4 拍；
    /// 若存在 Resources/Audio/bgm_track.json 则按曲目配置覆盖 BPM，使角色能跨曲目复用
    /// （每次音效触发时刻 = 拍 × 60/BPM）。
    /// 用 AudioSource.time 读取当前播放位置，采样级精确，避免 Time.deltaTime 累积误差。
    /// </summary>
    public sealed class BgmPlayer : MonoBehaviour
    {
        public const string ResourcePath = "Audio/bgm";
        public const string ConfigPath = "Audio/bgm_track";
        public const float DefaultBpm = 120f;
        public const int BeatsPerBar = 4;

        private AudioSource _source;
        private AudioClip _clip;

        /// <summary>当前曲目 BPM（默认 120，可由 bgm_track.json 覆盖）。</summary>
        public static float Bpm { get; private set; } = DefaultBpm;

        public bool HasClip { get { return _clip != null; } }
        public float BeatSeconds { get { return 60f / Bpm; } }
        public bool IsPlaying { get { return _source != null && _source.isPlaying; } }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _clip = Resources.Load<AudioClip>(ResourcePath);
            if (_clip == null)
            {
                Debug.LogWarning("[波形小队] 未找到 BGM（" + ResourcePath + "）。将用程序内置节拍计时，不发声。");
            }
            LoadConfig();
        }

        private static void LoadConfig()
        {
            var text = Resources.Load<TextAsset>(ConfigPath);
            if (text == null) return; // 无配置 → 保留默认 120
            try
            {
                var cfg = JsonUtility.FromJson<BgmConfig>(text.text);
                if (cfg != null && cfg.bpm > 0f) Bpm = cfg.bpm;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[波形小队] BGM 配置解析失败，沿用默认 120 BPM：" + e.Message);
            }
        }

        public void Play()
        {
            if (_clip == null) return;
            _source.clip = _clip;
            _source.Play();
        }

        /// <summary>立即停止 BGM 播放。</summary>
        public void Stop()
        {
            if (_source != null) _source.Stop();
        }

        /// <summary>当前播放到的秒数（未加载 BGM 时返回 -1，由调用方回退到程序计时）。</summary>
        public float Time { get { return _clip == null ? -1f : _source.time; } }
    }
}
