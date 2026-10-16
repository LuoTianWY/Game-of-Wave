using WaveTeam.UI;

namespace WaveTeam.Room
{
    public sealed class RehearsalScoreResult
    {
        public int Score;

        public bool AningOpeningGood;
        public bool XiaochuanTimingGood;
        public bool ShitouBalanceGood;
    }

    public static class RehearsalEvaluator
    {
        public static RehearsalScoreResult Evaluate(DrumTrackBoard board)
        {
            var result = new RehearsalScoreResult();

            if (board == null)
                return result;

            bool hasAning = false;
            bool hasXiaochuan = false;

            float firstAningBeat = float.MaxValue;
            float firstXiaochuanBeat = float.MaxValue;

            int shitouCount = 0;

            foreach (var drum in board.Placed)
            {
                string name = drum.Character.Name;

                // 阿宁：负责开场
                if (name == "阿宁")
                {
                    hasAning = true;

                    if (drum.StartBeat < firstAningBeat)
                        firstAningBeat = drum.StartBeat;
                }

                // 小川：不要太早进入
                else if (name == "小川")
                {
                    hasXiaochuan = true;

                    if (drum.StartBeat < firstXiaochuanBeat)
                        firstXiaochuanBeat = drum.StartBeat;
                }

                // 石头：不要放太多
                else if (name == "石头")
                {
                    shitouCount++;
                }
            }

            // 阿宁必须在前 4 拍进入
            result.AningOpeningGood =
                hasAning && firstAningBeat < 4f;

            // 小川从第 4 拍或更晚进入
            result.XiaochuanTimingGood =
                hasXiaochuan && firstXiaochuanBeat >= 4f;

            // 石头至少出现一次，但最多两次
            result.ShitouBalanceGood =
                shitouCount >= 1 && shitouCount <= 2;

            int passed = 0;

            if (result.AningOpeningGood)
                passed++;

            if (result.XiaochuanTimingGood)
                passed++;

            if (result.ShitouBalanceGood)
                passed++;

            if (passed == 3)
                result.Score = 100;
            else if (passed == 2)
                result.Score = 67;
            else if (passed == 1)
                result.Score = 33;
            else
                result.Score = 0;

            return result;
        }
    }
}