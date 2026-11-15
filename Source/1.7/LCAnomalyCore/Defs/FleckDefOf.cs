using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该Mod所有的FleckDef
    /// </summary>
    [DefOf]
    public static class FleckDefOf
    {
        /// <summary>
        /// 工作质量为好
        /// </summary>
        public static FleckDef WorkResult_Good;

        /// <summary>
        /// 工作质量为普通
        /// </summary>
        public static FleckDef WorkResult_Normal;

        /// <summary>
        /// 工作质量为差
        /// </summary>
        public static FleckDef WorkResult_Bad;
    }
}