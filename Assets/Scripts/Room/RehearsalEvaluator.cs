
using WaveTeam.Core;
using WaveTeam.UI;

namespace WaveTeam.Room
{
    public sealed class RehearsalScoreResult
    {
        public int Score;

        // 三个排练目标是否达成
        public bool OpeningStableGood;
        public bool DevelopmentLayeredGood;
        public bool DensityGood;
    }

    public static class RehearsalEvaluator
    {
        public static RehearsalScoreResult Evaluate(DrumTrackBoard board)
        {
            var result = new RehearsalScoreResult();

            if (board == null || board.Placed.Count == 0)
                return result;

            int highDensityCount = 0;

            foreach (var drum in board.Placed)
            {
                // 从 RehearsalTraits 获取角色属性
                var stats = RehearsalTraits.GetStats(
                    drum.Character.Noun
                );

                float startBeat = drum.StartBeat;

                // 目标1：开场（0-3拍）需要稳定
                if (startBeat >= 0f &&
                    startBeat < 4f &&
                    stats.Stability >= 3)
                {
                    result.OpeningStableGood = true;
                }

                // 目标2：发展（4-7拍）需要层次
                if (startBeat >= 4f &&
                    startBeat < 8f &&
                    stats.Layering >= 3)
                {
                    result.DevelopmentLayeredGood = true;
                }

                // 目标3：高密度波形不宜太多
                if (stats.Density >= 3)
                {
                    highDensityCount++;
                }
            }

            // 至少安排两段，高密度波形最多一段
            result.DensityGood =
                board.Placed.Count >= 2 &&
                highDensityCount <= 1;

            // 统一排练评分
            result.Score =
                (result.OpeningStableGood ? 33 : 0) +
                (result.DevelopmentLayeredGood ? 33 : 0) +
                (result.DensityGood ? 34 : 0);

            return result;
        }
    }
}
