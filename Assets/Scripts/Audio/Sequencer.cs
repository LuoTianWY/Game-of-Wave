using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.Audio
{
    /// <summary>
    /// 步进音序器：播放头按 BPM 从左到右扫过各轨，触发每轨链条的音符，
    /// 播完后发出结束事件。纯定时，不做实时音频分析。
    /// </summary>
    public sealed class Sequencer : MonoBehaviour
    {
        public float Bpm = 120f;
        public ChiptuneSynth Synth;

        public event System.Action<int> OnBeat;  // 参数：当前拍（从 0 起）
        public event System.Action OnFinished;

        public bool IsPlaying { get; private set; }
        public float BeatSeconds { get { return 60f / Bpm; } }

        private Dictionary<TrackRole, List<PlayedNote>> _notes;
        private int _totalBeats;

        public void Play(TaskDef task)
        {
            if (IsPlaying) return;

            _notes = new Dictionary<TrackRole, List<PlayedNote>>();
            _totalBeats = 0;
            foreach (var track in task.Tracks)
            {
                _notes[track.Role] = ChainTimeline.Flatten(track.Chain);
                _totalBeats = Mathf.Max(_totalBeats, TargetLength(track));
            }

            StartCoroutine(Run());
        }

        private static int TargetLength(Track track)
        {
            int len = 0;
            foreach (var t in track.TargetScore) len += t.Duration;
            return len;
        }

        private IEnumerator Run()
        {
            IsPlaying = true;
            for (int beat = 0; beat < _totalBeats; beat++)
            {
                if (OnBeat != null) OnBeat(beat);
                FireNotes(beat);
                yield return new WaitForSeconds(BeatSeconds);
            }
            IsPlaying = false;
            if (OnFinished != null) OnFinished();
        }

        private void FireNotes(int beat)
        {
            foreach (var pair in _notes)
            {
                foreach (var note in pair.Value)
                {
                    if (note.Onset == beat)
                    {
                        float seconds = note.Tile.Duration * BeatSeconds;
                        Synth.PlayNote(note.Tile.Pitch, note.Tile.Trait, seconds);
                    }
                }
            }
        }
    }
}
