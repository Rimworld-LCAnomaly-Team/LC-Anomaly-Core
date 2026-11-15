using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>表示 <c>FactionDefOf</c> 类型。</summary>
    [DefOf]
    public static class FactionDefOf
    {
        /// <summary>表示 <c>LC_Abnormalities</c>。</summary>
        public static FactionDef LC_Abnormalities;

        static FactionDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FactionDefOf));
        }
    }
}
