﻿using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Jobs
{
    public class WorkGiver_AbnormalityStudyInteract : WorkGiver_AbnormalityStudyBase
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
            if (t.def is Defs.LC_HoldingPlatformDef && t is Building_AbnormalityHoldingPlatform building)
            {
                var compAbnormalityStudiable = building.CompAbnormalityStudiable;
                if (compAbnormalityStudiable != null)
                {
                    if (compAbnormalityStudiable.KnowledgeCategory == null)
                    {
                        return false;
                    }

                    if (!compAbnormalityStudiable.EverStudiable())
                    {
                        JobFailReason.IsSilent();
                        return false;
                    }

                    if (!compAbnormalityStudiable.CurrentlyStudiable())
                    {
                        if (compAbnormalityStudiable.Props.frequencyTicks > 0 && compAbnormalityStudiable.TicksTilNextStudy > 0)
                        {
                            JobFailReason.Is("CanBeStudiedInDuration".Translate(compAbnormalityStudiable.TicksTilNextStudy.ToStringTicksToPeriod()).CapitalizeFirst());
                        }
                        else
                        {
                            JobFailReason.IsSilent();
                        }

                        return false;
                    }

                    var comp = pawn.GetComp<CompPawnStatus>();
                    //如果没有CompPawnStatus组件或属性未激活，则不兼容
                    if (comp == null || !comp.Enabled)
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
            return JobMaker.MakeJob(Defs.JobDefOf.LC_AbnormalityStudyInteract, t, null, (t as Building_AbnormalityHoldingPlatform)?.HeldPawn);
        }
    }
}