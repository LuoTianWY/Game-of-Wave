using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 角色：一段固定的波形（tile 序列），在编排过程中保持不变。
    /// </summary>
    public sealed class Character
    {
        public readonly string Name;
        public readonly IReadOnlyList<Tile> Tiles; // 固定波形

        public Character(string name, params Tile[] tiles)
        {
            Name = name;
            Tiles = tiles;
        }
    }
}
