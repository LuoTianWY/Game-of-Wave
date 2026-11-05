using System.Collections.Generic;
using WaveTeam.Core;

namespace WaveTeam.Audio
{
    public static class MelodyPhrases
    {
        public static List<DrumCharacter> CreateTestWaves()
        {
            return new List<DrumCharacter> {
                Phrase("Melody A", WaveKind.Melody, MelodySynthesizer.Keys, 60, 64, 67, 64),
                Phrase("Melody B", WaveKind.Melody, MelodySynthesizer.Keys, 64, 69, 60, 67),
                Phrase("Melody C", WaveKind.Melody, MelodySynthesizer.Keys, 67, 64, 62, 60),
                Phrase("Bass", WaveKind.Bass, MelodySynthesizer.Bass, 36, 43, 45, 43)
            };
        }

        private static DrumCharacter Phrase(string name, WaveKind kind, string instrument, params int[] midi)
        {
            var notes = new MelodyNote[midi.Length];
            for (int i = 0; i < midi.Length; i++)
                notes[i] = new MelodyNote(i, 1f, midi[i], kind == WaveKind.Bass ? 0.75f : 0.8f);
            return new DrumCharacter(name, kind, instrument, notes, 4f);
        }
    }
}
