using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WaveTeam.Core
{
    /// <summary>
    /// 排练玩法使用的三个属性。
    /// 1 = 低，2 = 中，3 = 高
    /// </summary>
    public struct RehearsalStats
    {
        public int Stability;
        public int Layering;
        public int Density;

        public RehearsalStats(int stability, int layering, int density)
        {
            Stability = stability;
            Layering = layering;
            Density = density;
        }
    }

    /// <summary>
    /// 把角色性格转换成排练玩法属性。
    /// UI 和评分系统都从这里读取，避免两套规则不一致。
    /// </summary>
    public static class RehearsalTraits
    {
        public static RehearsalStats GetStats(string noun)
        {
            switch (noun)
            {
                case "沉稳":
                    return new RehearsalStats(
                        3,  // 稳定
                        1,  // 层次
                        1   // 密度
                    );

                case "跳脱":
                    return new RehearsalStats(
                        1,
                        3,
                        2
                    );

                case "张扬":
                    return new RehearsalStats(
                        1,
                        2,
                        3
                    );

                case "干脆":
                    return new RehearsalStats(
                        2,
                        2,
                        2
                    );

                default:
                    return new RehearsalStats(
                        2,
                        2,
                        2
                    );
            }
        }
    }
}