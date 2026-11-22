using LCAnomalyCore.Comp;
using LCAnomalyCore.Defs;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>Identity, discovery and presentation owned by LC, independent of DLC content.</summary>
    public static class AbnormalityUtility
    {
        public static void Burst(IntVec3 position, Map map, float scale = 1f)
        {
            if (map == null || !position.InBounds(map)) return;
            FleckMaker.Static(position, map, DefDatabase<FleckDef>.GetNamed("BloodSplash"), scale);
            FleckMaker.ThrowDustPuff(position.ToVector3Shifted(), map, scale);
        }

        public static void TransferSignature(Thing thing, int signature)
        {
            var abnormality = thing.TryGetComp<CompAbnormality>();
            if (abnormality != null) abnormality.biosignature = signature;
            var egg = thing.TryGetComp<LC_CompEgg>();
            if (egg != null) egg.biosignature = signature;
        }

        public static void Discover(ThingDef thingDef)
        {
            Components.LC?.DiscoverAbnormality(thingDef);
        }

        /// <summary>根据实体或异想体蛋的 Def 查找对应的管理手册条目。</summary>
        public static AbnormalityCodexEntryDef ResolveCodexEntry(ThingDef thingDef)
        {
            if (thingDef == null)
            {
                return null;
            }

            foreach (AbnormalityCodexEntryDef entry in DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading)
            {
                if (entry.defName == thingDef.defName || (entry.linkedThings != null && entry.linkedThings.Contains(thingDef)))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>解析管理手册条目对应的主异想体实体 Def。</summary>
        public static ThingDef ResolvePrimaryAbnormalityDef(AbnormalityCodexEntryDef entry)
        {
            if (entry == null)
            {
                return null;
            }

            ThingDef direct = DefDatabase<ThingDef>.GetNamedSilentFail(entry.defName);
            if (IsPrimaryAbnormalityDef(direct))
            {
                return direct;
            }

            if (entry.linkedThings != null)
            {
                foreach (ThingDef linked in entry.linkedThings)
                {
                    if (IsPrimaryAbnormalityDef(linked))
                    {
                        return linked;
                    }
                }
            }

            return direct;
        }

        private static bool IsPrimaryAbnormalityDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (def.race != null && def.HasComp(typeof(CompAbnormality)))
            {
                return true;
            }

            return def is ThingDef_AbnormalityTool || def.HasComp(typeof(CompAbnormalityTool));
        }
    }
}
