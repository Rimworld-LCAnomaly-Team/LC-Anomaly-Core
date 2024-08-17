using RimWorld;
using Verse.AI;
using Verse;
using System.Linq;
using LCAnomalyLibrary.Comp.Pawns;

namespace LCAnomalyCore.Jobs
{
    public class WorkGiver_LC_StudyInteract : WorkGiver_StudyBase
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.AnomalyActive;
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

            //如果是LC平台
            if (t.def is Defs.LC_HoldingPlatformDef && t is Building.Building_HoldingPlatform building)
            {
                var compStudiable = building.CompStudiable;
                if (compStudiable != null)
                {
                    if (compStudiable.KnowledgeCategory == null)
                    {
                        return false;
                    }

                    if (!compStudiable.EverStudiable())
                    {
                        JobFailReason.IsSilent();
                        return false;
                    }

                    if (!compStudiable.CurrentlyStudiable())
                    {
                        if (compStudiable.Props.frequencyTicks > 0 && compStudiable.TicksTilNextStudy > 0)
                        {
                            JobFailReason.Is("CanBeStudiedInDuration".Translate(compStudiable.TicksTilNextStudy.ToStringTicksToPeriod()).CapitalizeFirst());
                        }
                        else
                        {
                            JobFailReason.IsSilent();
                        }

                        return false;
                    }

                    //如果没有CompPawnStatus组件就不兼容
                    if (pawn.GetComp<CompPawnStatus>() == null)
                    {
                        JobFailReason.IsSilent();
                        return false;
                    }

                    //如果是非右键强制状态
                    if (!forced)
                    {
                        //如果分配列表里有该小人，那就允许自动研究
                        if (building.CompAssignable.AssignedPawns.Contains(pawn))
                        {
                            return true;
                        }
                        //列表里没有该小人，那就不允许自动研究
                        else
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(Defs.JobDefOf.LC_StudyInteract, t, null, (t as Building.Building_HoldingPlatform)?.HeldPawn);
        }
    }
}
