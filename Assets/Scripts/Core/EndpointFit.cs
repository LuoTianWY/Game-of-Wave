using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 端点契合度（C10）：按时间顺序排列的波形链，相邻两者
    /// 「前鼓尾端点段 == 后鼓头端点段」即算「接上」。
    /// 契合度 = 接上的相邻对数 / 相邻总对数。纯逻辑，无 UnityEngine。
    /// </summary>
    public static class EndpointFit
    {
        /// <summary>单对是否接上。</summary>
        public static bool Fits(WaveformDefinition prev, WaveformDefinition next)
        {
            if (prev == null || next == null) return false;
            return prev.TailSegment == next.HeadSegment;
        }

        /// <summary>接上的相邻对数。</summary>
        public static int MatchCount(IReadOnlyList<WaveformDefinition> orderedChain)
        {
            if (orderedChain == null) return 0;
            int matches = 0;
            for (int i = 0; i < orderedChain.Count - 1; i++)
                if (Fits(orderedChain[i], orderedChain[i + 1])) matches++;
            return matches;
        }

        /// <summary>契合度 0..1（少于 2 个视为 0）。</summary>
        public static float Score(IReadOnlyList<WaveformDefinition> orderedChain)
        {
            if (orderedChain == null || orderedChain.Count < 2) return 0f;
            return MatchCount(orderedChain) / (float)(orderedChain.Count - 1);
        }

        /// <summary>一次返回「接上对数 / 总对数」。</summary>
        public static EndpointFitResult Evaluate(IReadOnlyList<WaveformDefinition> orderedChain)
        {
            int pairs = (orderedChain == null || orderedChain.Count < 2) ? 0 : orderedChain.Count - 1;
            return new EndpointFitResult(MatchCount(orderedChain), pairs);
        }
    }

    /// <summary>契合结果：接上对数、总相邻对数、以及 0..1 的契合度。</summary>
    public struct EndpointFitResult
    {
        public readonly int Matches;
        public readonly int Pairs;
        public float Score { get { return Pairs == 0 ? 0f : Matches / (float)Pairs; } }

        public EndpointFitResult(int matches, int pairs)
        {
            Matches = matches;
            Pairs = pairs;
        }
    }
}
