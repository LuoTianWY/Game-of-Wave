using UnityEngine;

namespace WaveTeam.Core
{
    /// <summary>
    /// 音色大类：决定六边形节点的贴图与基色（对应策划表 9 类）。
    /// 顺序必须与 HexCategoryInfo 里的贴图名 / 颜色 / UV 表一一对应。
    /// </summary>
    public enum HexCategory
    {
        Percussion, // 打击乐   灰 #9E9E9E
        Strings,    // 弦乐     棕 #8D6E63
        Brass,      // 铜管     金 #D4A017
        Woodwind,   // 木管     紫 #8E24AA
        Vocal,      // 人声     绿 #43A047
        Synth,      // 电子合成器 蓝 #1E88E5
        Guitar,     // 吉他     橙 #FF8A00
        Piano,      // 钢琴     青 #26C6DA
        Ethnic      // 民族乐器  红 #E53935（占位）
    }

    /// <summary>
    /// 音色大类 → 贴图路径 / 基色 / UV 包围盒 的映射。
    ///
    /// 贴图放在 Resources/HexNodes/，每张 2048×2048，但六边形在画布里的位置和大小各不相同
    /// （宽度占比 0.77~0.88、高度 0.81~0.95，中心也不在画布正中），
    /// 所以用 UV 表把每张图里六边形的实际包围盒对齐到程序绘制的正六边形上，
    /// 否则 9 种六边形在游戏里显示出来会大小不一。
    ///
    /// UV 表由离线脚本按「非白像素包围盒」算出，V 已按 Unity 左下原点翻转。
    /// 素材更新后需重算（方法见 Assets/Resources/HexNodes/README.md）。
    /// </summary>
    public static class HexCategoryInfo
    {
        public const string ResourceFolder = "HexNodes/";
        public const int Count = 9;

        /// <summary>贴图文件名（不含扩展名），顺序 = HexCategory 枚举顺序。</summary>
        private static readonly string[] FileNames =
        {
            "percussion", "strings", "brass", "woodwind", "vocal",
            "synth", "guitar", "piano", "ethnic"
        };

        /// <summary>策划表里的基色，与 trait_music.json 的 color 字段一致。</summary>
        private static readonly string[] ColorHexes =
        {
            "#9E9E9E", "#8D6E63", "#D4A017", "#8E24AA", "#43A047",
            "#1E88E5", "#FF8A00", "#26C6DA", "#E53935"
        };

        /// <summary>Resources 下的加载路径（不含扩展名）。</summary>
        public static string ResourcePath(HexCategory category)
        {
            return ResourceFolder + FileNames[(int)category];
        }

        /// <summary>贴图文件名（不含扩展名）。</summary>
        public static string FileName(HexCategory category)
        {
            return FileNames[(int)category];
        }

        /// <summary>
        /// 该大类的 UV 包围盒。素材已在离屏阶段规范化：裁到六边形本体、统一缩放到
        /// 443×512（正六边形比例 宽:高 = √3:2），六边形精确占满整图，所以直接用全图 UV。
        /// 重新生成素材后需要重新规范化（见 Assets/Resources/HexNodes/README.md）。
        /// </summary>
        public static Vector4 UvRect(HexCategory category)
        {
            return new Vector4(0f, 0f, 1f, 1f);
        }

        /// <summary>该大类的基色（与策划表一致），解析失败回退灰色。</summary>
        public static Color BaseColor(HexCategory category)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(ColorHexes[(int)category], out color) ? color : Color.gray;
        }

        /// <summary>把任意整数安全映射回枚举（随机分配用）。</summary>
        public static HexCategory FromIndex(int index)
        {
            int n = ((index % Count) + Count) % Count;
            return (HexCategory)n;
        }
    }
}
