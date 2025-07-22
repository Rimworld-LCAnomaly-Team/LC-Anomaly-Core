using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Jobs
{
    public abstract class WorkGiver_AbnormalityStudyBase : WorkGiver_Scanner
    {
        public override bool Prioritized => true;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return Find.StudyManager.GetStudiableThingsAndPlatforms(pawn.Map);
        }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            Thing thing = t.Thing;
            if (thing is Building_AbnormalityHoldingPlatform building_HoldingPlatform)
            {
                thing = building_HoldingPlatform.HeldPawn;
            }

            if (thing == null)
            {
                return 0f;
            }

            CompAbnormalityStudiable compStudiable = thing.TryGetComp<CompAbnormalityStudiable>();
            return Find.TickManager.TicksGame - compStudiable.lastStudiedTick;
        }
    }
}
