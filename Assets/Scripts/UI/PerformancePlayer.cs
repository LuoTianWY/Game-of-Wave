using System;
using System.Collections.Generic;
using UnityEngine;
using WaveTeam.Audio;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>用同一个 DSP 起点调度 BGM、鼓点、低音和旋律音符。</summary>
    public sealed class PerformancePlayer : MonoBehaviour
    {
        private const double ScheduleAhead = 0.2;

        private DrumTrackBoard _board;
        private BgmPlayer _bgm;
        private bool _useBackgroundMusic;
        private float _playbackBpm;
        public float CurrentBpm => _playbackBpm > 0f ? _playbackBpm : BgmPlayer.Bpm;
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly List<Hit> _hits = new List<Hit>();
        private readonly List<Transition> _transitions = new List<Transition>();
        private readonly MelodySynthesizer _synth = new MelodySynthesizer();

        private bool _playing;
        private double _startDsp;
        private double _beatSeconds;
        private float _endBeat;
        private int _nextHit;
        private int _nextTransition;
        private string _fitText;
        private Action<string> _onPlaybackEnded;
        private bool _preview;
        private bool _showBoardProgress;
        private bool _presetsOnly;

        private struct Hit
        {
            public float Beat;
            public AudioClip Clip;
            public WaveKind Kind;
            public string Instrument;
            public int MidiNote;
            public float DurationBeats;
            public float Velocity;
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

        public void Setup(DrumTrackBoard board, BgmPlayer bgm, bool useBackgroundMusic = true, float playbackBpm = 0f)
        {
            Stop();
            foreach (var voice in _voices) voice.Source.clip = null;
            _synth.Dispose();
            _board = board;
            _bgm = bgm;
            _useBackgroundMusic = useBackgroundMusic;
            _playbackBpm = playbackBpm > 0f && playbackBpm < 400f ? playbackBpm : 0f;
        }

        public void Play()
        {
            PlayArrangement(OnFinished, preview: false, presetsOnly: false);
        }

        public void PreviewArrangement(Action<string> onFinished, bool presetsOnly = false)
        {
            PlayArrangement(onFinished, preview: true, presetsOnly);
        }

        private void PlayArrangement(Action<string> onFinished, bool preview, bool presetsOnly)
        {
            if (_playing) return;
            if (_board.Placed.Count == 0)
            {
                onFinished?.Invoke("先把角色拖到鼓轨上，再演出");
                return;
            }

            Stop();
            BuildTimeline(presetsOnly);
            _showBoardProgress = true;
            _presetsOnly = presetsOnly;
            StartPlayback(_useBackgroundMusic, preview, onFinished);
        }

        public void PreviewCharacter(DrumCharacter character, Action<string> onFinished)
        {
            Stop();
            ResetTimeline();
            AddHits(character, 0f);
            _endBeat = character.Pattern.TotalBeats;
            _showBoardProgress = false;
            StartPlayback(false, true, onFinished);
        }

        private void StartPlayback(bool withBgm, bool preview, Action<string> onFinished)
        {
            _preview = preview;
            _onPlaybackEnded = onFinished;
            _beatSeconds = 60.0 / CurrentBpm;
            for (int i = 0; i < _hits.Count; i++)
            {
                var hit = _hits[i];
                if (hit.Kind == WaveKind.Rhythm) continue;
                hit.Clip = _synth.GetClip(hit.Instrument, hit.MidiNote, hit.DurationBeats * _beatSeconds);
                _hits[i] = hit;
            }
            double now = AudioSettings.dspTime;
            _startDsp = now + ScheduleAhead;
            if (withBgm) _bgm.PlayScheduled(_startDsp);
            _playing = true;
            ScheduleHits(now);
        }

        /// <summary>停止已发声和仍在排队的鼓点与音符。</summary>
        public void Stop()
        {
            _playing = false;
            if (_board != null) _board.SetPlaybackBeat(-1f, false);
            _onPlaybackEnded = null;
            if (_bgm != null) _bgm.Stop();
            foreach (var voice in _voices)
            {
                if (voice.Source != null) voice.Source.Stop();
                voice.BusyUntil = 0;
            }
        }

        private void OnDestroy()
        {
            Stop();
            _synth.Dispose();
        }

        private void ResetTimeline()
        {
            _hits.Clear();
            _transitions.Clear();
            _endBeat = 0f;
            _nextHit = 0;
            _nextTransition = 0;
            _fitText = null;
        }

        private void BuildTimeline(bool presetsOnly)
        {
            ResetTimeline();
            var ordered = new List<PlacedDrum>();
            foreach (var drum in _board.Placed)
                if (!presetsOnly || drum.Locked) ordered.Add(drum);
            ordered.Sort((a, b) => {
                int laneOrder = a.LaneIndex.CompareTo(b.LaneIndex);
                return laneOrder != 0 ? laneOrder : a.StartBeat.CompareTo(b.StartBeat);
            });

            int connectable = 0, connected = 0;
            PlacedDrum previous = null;
            foreach (var drum in ordered)
            {
                _endBeat = Mathf.Max(_endBeat, drum.StartBeat + drum.DurationBeats);
                AddHits(drum.Character, drum.StartBeat);

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

        private void AddHits(DrumCharacter character, float startBeat)
        {
            if (character.Kind != WaveKind.Rhythm)
            {
                foreach (var note in character.MelodyNotes)
                {
                    if (note.Velocity <= 0f) continue;
                    _hits.Add(new Hit {
                        Beat = startBeat + note.StartBeat, Kind = character.Kind, Instrument = character.InstrumentId,
                        MidiNote = note.MidiNote, DurationBeats = note.DurationBeats, Velocity = note.Velocity
                    });
                }
                return;
            }
            foreach (var point in character.PlaybackPoints)
                _hits.Add(new Hit { Beat = startBeat + point.Beat, Clip = character.Sample.Clip, Velocity = 1f });
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
                voice.Source.pitch = 1f;
                voice.Source.volume = hit.Velocity;
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
            if (_showBoardProgress && _board != null)
                _board.SetPlaybackBeat((float)beat, _presetsOnly);
            while (_nextTransition < _transitions.Count && _transitions[_nextTransition].Beat <= beat)
            {
                var transition = _transitions[_nextTransition++];
                if (transition.Previous != null && transition.Previous.isActiveAndEnabled)
                    transition.Previous.Tremble(transition.Intensity);
                if (transition.Next != null && transition.Next.isActiveAndEnabled)
                    transition.Next.Tremble(transition.Intensity);
            }

            if (beat >= _endBeat + (_preview ? 0f : 1f))
            {
                _playing = false;
                if (_board != null) _board.SetPlaybackBeat(-1f, false);
                if (_preview) _bgm.Stop();
                var finished = _onPlaybackEnded;
                _onPlaybackEnded = null;
                finished?.Invoke(_fitText);
            }
        }
    }
}
