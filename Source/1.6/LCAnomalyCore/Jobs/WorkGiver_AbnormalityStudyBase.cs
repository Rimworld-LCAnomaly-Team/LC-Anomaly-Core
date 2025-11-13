using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LCAnomalyCore.Jobs
{
    public abstract class WorkGiver_AbnormalityStudyBase : WorkGiver_Scanner
    {
        public override bool Prioritized => true;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // 优化: 使用 ThingsInGroup 代替遍历所有物体
            foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (thing.def is Defs.LC_HoldingPlatformDef)
                {
                    yield return thing;
                }
            }
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

            var compAbnormalityStudiable = thing.TryGetComp<CompAbnormalityStudiable>();
            return Find.TickManager.TicksGame - compAbnormalityStudiable.lastStudiedTick;
        }
    }
}
