using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Jobs
{
    /// <summary>表示 <c>JobDriver_AbnormalityStudyInteract</c> 类型。</summary>
    public class JobDriver_AbnormalityStudyInteract : JobDriver
    {
        private const TargetIndex ThingToStudyIndex = TargetIndex.A;

        private const TargetIndex AdjacentCellIndex = TargetIndex.B;

        private const TargetIndex RoomPawnIndex = TargetIndex.C;

        private const int BaseDurationTicks = 600;

        private const int PawnTargetStudyDurationFactor = 2;

        /// <summary>表示 <c>ElectroharvesterFactor</c>。</summary>
        public const float ElectroharvesterFactor = 0.5f;

        private int studyInteractions;

        /// <inheritdoc />
        public override bool PlayerInterruptable => playerInterruptable;
        /// <summary>表示 <c>playerInterruptable</c>。</summary>
        protected bool playerInterruptable = true;

        private IAbnormalityContainmentUnit Unit => base.TargetThingA as IAbnormalityContainmentUnit;

        /// <summary>获取 <c>ThingToStudy</c>。</summary>
        public Thing ThingToStudy => Unit?.HeldPawn ?? base.TargetThingA;

        private CompAbnormalityStudiable StudyComp => ThingToStudy?.TryGetComp<CompAbnormalityStudiable>();

        /// <inheritdoc />
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Unit != null)
            {
                if (pawn.Reserve(Unit.UnitThing, job, 1, -1, null, errorOnFailed))
                {
                    return ThingToStudy == null || ThingToStudy == Unit.UnitThing || pawn.Reserve(ThingToStudy, job, 1, -1, null, errorOnFailed);
                }
                return false;
            }

            return pawn.Reserve(base.TargetThingA, job, 1, -1, null, errorOnFailed);
        }

        /// <inheritdoc />
        protected override IEnumerable<Toil> MakeNewToils()
        {
            bool targetIsPawn = base.TargetThingA is Pawn;
            bool isRoomContainment = Unit != null && Unit.IsRoomContainment && ThingToStudy is Pawn roomPawn && roomPawn.Spawned;
            if (isRoomContainment && job.targetC.Thing == null)
            {
                job.SetTarget(RoomPawnIndex, ThingToStudy);
            }

            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => (StudyComp == null || !StudyComp.CurrentlyStudiable()) ? true : false);
            if (isRoomContainment)
            {
                this.FailOn(() => !(ThingToStudy is Pawn rp) || !rp.Spawned || rp.Dead || rp.Downed || rp.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.CurrentlyContainedInRoom != true);
            }

            AddFinishAction(delegate
            {
                if (StudyComp != null)
                {
                    StudyComp.studyInteractions += studyInteractions;
                }
            });

            int numModified = 0;
            int durationTicks = 0;
            int duration = 0;
            //Log.Warning(base.TargetThingA.def.defName);
            if (ThingToStudy != null)
            {
                CompPeBoxProduce comp = ThingToStudy.TryGetComp<CompPeBoxProduce>();
                if (comp != null)
                {
                    //每点自律提供1%工作速度加成
                    float statusRate = pawn.GetComp<CompPawnStatus>().GetPawnStatusLevel(EPawnStatus.Temperance).Status * 0.01f;
                    float unlockRate = ThingToStudy.TryGetComp<CompAbnormalityStudiable>().GetWorkSpeedOffset();
                    float workSpeed = pawn.GetStatValue(Defs.StatDefOf.LC_AbnormalityStudyRate);
                    duration = System.Math.Max(1, (int)(300 / ((1 + statusRate + unlockRate) * workSpeed)));
                    numModified = duration * comp.Props.amountProdueMax + duration;
                    LogUtil.Warning($"Study amount: {numModified}, Study duration: {duration}\nStatusRate: {statusRate}, UnlockRate: {unlockRate}");

                    Unit?.Notify_StudyStart(pawn);
                }
            }

            CompPawnStatus compPawnStatus = pawn.GetComp<CompPawnStatus>();

            Toil findAdjacentCell = Toils_General.Do(delegate
            {
                IntVec3 adjacentInteractionCell_NewTemp = SocialInteractionUtility.GetAdjacentInteractionCell(pawn, job.GetTarget(TargetIndex.A).Thing, job.playerForced);
                pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, adjacentInteractionCell_NewTemp);
                job.targetB = adjacentInteractionCell_NewTemp;
            });
            Toil goToAdjacentCell = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil studyToil;
            if (targetIsPawn)
            {
                studyToil = StudyPawn(TargetIndex.A, numModified);
            }
            else if (isRoomContainment)
            {
                studyToil = StudyPawn(RoomPawnIndex, numModified);
            }
            else
            {
                studyToil = Toils_General.WaitWith(TargetIndex.A, numModified, useProgressBar: true, maintainPosture: false, maintainSleep: false, TargetIndex.A);
                studyToil.AddPreTickIntervalAction(delegate (int delta)
                {
                    ThingToStudy.TryGetComp<CompObelisk>()?.Notify_InteractedTick(pawn, delta);
                });
            }

            studyToil.AddPreTickAction(delegate
            {
                //Log.Warning($"正在进行对 [{ThingToStudy.def.label.Translate()}] 的研究工作");
                playerInterruptable = false;
                Unit?.Notify_Studying(pawn);
                if (duration != 0)
                {
                    durationTicks++;
                    if (durationTicks % duration == 0)
                    {
                        //Log.Warning("触发一次成功率判断");
                        Unit?.Notify_StudyInterval(compPawnStatus);
                    }
                }
                //pawn.skills.Learn(SkillDefOf.Intellectual, 0.1f);
            });
            studyToil.AddFinishAction(delegate
            {
                if (durationTicks < numModified - 1)
                {
                    LogUtil.Warning($"Abnormality study inturrpted");
                    ThingToStudy.TryGetComp<CompAbnormality>()?.Notify_Studied(pawn);
                }
            });

            if (ModsConfig.AnomalyActive && EffecterDefOf.StudyHoraxian != null)
                studyToil.WithEffect(() => EffecterDefOf.StudyHoraxian, isRoomContainment ? ThingToStudy : base.TargetThingA);

            Toil finishInteraction = ToilMaker.MakeToil("Interaction finish");
            finishInteraction.initAction = DoStudyInteraction;
            yield return Toils_Goto.GotoThing(isRoomContainment ? RoomPawnIndex : TargetIndex.A, PathEndMode.Touch);
            int interactions = StudyComp.StudyTimesPeriod - StudyComp.studyInteractions;
            for (int i = 0; i < interactions; i++)
            {
                if (i > 0)
                {
                    if (isRoomContainment)
                    {
                        yield return Toils_Goto.GotoThing(RoomPawnIndex, PathEndMode.Touch);
                    }
                    else if (!targetIsPawn)
                    {
                        yield return findAdjacentCell;
                        yield return goToAdjacentCell;
                    }
                }

                yield return studyToil;
                yield return finishInteraction;
            }

            Toil toil = ToilMaker.MakeToil("Study finish");
            toil.initAction = delegate
            {
                studyInteractions = 0;
                StudyComp.studyInteractions = 0;
                StudyComp.lastStudiedTick = Find.TickManager.TicksGame;
                ThingToStudy.TryGetComp<CompAbnormality>()?.Notify_Studied(pawn);
                if (ModsConfig.AnomalyActive && TaleDefOf.StudiedEntity != null && ThingToStudy is Pawn entityPawn && (!entityPawn.RaceProps.Humanlike || entityPawn.IsMutant))
                {
                    TaleRecorder.RecordTale(TaleDefOf.StudiedEntity, base.pawn, entityPawn);
                }
            };
            yield return toil;
        }

        private Toil StudyPawn(TargetIndex pawnIndex, int duration)
        {
            Toil study = ToilMaker.MakeToil("Study pawn");
            study.initAction = delegate
            {
                Pawn actor = study.actor;
                actor.pather.StopDead();
                if (actor.CurJob.GetTarget(pawnIndex).Thing is Pawn recipient)
                {
                    PawnUtility.ForceWait(recipient, duration + 30, study.actor);
                    if (InteractionDefOf.PrisonerStudyAnomaly != null)
                        actor.interactions.TryInteractWith(recipient, InteractionDefOf.PrisonerStudyAnomaly);
                }
            };
            study.tickAction = delegate
            {
                study.actor.rotationTracker.FaceTarget(study.actor.CurJob.GetTarget(pawnIndex));
                if (study.actor.CurJob.GetTarget(pawnIndex).Thing is Pawn recipient && recipient.Spawned)
                {
                    recipient.pather?.StopDead();
                    recipient.rotationTracker?.FaceCell(study.actor.Position);
                }
            };
            study.handlingFacing = true;
            study.defaultCompleteMode = ToilCompleteMode.Delay;
            study.defaultDuration = duration;
            study.WithProgressBarToilDelay(pawnIndex);
            return study;
        }

        private void DoStudyInteraction()
        {
            StudyComp.Study(base.pawn, 0.87f);
            studyInteractions++;
            if (ThingToStudy is Pawn pawn)
            {
                pawn.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                pawn.mindState.interactionsToday++;
            }
        }

        /// <inheritdoc />
        public override bool? IsSameJobAs(Job j)
        {
            return j.targetA == base.TargetThingA;
        }

        /// <inheritdoc />
        protected override string ReportStringProcessed(string str)
        {
            return JobUtility.GetResolvedJobReport(str, ThingToStudy);
        }

        /// <inheritdoc />
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref studyInteractions, "studyInteractions", 0);
        }
    }
}
