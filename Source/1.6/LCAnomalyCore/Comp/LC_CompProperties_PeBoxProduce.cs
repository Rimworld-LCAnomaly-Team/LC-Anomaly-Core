using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC可产PeBox的CompProperties
    /// </summary>
    public class LC_CompProperties_PeBoxProduce : CompProperties
    {
        #region XML字段

        /// <summary>
        /// XML：工作产生PeBox的类型
        /// </summary>
        public ThingDef peBoxDef;

        public IntRange amountProdueRangeNormal;
        public int amountProdueMax;

        public float boxTexOffsetZ;

        #endregion XML字段

        /// <summary>
        /// Comp
        /// </summary>
        public LC_CompProperties_PeBoxProduce()
        {
            compClass = typeof(LC_CompPeBoxProduce);
        }
    }
}