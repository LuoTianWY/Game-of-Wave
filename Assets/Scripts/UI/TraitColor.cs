using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>特质 → 颜色的展示映射（尖峰=红、平缓=蓝、波浪=绿、方波=紫灰）。</summary>
    public static class TraitColor
    {
        public static Color Of(Trait trait)
        {
            switch (trait)
            {
                case Trait.Impatient: return new Color(0.95f, 0.40f, 0.35f);
                case Trait.Calm: return new Color(0.40f, 0.60f, 0.95f);
                case Trait.Flexible: return new Color(0.40f, 0.85f, 0.55f);
                case Trait.Stubborn: return new Color(0.62f, 0.55f, 0.78f);
                default: return Color.gray;
            }
        }
    }
}
