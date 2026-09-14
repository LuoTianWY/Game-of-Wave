using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>占位数据：4 个可解锁角色 + 1 个简单任务（每个轨道都有完美解）。</summary>
    public static class SampleData
    {
        public static readonly Character Aning = new Character(
            "阿宁",
            new Tile(3, 1, Trait.Calm),
            new Tile(3, 1, Trait.Calm));

        public static readonly Character Xiaochuan = new Character(
            "小川",
            new Tile(5, 1, Trait.Flexible),
            new Tile(5, 1, Trait.Flexible));

        public static readonly Character Laozhou = new Character(
            "老周",
            new Tile(4, 1, Trait.Impatient),
            new Tile(4, 1, Trait.Impatient));

        public static readonly Character Shitou = new Character(
            "石头",
            new Tile(2, 1, Trait.Stubborn),
            new Tile(2, 1, Trait.Stubborn));

        /// <summary>一个需要花资源解锁的隐藏角色。</summary>
        public static readonly Character Mysterious = new Character(
            "神秘嘉宾",
            new Tile(6, 1, Trait.Impatient),
            new Tile(6, 1, Trait.Impatient),
            new Tile(6, 1, Trait.Flexible));

        /// <summary>初始可用角色。</summary>
        public static readonly IReadOnlyList<Character> Roster = new List<Character>
        {
            Aning, Xiaochuan, Laozhou, Shitou
        };

        /// <summary>解锁隐藏角色需要的资源。</summary>
        public const int UnlockCost = 50;

        /// <summary>
        /// 任务《晨间速递》：4/4 拍，三轨。
        /// 完美解：主旋律 [阿宁,小川,老周,石头]；和声 [阿宁,小川]；低音 [石头,石头]。
        /// </summary>
        public static TaskDef MorningDelivery()
        {
            var melody = new Track(TrackRole.Melody,
                new Tile(3, 1, Trait.Calm), new Tile(3, 1, Trait.Calm),
                new Tile(5, 1, Trait.Flexible), new Tile(5, 1, Trait.Flexible),
                new Tile(4, 1, Trait.Impatient), new Tile(4, 1, Trait.Impatient),
                new Tile(2, 1, Trait.Stubborn), new Tile(2, 1, Trait.Stubborn));

            var harmony = new Track(TrackRole.Harmony,
                new Tile(3, 1, Trait.Calm), new Tile(3, 1, Trait.Calm),
                new Tile(5, 1, Trait.Flexible), new Tile(5, 1, Trait.Flexible));

            var bass = new Track(TrackRole.Bass,
                new Tile(2, 1, Trait.Stubborn), new Tile(2, 1, Trait.Stubborn),
                new Tile(2, 1, Trait.Stubborn), new Tile(2, 1, Trait.Stubborn));

            return new TaskDef("晨间速递", 4, melody, harmony, bass);
        }
    }
}
