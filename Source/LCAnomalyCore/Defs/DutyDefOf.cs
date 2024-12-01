using RimWorld;
using Verse.AI;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该Mod所有的DutyDef
    /// </summary>
    [DefOf]
    public static class DutyDefOf
    {
        static DutyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DutyDefOf));
        }

        /// <summary>
        /// 为了执行提取仪式而收集祭品
        /// </summary>
        public static DutyDef GatherOfferingsForExtractRitual;
    }
}