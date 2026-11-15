using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC可产PeBox的CompProperties
    /// </summary>
    public class CompProperties_PeBoxProduce : CompProperties
    {
        #region XML字段

        /// <summary>
        /// XML：工作产生PeBox的类型
        /// </summary>
        public ThingDef peBoxDef;

        /// <summary>表示 <c>amountProdueRangeNormal</c>。</summary>
        public IntRange amountProdueRangeNormal;
        /// <summary>表示 <c>amountProdueMax</c>。</summary>
        public int amountProdueMax;

        /// <summary>表示 <c>boxTexOffsetZ</c>。</summary>
        public float boxTexOffsetZ;

        #endregion XML字段

        /// <summary>
        /// Comp
        /// </summary>
        public CompProperties_PeBoxProduce()
        {
            compClass = typeof(CompPeBoxProduce);
        }
    }
}