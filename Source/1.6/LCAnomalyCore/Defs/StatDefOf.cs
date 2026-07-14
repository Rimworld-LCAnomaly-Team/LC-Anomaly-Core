using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    [DefOf]
    public static class StatDefOf
    {
        public static StatDef LC_ContainmentStrength;

        public static StatDef LC_MinimumContainmentStrength;

        static StatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(StatDefOf));
        }
    }
}
