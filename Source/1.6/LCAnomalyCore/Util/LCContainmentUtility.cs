using LCAnomalyCore.Comp;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Util
{
    public static class LCContainmentUtility
    {
        public static Faction AbnormalityFaction => Find.FactionManager?.FirstFactionOfDef(Defs.FactionDefOf.LC_Abnormalities);

        public static float GetContainmentStrength(Thing holder)
        {
            return holder?.GetStatValue(Defs.StatDefOf.LC_ContainmentStrength, applyPostProcess: true, cacheStaleAfterTicks: 15) ?? 0f;
        }

        public static float GetMinimumContainmentStrength(Thing abnormality)
        {
            if (abnormality == null)
            {
                return 0f;
            }

            return abnormality.GetStatValue(Defs.StatDefOf.LC_MinimumContainmentStrength);
        }

        public static float GetMinimumContainmentStrength(ThingDef abnormalityDef)
        {
            return abnormalityDef?.GetStatValueAbstract(Defs.StatDefOf.LC_MinimumContainmentStrength) ?? 0f;
        }

        public static bool SafelyContains(Thing holder, Thing abnormality)
        {
            CompAbnormalityHolder holderComp = holder?.TryGetComp<CompAbnormalityHolder>();
            return holderComp != null && holderComp.ContainmentStrength >= GetMinimumContainmentStrength(abnormality);
        }
    }
}
