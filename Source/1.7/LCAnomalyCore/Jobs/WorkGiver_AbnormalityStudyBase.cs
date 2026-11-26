using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Interface;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
            return pawn.Map.listerBuildings.allBuildingsColonist.Where(m => m is IAbnormalityContainmentUnit);
        }

        /// <inheritdoc />
        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            Thing thing = t.Thing;
            if (thing is Building_AbnormalityContainmentTerminal terminal && terminal.IsInMeltdown)
            {
                return 100000000f + Mathf.Max(0f, 100000f - terminal.MeltdownTicksRemaining);
            }

            if (thing is IAbnormalityContainmentUnit unit)
            {
                thing = unit.HeldPawn;
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
