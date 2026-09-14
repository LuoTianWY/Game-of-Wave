using System;

namespace WaveTeam.Core
{
    /// <summary>资源回环：准确度 → 资源 → 解锁。</summary>
    public sealed class Progression
    {
        public int Resources { get; private set; }

        /// <summary>结算：准确度(0..1) → 资源（成正比，取整）。返回本次获得量。</summary>
        public int Settle(float accuracy)
        {
            int gained = (int)Math.Round(accuracy * 100f);
            Resources += gained;
            return gained;
        }

        /// <summary>尝试花费资源。成功返回 true，否则返回 false。</summary>
        public bool TrySpend(int cost)
        {
            if (Resources < cost) return false;
            Resources -= cost;
            return true;
        }
    }
}
