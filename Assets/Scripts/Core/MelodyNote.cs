using System;

namespace WaveTeam.Core
{
    // Rhythm must remain zero so older JSON entries retain their original meaning.
    public enum WaveKind { Rhythm, Bass, Melody }

    [Serializable]
    public struct MelodyNote
    {
        public float StartBeat;
        public float DurationBeats;
        public int MidiNote;
        public float Velocity;

        public MelodyNote(float startBeat, float durationBeats, int midiNote, float velocity)
        {
            StartBeat = startBeat;
            DurationBeats = durationBeats;
            MidiNote = midiNote;
            Velocity = velocity;
        }

        public bool IsValid(float phraseBeats)
        {
            return StartBeat >= 0f && DurationBeats > 0f && StartBeat + DurationBeats <= phraseBeats
                && MidiNote >= 0 && MidiNote <= 127 && Velocity >= 0f && Velocity <= 1f;
        }

        public static string Name(int midiNote)
        {
            string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            return names[midiNote % 12] + (midiNote / 12 - 1);
        }
    }
}
