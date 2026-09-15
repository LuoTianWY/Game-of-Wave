using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>链条展开后的一个音符：tile + 它的起始拍。</summary>
    public sealed class PlayedNote
    {
        public readonly Tile Tile;
        public readonly float Onset; // 起始拍（从 0 起）

        public PlayedNote(Tile tile, float onset)
        {
            Tile = tile;
            Onset = onset;
        }
    }

    /// <summary>把一条链条（角色有序列表）展开成时间线上的音符。</summary>
    public static class ChainTimeline
    {
        public static List<PlayedNote> Flatten(IEnumerable<Character> chain)
        {
            var notes = new List<PlayedNote>();
            float beat = 0f;
            foreach (var character in chain)
            {
                foreach (var tile in character.Tiles)
                {
                    notes.Add(new PlayedNote(tile, beat));
                    beat += tile.Duration;
                }
            }
            return notes;
        }

        /// <summary>一名角色的总拍数（所有 tile 时值之和）。</summary>
        public static float TotalBeats(Character character)
        {
            float total = 0f;
            foreach (var tile in character.Tiles) total += tile.Duration;
            return total;
        }

        /// <summary>一条链条的总拍数。</summary>
        public static float TotalBeats(IEnumerable<Character> chain)
        {
            float total = 0f;
            foreach (var character in chain) total += TotalBeats(character);
            return total;
        }
    }
}
