using UnityEngine;

namespace WaveTeam.Audio
{
    /// <summary>
    /// 播放用户导入的 BGM（Resources/Audio/bgm）。统一节奏基准：120 BPM、4/4 拍。
    /// 用 AudioSource.time 读取当前播放位置，采样级精确，避免 Time.deltaTime 累积误差。
    /// </summary>
    public sealed class BgmPlayer : MonoBehaviour
    {
        public const string ResourcePath = "Audio/bgm";
        public const float Bpm = 120f;
        public const int BeatsPerBar = 4;

        private AudioSource _source;
        private AudioClip _clip;

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
        }

        public void Play()
        {
            if (_clip == null) return;
            _source.clip = _clip;
            _source.Play();
        }

        /// <summary>当前播放到的秒数（未加载 BGM 时返回 -1，由调用方回退到程序计时）。</summary>
        public float Time { get { return _clip == null ? -1f : _source.time; } }
    }
}
