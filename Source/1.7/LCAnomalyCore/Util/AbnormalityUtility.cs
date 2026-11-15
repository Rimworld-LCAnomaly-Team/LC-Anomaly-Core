using LCAnomalyCore.Comp;
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
    }
}
