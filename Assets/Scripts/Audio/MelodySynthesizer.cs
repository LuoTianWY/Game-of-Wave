using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaveTeam.Audio
{
    /// <summary>Finite, enveloped pitched tones. Clips belong to one player and are released with it.</summary>
    internal sealed class MelodySynthesizer : IDisposable
    {
        public const string Keys = "synth.soft_keys";
        public const string Bass = "synth.round_bass";
        private readonly Dictionary<(string Instrument, int Midi, int Samples, int Rate), AudioClip> _clips =
            new Dictionary<(string, int, int, int), AudioClip>();

        public static bool Supports(string instrument) { return instrument == Keys || instrument == Bass; }
        public static double Frequency(int midi) { return 440.0 * Math.Pow(2.0, (midi - 69) / 12.0); }

        public AudioClip GetClip(string instrument, int midi, double durationSeconds)
        {
            if (!Supports(instrument) || midi < 0 || midi > 127 || !(durationSeconds > 0 && durationSeconds <= 48))
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Invalid synthesizer note.");
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            int samples = Math.Max(2, (int)Math.Round(durationSeconds * rate));
            var key = (instrument, midi, samples, rate);
            if (_clips.TryGetValue(key, out var cached)) return cached;

            var pcm = new float[samples];
            double frequency = Frequency(midi);
            double attack = Math.Min(0.008, durationSeconds * 0.15);
            double release = Math.Min(0.035, durationSeconds * 0.25);
            bool bass = instrument == Bass;
            for (int i = 0; i < samples; i++)
            {
                double time = i / (double)rate;
                double phase = 2 * Math.PI * frequency * time;
                double tone = Math.Sin(phase);
                if (frequency * 2 < rate * 0.5) tone += (bass ? 0.12 : 0.25) * Math.Sin(phase * 2);
                if (!bass && frequency * 3 < rate * 0.5) tone += 0.08 * Math.Sin(phase * 3);
                double envelope = Math.Min(1, time / attack) * Math.Min(1, (samples - 1 - i) / (release * rate));
                double decay = bass ? 0.85 : 0.35 + 0.65 * Math.Exp(-time * 3);
                pcm[i] = (float)(tone * envelope * decay * (bass ? 0.24 : 0.18));
            }

            var clip = AudioClip.Create(instrument + "." + midi + "." + samples, samples, 1, rate, false);
            clip.SetData(pcm, 0);
            _clips.Add(key, clip);
            return clip;
        }

        public void Dispose()
        {
            foreach (var clip in _clips.Values)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(clip);
                else UnityEngine.Object.DestroyImmediate(clip);
            }
            _clips.Clear();
        }
    }
}
