using RimWorld;
using Verse.AI;
using Verse;
using LCAnomalyLibrary.Comp.Pawns;
using LCAnomalyCore.Building;
using System.Collections.Generic;

namespace LCAnomalyCore.Jobs
{
    public class WorkGiver_BecomeEmployee : WorkGiver_Scanner
    {
        public override bool Prioritized => false;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            //return Find.StudyManager.GetStudiableThingsAndPlatforms(pawn.Map);
            //TODO 这里需要缓存，不然性能会很差
            return pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_DepartmentCore>();
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!forced)
                return true;
            var comp = pawn.GetComp<CompPawnStatus>();

            //不兼容的种族/已激活单位不可重复激活
            if (comp != null)
                return comp.Triggered;
            else
                return true;
        }

        public override string PostProcessedGerund(Job job)
        {
            if (job.targetC == null)
            {
                return base.PostProcessedGerund(job);
            }

            return "DoWorkAtThing".Translate(def.gerund.Named("GERUND"), job.targetC.Label.Named("TARGETLABEL"));
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (t == null)
                return false;

            if (t is not Building_DepartmentCore)
                return false;

            var comp = pawn.GetComp<CompPawnStatus>();
            if (comp == null)
                return false;
            if (!comp.Triggered)
                return true;

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(Defs.JobDefOf.LC_BecomeEmployee, pawn, t);
        }
    }
}
