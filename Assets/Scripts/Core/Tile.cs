namespace WaveTeam.Core
{
    /// <summary>
    /// 波形的一段（一个 step）。三个属性：音高(高度)、时值(拍数)、特质(形状/音色)。
    /// </summary>
    public sealed class Tile
    {
        public readonly int Pitch;    // 音高，越高 tile 越高
        public readonly int Duration; // 时值（拍数），1 = 一拍，2 = 两拍
        public readonly Trait Trait;  // 特质，决定形状与音色

        public Tile(int pitch, int duration, Trait trait)
        {
            Pitch = pitch;
            Duration = duration;
            Trait = trait;
        }

        public override string ToString()
        {
            return string.Format("{0}(音高{1}, {2}拍)", Trait.DisplayName(), Pitch, Duration);
        }
    }
}
