using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 提取仪式点的CompProperties（和原版基本一致）
    /// </summary>
    public class CompProperties_ExtractRitualSpot : CompProperties
    {
        public CompProperties_ExtractRitualSpot()
        {
            compClass = typeof(CompExtractRitualSpot);
        }
    }
}