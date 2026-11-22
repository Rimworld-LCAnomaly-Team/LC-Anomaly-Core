using System;
using System.Collections.Generic;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAnomalyCore.Jobs
{
    /// <summary>执行与工具型异想体交互的工作。</summary>
    public class JobDriver_UseAbnormalityTool : JobDriver
    {
        private CompAbnormalityTool ToolComp =>
            (TargetThingA as Building_AbnormalityHoldingPlatform)?.HeldPawn?.TryGetComp<CompAbnormalityTool>()
            ?? TargetThingA?.TryGetComp<CompAbnormalityTool>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => ToolComp == null || !ToolComp.CanUseTool(pawn).Accepted);

            CompAbnormalityTool comp = ToolComp;
            PathEndMode peMode = comp != null
                ? comp.GetInteractionPathEndMode(TargetThingA)
                : (TargetThingA != null && TargetThingA.def.hasInteractionCell
                    ? PathEndMode.InteractionCell
                    : PathEndMode.Touch);

            yield return Toils_Goto.GotoThing(TargetIndex.A, peMode);

            comp = ToolComp;
            if (comp?.Props?.toolMode == AbnormalityToolMode.Continuous && comp.Props.enclosesUserInside)
            {
                Toil enter = ToilMaker.MakeToil("EnterEnclosedAbnormalityTool");
                enter.initAction = delegate
                {
                    CompAbnormalityTool activeComp = ToolComp;
                    if (activeComp == null || !activeComp.TryAcceptEnclosedUser(pawn))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (activeComp.Props.soundUsed != null && TargetThingA?.MapHeld != null)
                    {
                        // Entrance snap sound if desired; continuous soundUsing runs inside TickEnclosedContinuous.
                    }
                };
                enter.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return enter;
                yield break;
            }

            if (comp?.Props?.toolMode == AbnormalityToolMode.Continuous)
            {
                Toil channel = ToilMaker.MakeToil("ContinuousUseAbnormalityTool");
                channel.defaultCompleteMode = ToilCompleteMode.Never;
                int sessionTicks = 0;
                int tickAccumulator = 0;
                bool started = false;
                bool ended = false;
                bool naturalCompletion = false;
                CompAbnormalityTool startedComp = null;

                channel.initAction = delegate
                {
                    CompAbnormalityTool activeComp = ToolComp;
                    if (activeComp == null || !activeComp.CanUseTool(pawn).Accepted)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    startedComp = activeComp;
                    activeComp.OnContinuousStart(pawn);
                    started = true;
                };

                channel.tickAction = delegate
                {
                    pawn.rotationTracker.FaceTarget(TargetA);
                    CompAbnormalityTool activeComp = ToolComp;
                    if (activeComp == null)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    sessionTicks++;
                    tickAccumulator++;
                    int interval = Math.Max(1, activeComp.Props.continuousTickInterval);
                    if (tickAccumulator >= interval)
                    {
                        int delta = tickAccumulator;
                        tickAccumulator = 0;
                        activeComp.OnContinuousTick(pawn, delta, sessionTicks);
                    }

                    bool maxReached = activeComp.Props.maxContinuousDurationTicks > 0
                        && sessionTicks >= activeComp.Props.maxContinuousDurationTicks;
                    if (activeComp.StopRequested || maxReached)
                    {
                        naturalCompletion = true;
                        ReadyForNextToil();
                    }
                    else if (pawn.Dead || pawn.Downed)
                    {
                        ReadyForNextToil();
                    }
                };

                channel.AddEndCondition(delegate
                {
                    CompAbnormalityTool activeComp = ToolComp;
                    if (activeComp == null || pawn.Dead || pawn.Downed)
                    {
                        return JobCondition.Incompletable;
                    }

                    bool maxReached = activeComp.Props.maxContinuousDurationTicks > 0
                        && sessionTicks >= activeComp.Props.maxContinuousDurationTicks;
                    return (activeComp.StopRequested || maxReached) ? JobCondition.Succeeded : JobCondition.Ongoing;
                });

                channel.AddFinishAction(delegate
                {
                    CompAbnormalityTool activeComp = ToolComp ?? startedComp;
                    if (started && !ended)
                    {
                        ended = true;
                        activeComp?.OnContinuousEnd(pawn, sessionTicks, naturalCompletion);
                    }

                    if (naturalCompletion
                        && activeComp?.Props?.soundUsed != null
                        && TargetThingA?.MapHeld != null)
                    {
                        activeComp.Props.soundUsed.PlayOneShot(new TargetInfo(TargetThingA.PositionHeld, TargetThingA.MapHeld));
                    }
                });

                if (comp.Props.soundUsing != null)
                {
                    channel.PlaySustainerOrSound(comp.Props.soundUsing);
                }

                yield return channel;
                yield break;
            }

            int duration = comp != null ? Math.Max(1, comp.Props.useDurationTicks) : 60;
            Toil wait = Toils_General.WaitWith(
                TargetIndex.A,
                duration,
                useProgressBar: true,
                face: TargetIndex.A,
                pathEndMode: peMode);

            if (comp?.Props?.soundUsing != null)
            {
                wait.PlaySustainerOrSound(comp.Props.soundUsing);
            }

            yield return wait;

            Toil complete = ToilMaker.MakeToil("CompleteUseAbnormalityTool");
            complete.initAction = delegate
            {
                CompAbnormalityTool activeComp = ToolComp;
                if (activeComp == null || !activeComp.CanUseTool(pawn).Accepted)
                {
                    return;
                }

                Thing toolThing = TargetThingA;
                SoundDef finishSound = activeComp.Props.soundUsed;
                activeComp.OnToolUsed(pawn);

                if (finishSound != null && toolThing != null && toolThing.MapHeld != null)
                {
                    finishSound.PlayOneShot(new TargetInfo(toolThing.PositionHeld, toolThing.MapHeld));
                }
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }
}
