using System.Collections.Generic;
using UnityEngine;
using WaveTeam.Audio;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 演出播放器（C12）：按起始拍排序已放鼓，播放 BGM 与鼓音效；
    /// 播放头越过相邻鼓边界时按契合度触发「共振颤动」；结束后回调契合度文本。
    /// </summary>
    public sealed class PerformancePlayer : MonoBehaviour
    {
        private DrumTrackBoard _board;
        private BgmPlayer _bgm;
        private AudioSource _drumSource;

        private readonly List<PlacedDrum> _order = new List<PlacedDrum>();
        private bool _playing;
        private float _startWall;
        private int _nextPlayIdx;
        private int _trembleIdx;

        public bool IsPlaying { get { return _playing; } }

        /// <summary>演出结束回调：参数为契合度展示文本。</summary>
        public System.Action<string> OnFinished;

        public void Setup(DrumTrackBoard board, BgmPlayer bgm)
        {
            _board = board;
            _bgm = bgm;
        }

        private void Awake()
        {
            _drumSource = gameObject.AddComponent<AudioSource>();
            _drumSource.playOnAwake = false;
            _drumSource.spatialBlend = 0f;
        }

        public void Play()
        {
            if (_playing) return;

            _order.Clear();
            foreach (var p in _board.Placed) _order.Add(p);
            _order.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));

            if (_order.Count == 0)
            {
                if (OnFinished != null) OnFinished("先把角色拖到鼓轨上，再演出");
                return;
            }

            _startWall = Time.time;
            _nextPlayIdx = 0;
            _trembleIdx = 0;

            _bgm.Play();
            _playing = true;
        }

        private void Update()
        {
            if (!_playing) return;
            float beat = (Time.time - _startWall) / BeatSeconds;

            // 越过起始拍 → 播放鼓音效
            while (_nextPlayIdx < _order.Count)
            {
                if (beat < _order[_nextPlayIdx].StartBeat) break;
                var clip = _order[_nextPlayIdx].Character.Sample.Clip;
                if (clip != null) _drumSource.PlayOneShot(clip);
                _nextPlayIdx++;
            }

            // 越过相邻边界 → 按契合度颤动（接上=大幅，没接上=小幅）
            while (_trembleIdx < _order.Count - 1)
            {
                if (beat < _order[_trembleIdx + 1].StartBeat) break;
                var prev = _order[_trembleIdx];
                var next = _order[_trembleIdx + 1];
                float intensity = EndpointFit.Fits(prev.Character.Waveform, next.Character.Waveform) ? 1f : 0.15f;
                prev.Waveform.Tremble(intensity);
                next.Waveform.Tremble(intensity);
                _trembleIdx++;
            }

            // 结束：最后一个鼓播完 + 1 拍余量
            var last = _order[_order.Count - 1];
            if (beat >= last.StartBeat + last.Character.Waveform.Duration + 1f)
            {
                _playing = false;
                if (OnFinished != null) OnFinished(BuildFitText());
            }
        }

        private float BeatSeconds { get { return 60f / BgmPlayer.Bpm; } }

        private string BuildFitText()
        {
            var waves = new List<WaveformDefinition>();
            foreach (var p in _order) waves.Add(p.Character.Waveform);
            var r = EndpointFit.Evaluate(waves);
            if (r.Pairs == 0) return "契合 · 只有 " + _order.Count + " 个鼓，还接不上";
            return "契合 " + r.Matches + "/" + r.Pairs + " · " + Mathf.RoundToInt(r.Score * 100f) + "%";
        }
    }
}
