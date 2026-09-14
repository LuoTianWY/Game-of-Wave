namespace WaveTeam.Core
{
    /// <summary>
    /// 性格特质 = 波形形状 = 音色（形状即特质，不设独立外壳）。
    /// 尖峰=急躁，平缓=冷静，波浪=灵活，方波=固执。
    /// </summary>
    public enum Trait
    {
        Impatient, // 急躁 ╱╲
        Calm,      // 冷静 ──
        Flexible,  // 灵活 ≈≈
        Stubborn   // 固执 ▇▇
    }

    public static class TraitExtensions
    {
        public static string DisplayName(this Trait t)
        {
            switch (t)
            {
                case Trait.Impatient: return "急躁";
                case Trait.Calm: return "冷静";
                case Trait.Flexible: return "灵活";
                case Trait.Stubborn: return "固执";
                default: return "?";
            }
        }
    }
}
