using LCAnomalyCore.Comp;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>表示 <c>LCContainmentUtility</c> 类型。</summary>
    public static class LCContainmentUtility
    {
        /// <summary>获取 <c>AbnormalityFaction</c>。</summary>
        public static Faction AbnormalityFaction => Find.FactionManager?.FirstFactionOfDef(Defs.FactionDefOf.LC_Abnormalities);

        /// <summary>执行 <c>GetContainmentStrength</c> 定义的操作。</summary>
        public static float GetContainmentStrength(Thing holder)
        {
            return holder?.GetStatValue(Defs.StatDefOf.LC_ContainmentStrength, applyPostProcess: true, cacheStaleAfterTicks: 15) ?? 0f;
        }

        /// <summary>执行 <c>GetMinimumContainmentStrength</c> 定义的操作。</summary>
        public static float GetMinimumContainmentStrength(Thing abnormality)
        {
            if (abnormality == null)
            {
                return 0f;
            }

            return abnormality.GetStatValue(Defs.StatDefOf.LC_MinimumContainmentStrength);
        }

        /// <summary>执行 <c>GetMinimumContainmentStrength</c> 定义的操作。</summary>
        public static float GetMinimumContainmentStrength(ThingDef abnormalityDef)
        {
            return abnormalityDef?.GetStatValueAbstract(Defs.StatDefOf.LC_MinimumContainmentStrength) ?? 0f;
        }

        /// <summary>执行 <c>SafelyContains</c> 定义的操作。</summary>
        public static bool SafelyContains(Thing holder, Thing abnormality)
        {
            CompAbnormalityHolder holderComp = holder?.TryGetComp<CompAbnormalityHolder>();
            return holderComp != null && holderComp.ContainmentStrength >= GetMinimumContainmentStrength(abnormality);
        }
    }
}
