using System;
using System.Collections.Generic;
using UnityEngine;
using WaveTeam.Audio;

namespace WaveTeam.UI
{
    /// <summary>用同一个 DSP 起点调度 BGM 和所有轨道的节奏点。</summary>
    public sealed class PerformancePlayer : MonoBehaviour
    {
        private const double ScheduleAhead = 0.2;

        private DrumTrackBoard _board;
        private BgmPlayer _bgm;
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly List<Hit> _hits = new List<Hit>();
        private readonly List<Transition> _transitions = new List<Transition>();

        private bool _playing;
        private double _startDsp;
        private double _beatSeconds;
        private float _endBeat;
        private int _nextHit;
        private int _nextTransition;
        private string _fitText;

        private struct Hit
        {
            public float Beat;
            public AudioClip Clip;
        }

        private struct Transition
        {
            public float Beat;
            public HexWaveformRenderer Previous;
            public HexWaveformRenderer Next;
            public float Intensity;
        }

        private sealed class Voice
        {
            public AudioSource Source;
            public double BusyUntil;
        }

        public bool IsPlaying { get { return _playing; } }

        public Action<string> OnFinished;

        public void Setup(DrumTrackBoard board, BgmPlayer bgm)
        {
            _board = board;
            _bgm = bgm;
        }

        public void Play()
        {
            if (_playing) return;
            if (_board.Placed.Count == 0)
            {
                OnFinished?.Invoke("先把角色拖到鼓轨上，再演出");
                return;
            }

            Stop();
            BuildTimeline();
            _beatSeconds = _bgm.BeatSeconds;
            double now = AudioSettings.dspTime;
            _startDsp = now + ScheduleAhead;
            _bgm.PlayScheduled(_startDsp);
            _playing = true;
            ScheduleHits(now);
        }

        /// <summary>停止已发声和仍在排队的鼓点。</summary>
        public void Stop()
        {
            _playing = false;
            foreach (var voice in _voices)
            {
                voice.Source.Stop();
                voice.BusyUntil = 0;
            }
        }

        private void BuildTimeline()
        {
            _hits.Clear();
            _transitions.Clear();
            _endBeat = 0f;
            _nextHit = 0;
            _nextTransition = 0;

            var ordered = new List<PlacedDrum>(_board.Placed);
            ordered.Sort((a, b) => {
                int laneOrder = a.LaneIndex.CompareTo(b.LaneIndex);
                return laneOrder != 0 ? laneOrder : a.StartBeat.CompareTo(b.StartBeat);
            });

            int connectable = 0, connected = 0;
            PlacedDrum previous = null;
            foreach (var drum in ordered)
            {
                _endBeat = Mathf.Max(_endBeat, drum.StartBeat + drum.DurationBeats);
                var clip = drum.Character.Sample != null ? drum.Character.Sample.Clip : null;
                if (clip != null)
                {
                    foreach (var point in drum.Character.EffectivePattern.Points)
                        _hits.Add(new Hit { Beat = drum.StartBeat + point.Beat, Clip = clip });
                }

                if (previous != null && previous.LaneIndex == drum.LaneIndex)
                {
                    bool linked = previous.ConnectedNeighbor == drum;
                    if (DrumTrackBoard.CanConnect(previous, drum))
                    {
                        connectable++;
                        if (linked) connected++;
                    }
                    _transitions.Add(new Transition
                    {
                        Beat = drum.StartBeat,
                        Previous = previous.Waveform,
                        Next = drum.Waveform,
                        Intensity = linked ? 1f : 0.15f
                    });
                }
                previous = drum;
            }

            _hits.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            _transitions.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            _fitText = connectable == 0
                ? "契合 · 只有 " + ordered.Count + " 个鼓，还接不上"
                : "契合 " + connected + "/" + connectable + " · "
                    + Mathf.RoundToInt(connected / (float)connectable * 100f) + "%";
        }

        private void ScheduleHits(double now)
        {
            while (_nextHit < _hits.Count)
            {
                var hit = _hits[_nextHit];
                double time = _startDsp + hit.Beat * _beatSeconds;
                if (time > now + ScheduleAhead) break;

                // 主线程长帧错过计划时间时，给该批鼓点同一个补发时间。
                time = Math.Max(time, now + 0.02);
                var voice = GetVoice(now);
                voice.Source.clip = hit.Clip;
                voice.Source.PlayScheduled(time);
                voice.BusyUntil = time + hit.Clip.samples / (double)hit.Clip.frequency;
                _nextHit++;
            }
        }

        private Voice GetVoice(double now)
        {
            foreach (var voice in _voices)
                if (voice.BusyUntil <= now && !voice.Source.isPlaying) return voice;

            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var newVoice = new Voice { Source = source };
            _voices.Add(newVoice);
            return newVoice;
        }

        private void Update()
        {
            if (!_playing) return;
            double now = AudioSettings.dspTime;
            ScheduleHits(now);
            double beat = (now - _startDsp) / _beatSeconds;
            while (_nextTransition < _transitions.Count && _transitions[_nextTransition].Beat <= beat)
            {
                var transition = _transitions[_nextTransition++];
                if (transition.Previous != null && transition.Previous.isActiveAndEnabled)
                    transition.Previous.Tremble(transition.Intensity);
                if (transition.Next != null && transition.Next.isActiveAndEnabled)
                    transition.Next.Tremble(transition.Intensity);
            }

            if (beat >= _endBeat + 1f)
            {
                _playing = false;
                OnFinished?.Invoke(_fitText);
            }
        }
    }
}
