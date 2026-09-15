namespace WaveTeam.Core
{
    /// <summary>
    /// 波形的一段（一个 step）。三个属性：音高(高度)、时值(拍数)、特质(形状/音色)。
    /// 时值为浮点拍数：0.5=八分，1=四分，1.5=附点四分，2=二分。
    /// </summary>
    public sealed class Tile
    {
        public readonly int Pitch;      // 音高，越高 tile 越高
        public readonly float Duration; // 时值（拍数）：0.5=八分, 1=四分, 1.5=附点四分, 2=二分
        public readonly Trait Trait;    // 特质，决定形状与音色

        public Tile(int pitch, float duration, Trait trait)
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
