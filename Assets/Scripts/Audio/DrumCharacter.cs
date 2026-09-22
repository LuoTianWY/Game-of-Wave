using System.Collections.Generic;
using WaveTeam.Core;

namespace WaveTeam.Audio
{
    /// <summary>
    /// 鼓原型里的一名角色：一个名字 + 随机绑定的一种鼓音色（波形 + AudioClip）。
    /// 波形即人格：性格名词跟随当前波形。
    /// </summary>
    public sealed class DrumCharacter
    {
        public readonly string Name;
        public readonly DrumSample Sample; // 音色源：基准波形定义 + AudioClip + 文件名
        public readonly RhythmPattern Pattern; // 完整节奏型骨架（含全部节奏点）

        private readonly List<HexCell> _path; // 展开后的完整波形路径（格序固定，解锁按「路径前缀」计）
        private int _unlockedCells;           // 已解锁的路径格数（前缀长度：path[0.._unlockedCells-1] 亮，其余暗）

        /// <summary>加点/洗点后触发（各预览渲染器据此刷新）。</summary>
        public event System.Action Changed;

        /// <summary>当前波形（原型期 = 基准定义；练习/磨合改变波形时再替换）。</summary>
        public WaveformDefinition Waveform { get { return Sample.Definition; } }

        /// <summary>性格名词（跟随波形）。</summary>
        public string Noun { get { return Sample.Definition.Noun; } }

        /// <summary>已解锁的路径格数（前缀长度）。</summary>
        public int UnlockedCells { get { return _unlockedCells; } }

        /// <summary>该角色的纵向抬升档位（-2..+2，由名字 seed 决定）；用于判断前后端点能否接上。</summary>
        public int Level { get { return RhythmPattern.LevelForSeed(RhythmPattern.StableHash(Name)); } }

        /// <summary>已解锁的节奏点数量（前缀覆盖到的节点数，≥1）。</summary>
        public int UnlockedPoints { get { int c = 0; for (int i = 0; i < _path.Count && i < _unlockedCells; i++) if (_path[i].IsRhythm) c++; return c; } }

        /// <summary>生效节奏型 = 前缀覆盖到的节奏点组成的子型（未解锁的不显示/不发声），时值不变。</summary>
        public RhythmPattern EffectivePattern
        {
            get
            {
                var list = new List<RhythmPoint>();
                for (int i = 0; i < _path.Count && i < _unlockedCells; i++)
                    if (_path[i].IsRhythm) list.Add(Pattern.Points[_path[i].PointIndex]);
                return new RhythmPattern(list.ToArray(), Pattern.TotalBeats);
            }
        }

        public DrumCharacter(string name, DrumSample sample, RhythmPattern pattern)
        {
            Name = name;
            Sample = sample;
            Pattern = pattern;
            _path = pattern.BuildPath(RhythmPattern.StableHash(name));
            // 默认解锁到第 2 个节奏点（下标 1）的节点格；不足 2 点或仅 2 点（末点含尾巴）则全解锁
            int secondNode = -1;
            for (int i = 0; i < _path.Count; i++)
                if (_path[i].IsRhythm && _path[i].PointIndex == 1) { secondNode = i; break; }
            _unlockedCells = (secondNode < 0 || Pattern.Points.Length <= 2) ? _path.Count : secondNode + 1;
        }

        /// <summary>加点：解锁到下一个节奏点（节点）为止。</summary>
        public void AddPoint()
        {
            for (int i = _unlockedCells; i < _path.Count; i++)
                if (_path[i].IsRhythm) { _unlockedCells = i + 1; if (Changed != null) Changed(); return; }
        }

        /// <summary>点到哪加到哪：把前缀解锁到指定格（含该格）；已越界则无操作。</summary>
        public void UnlockToCell(int cellIndex)
        {
            int target = System.Math.Min(cellIndex + 1, _path.Count);
            if (target <= _unlockedCells) return;
            _unlockedCells = target;
            if (Changed != null) Changed();
        }

        /// <summary>右键到哪取消到哪：把前缀收缩到指定格之前（该格起锁定）；至少保留首格（首节点）。</summary>
        public void LockToCell(int cellIndex)
        {
            int target = System.Math.Max(cellIndex, 1);
            if (target >= _unlockedCells) return;
            _unlockedCells = target;
            if (Changed != null) Changed();
        }

        /// <summary>洗点：回到只保留首个节奏点（首格）。</summary>
        public void ResetPoints()
        {
            if (_unlockedCells == 1) return;
            _unlockedCells = 1;
            if (Changed != null) Changed();
        }
    }

    /// <summary>
    /// 角色分队的随机分配器：每次启动把鼓样本洗牌分配给角色（C4）。
    /// </summary>
    public static class Roster
    {
        /// <summary>默认角色名（沿用现有 6 名角色）。</summary>
        public static readonly IReadOnlyList<string> DefaultNames = new List<string>
        {
            "阿宁", "小川", "老周", "石头", "小雨", "神秘嘉宾"
        };

        /// <summary>
        /// 给每个名字随机分配一个鼓样本；样本足够时「不重复」（每个鼓音色至多一名角色），
        /// 名字多于样本时才允许重复。每次调用结果不同。
        /// </summary>
        public static List<DrumCharacter> Build(
            IReadOnlyList<string> names, IReadOnlyList<DrumSample> samples, System.Random rng = null)
        {
            rng = rng ?? new System.Random();
            var pool = new List<DrumSample>(samples);
            var result = new List<DrumCharacter>();

            foreach (var name in names)
            {
                DrumSample sample;
                if (pool.Count > 0)
                {
                    int i = rng.Next(pool.Count);
                    sample = pool[i];
                    pool.RemoveAt(i);
                }
                else
                {
                    sample = samples[rng.Next(samples.Count)];
                }
                result.Add(new DrumCharacter(name, sample, RhythmPattern.ForSeed(RhythmPattern.StableHash(name))));
            }
            return result;
        }
    }
}
