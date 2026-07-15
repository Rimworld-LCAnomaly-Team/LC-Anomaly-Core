using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>表示 <c>StatDefOf</c> 类型。</summary>
    [DefOf]
    public static class StatDefOf
    {
        /// <summary>表示 <c>LC_ContainmentStrength</c>。</summary>
        public static StatDef LC_ContainmentStrength;

        /// <summary>表示 <c>LC_MinimumContainmentStrength</c>。</summary>
        public static StatDef LC_MinimumContainmentStrength;

        static StatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(StatDefOf));
        }
    }
}
