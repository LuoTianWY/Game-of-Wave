using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 占位数据：6 名角色。每个角色是一段固定波形（tile 序列），
    /// 时值混搭（0.5/1/1.5/2 拍），让节奏契合度判定有意义。
    /// </summary>
    public static class SampleData
    {
        public static readonly Character Aning = new Character(
            "阿宁",
            new Tile(3, 1f, Trait.Calm),
            new Tile(3, 1f, Trait.Calm));

        public static readonly Character Xiaochuan = new Character(
            "小川",
            new Tile(5, 0.5f, Trait.Flexible),
            new Tile(5, 0.5f, Trait.Flexible));

        public static readonly Character Laozhou = new Character(
            "老周",
            new Tile(6, 1.5f, Trait.Impatient));

        public static readonly Character Shitou = new Character(
            "石头",
            new Tile(2, 2f, Trait.Stubborn));

        public static readonly Character Xiaoyu = new Character(
            "小雨",
            new Tile(4, 1f, Trait.Flexible),
            new Tile(4, 0.5f, Trait.Flexible));

        public static readonly Character Mysterious = new Character(
            "神秘嘉宾",
            new Tile(7, 0.5f, Trait.Impatient),
            new Tile(7, 1.5f, Trait.Impatient));

        /// <summary>全部 6 名角色，供待选区展示。</summary>
        public static readonly IReadOnlyList<Character> All = new List<Character>
        {
            Aning, Xiaochuan, Laozhou, Shitou, Xiaoyu, Mysterious
        };
    }
}
