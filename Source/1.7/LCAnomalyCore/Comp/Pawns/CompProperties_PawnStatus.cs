using Verse;

namespace LCAnomalyCore.Comp.Pawns
{
    /// <summary>
    /// LC员工基本属性CompProperties
    /// </summary>
    public class CompProperties_PawnStatus : CompProperties
    {
        /// <summary>表示 <c>statusNumMax</c>。</summary>
        public int statusNumMax = 100;

        /// <summary>表示 <c>initialRange_Fortitude</c>。</summary>
        public IntRange initialRange_Fortitude = new IntRange(17, 20);
        /// <summary>表示 <c>initialRange_Prudence</c>。</summary>
        public IntRange initialRange_Prudence = new IntRange(17, 20);
        /// <summary>表示 <c>initialRange_Temperance</c>。</summary>
        public IntRange initialRange_Temperance = new IntRange(17, 20);
        /// <summary>表示 <c>initialRange_Justice</c>。</summary>
        public IntRange initialRange_Justice = new IntRange(17, 20);

        /// <summary>初始化 <c>CompProperties_PawnStatus</c> 类的新实例。</summary>
        public CompProperties_PawnStatus()
        {
            compClass = typeof(CompPawnStatus);
        }
    }
}