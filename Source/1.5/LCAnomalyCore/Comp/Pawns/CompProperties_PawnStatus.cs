using Verse;

namespace LCAnomalyCore.Comp.Pawns
{
    /// <summary>
    /// LC员工基本属性CompProperties
    /// </summary>
    public class CompProperties_PawnStatus : CompProperties
    {
        public int statusNumMax = 100;

        public IntRange initialRange_Fortitude = new IntRange(17, 20);
        public IntRange initialRange_Prudence = new IntRange(17, 20);
        public IntRange initialRange_Temperance = new IntRange(17, 20);
        public IntRange initialRange_Justice = new IntRange(17, 20);

        public CompProperties_PawnStatus()
        {
            compClass = typeof(CompPawnStatus);
        }
    }
}