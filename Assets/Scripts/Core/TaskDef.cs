using System.Collections.Generic;

namespace WaveTeam.Core
{
    /// <summary>
    /// 任务：一段多声部音乐，由多条轨道组成；每轨有一段待复现的目标谱。
    /// （命名 TaskDef 以避免与 System.Threading.Tasks.Task 冲突。）
    /// </summary>
    public sealed class TaskDef
    {
        public readonly string Name;
        public readonly int BeatsPerBar;                 // 节奏骨架：每小节拍数
        public readonly IReadOnlyList<Track> Tracks;

        public TaskDef(string name, int beatsPerBar, params Track[] tracks)
        {
            Name = name;
            BeatsPerBar = beatsPerBar;
            Tracks = tracks;
        }
    }
}
