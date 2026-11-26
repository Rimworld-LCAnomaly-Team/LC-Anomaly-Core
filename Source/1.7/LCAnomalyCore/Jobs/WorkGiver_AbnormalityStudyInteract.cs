using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Util;
using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Jobs
{
    /// <summary>表示 <c>WorkGiver_AbnormalityStudyInteract</c> 类型。</summary>
    public class WorkGiver_AbnormalityStudyInteract : WorkGiver_AbnormalityStudyBase
    {
        /// <inheritdoc />
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            //如果没有CompPawnStatus组件或属性未激活，则不兼容
            var comp = pawn.GetComp<CompPawnStatus>();
            return comp == null || !comp.Enabled;
        }

        /// <inheritdoc />
        public override string PostProcessedGerund(Job job)
        {
            if (job.targetC == null)
            {
                return base.PostProcessedGerund(job);
            }

            return "DoWorkAtThing".Translate(def.gerund.Named("GERUND"), job.targetC.Label.Named("TARGETLABEL"));
        }

        /// <inheritdoc />
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null)
            {
                return false;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (t is IAbnormalityContainmentUnit unit)
            {
                if (unit.IsHoldingTool)
                {
                    JobFailReason.IsSilent();
                    return false;
                }

                if (unit.IsRoomContainment)
                {
                    Pawn roomPawn = unit.HeldPawn;
                    if (roomPawn == null || !roomPawn.Spawned || roomPawn.Dead || roomPawn.Downed)
                    {
                        JobFailReason.IsSilent();
                        return false;
                    }

                    if (!pawn.CanReserveAndReach(roomPawn, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced))
                    {
                        return false;
                    }
                }

                var compAbnormalityStudiable = unit.CompAbnormalityStudiable;
                if (compAbnormalityStudiable != null)
                {
                    if (!compAbnormalityStudiable.CurrentlyStudiable())
                    {
                        CompAbnormality heldAbn = unit.HeldPawn?.TryGetComp<CompAbnormality>();
                        if (heldAbn != null && !heldAbn.CanBeStudiedNow(out string blockReason))
                        {
                            if (!blockReason.NullOrEmpty())
                            {
                                JobFailReason.Is(blockReason);
                            }
                            else
                            {
                                JobFailReason.IsSilent();
                            }
                        }
                        else if (compAbnormalityStudiable.Props.frequencyTicks > 0 && compAbnormalityStudiable.TicksTilNextStudy > 0)
                        {
                            JobFailReason.Is("LC_CanBeStudiedInDuration".Translate(compAbnormalityStudiable.TicksTilNextStudy.ToStringTicksToPeriod()).CapitalizeFirst());
                        }
                        else
                        {
                            JobFailReason.IsSilent();
                        }

                        return false;
                    }

                    //如果是非右键强制状态
                    if (!forced)
                    {
                        if (t is LCAnomalyCore.Buildings.Building_AbnormalityContainmentTerminal term && term.IsInMeltdown)
                        {
                            if (unit.CompAssignable != null && unit.CompAssignable.AssignedPawns.Any())
                            {
                                return unit.CompAssignable.AssignedPawns.Contains(pawn);
                            }
                            return true;
                        }

                        //如果分配列表里有该小人，那就允许自动研究
                        return unit.CompAssignable != null && unit.CompAssignable.AssignedPawns.Contains(pawn);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(Defs.JobDefOf.LC_AbnormalityStudyInteract, t, null, (t as IAbnormalityContainmentUnit)?.HeldPawn);
        }
    }
}
