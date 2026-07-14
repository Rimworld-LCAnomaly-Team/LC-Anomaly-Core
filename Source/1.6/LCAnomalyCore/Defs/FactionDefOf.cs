using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    [DefOf]
    public static class FactionDefOf
    {
        public static FactionDef LC_Abnormalities;

        static FactionDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FactionDefOf));
        }
    }
}
