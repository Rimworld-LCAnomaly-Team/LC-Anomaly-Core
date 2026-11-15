using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LCAnomalyCore.Jobs
{
    /// <summary>表示 <c>WorkGiver_AbnormalityStudyBase</c> 类型。</summary>
    public abstract class WorkGiver_AbnormalityStudyBase : WorkGiver_Scanner
    {
        /// <inheritdoc />
        public override bool Prioritized => true;

        /// <inheritdoc />
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings.Where(m => m.def is Defs.LC_HoldingPlatformDef);
        }

        /// <inheritdoc />
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
            return compAbnormalityStudiable == null ? 0f : Find.TickManager.TicksGame - compAbnormalityStudiable.lastStudiedTick;
        }
    }
}
