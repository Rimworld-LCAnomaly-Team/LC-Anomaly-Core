using RimWorld;
using Verse.AI;
using Verse;
using LCAnomalyCore.Comp;
using System.Collections.Generic;
using System.Linq;

namespace LCAnomalyCore.Jobs
{
    public class WorkGiver_TakeAbnormalityToHoldingPlatform : WorkGiver_Scanner
    {
        //public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HoldingPlatformTarget);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings.Where(m => m.HasComp<CompAbnormalityHoldingPlatformTarget>());
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return false;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                JobFailReason.Is("IncapableOfCapacity".Translate(PawnCapacityDefOf.Manipulation.label).CapitalizeFirst());
                return false;
            }

            var compHoldingPlatformTarget = t.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (compHoldingPlatformTarget?.targetHolder == null || compHoldingPlatformTarget.targetHolder.Destroyed || compHoldingPlatformTarget.targetHolder.MapHeld != t.MapHeld || compHoldingPlatformTarget.EntityHolder.HeldPawn != null)
            {
                return false;
            }

            if (!pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Deadly, 1, -1, null, forced))
            {
                return false;
            }

            if (!pawn.CanReserveAndReach(compHoldingPlatformTarget.targetHolder, PathEndMode.ClosestTouch, Danger.Deadly, 1, -1, null, forced))
            {
                return false;
            }

            if (t is Pawn pawn2 && !pawn2.ThreatDisabled(pawn))
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var compHoldingPlatformTarget = t.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (compHoldingPlatformTarget == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(Defs.JobDefOf.CarryToAbnormalityHolder, compHoldingPlatformTarget.targetHolder, t);
            job.count = 1;
            return job;
        }
    }
}
