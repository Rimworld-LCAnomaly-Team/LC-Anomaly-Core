using LCAnomalyCore.Comp;
using LCAnomalyLibrary.Comp;
using LCAnomalyLibrary.Comp.Pawns;
using LCAnomalyLibrary.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Jobs
{
    public class JobDriver_StudyInteract : JobDriver
    {
        private const TargetIndex ThingToStudyIndex = TargetIndex.A;

        private const TargetIndex AdjacentCellIndex = TargetIndex.B;

        private const int BaseDurationTicks = 600;

        private const int PawnTargetStudyDurationFactor = 2;

        public const float ElectroharvesterFactor = 0.5f;

        private int studyInteractions;

        //public new bool PlayerInterruptable = true;

        public override bool PlayerInterruptable => playerInterruptable;
        protected bool playerInterruptable = true;

        private Building.Building_HoldingPlatform Platform => base.TargetThingA as Building.Building_HoldingPlatform;

        public Thing ThingToStudy => Platform?.HeldPawn ?? base.TargetThingA;

        private LC_CompStudiable StudyComp => ThingToStudy?.TryGetComp<LC_CompStudiable>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Platform != null)
            {
                if (pawn.Reserve(Platform, job, 1, -1, null, errorOnFailed))
                {
                    return pawn.Reserve(ThingToStudy, job, 1, -1, null, errorOnFailed);
                }
                return false;
            }

            return pawn.Reserve(base.TargetThingA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            bool targetIsPawn = base.TargetThingA is Pawn;
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => (StudyComp == null || !StudyComp.CurrentlyStudiable()) ? true : false);
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
                if(comp != null)
                {
                    //每点自律提供1%工作速度加成
                    float statusRate = pawn.GetComp<CompPawnStatus>().GetPawnStatusLevel(EPawnStatus.Temperance).Status * 0.01f;
                    float unlockRate = ThingToStudy.TryGetComp<LC_CompStudiable>().GetWorkSpeedOffset();
                    duration = (int)(300 * (1 / (1 + statusRate + unlockRate)));
                    numModified = duration * comp.Props.amountProdueMax + duration;
                    LogUtil.Warning($"Study amount: {numModified}, Study duration: {duration}\nStatusRate: {statusRate}, UnlockRate: {unlockRate}");

                    Platform.Notify_StudyStart(pawn);
                }
            }

            CompPawnStatus compPawnStatus = pawn.GetComp<CompPawnStatus>();

            Toil findAdjacentCell = Toils_General.Do(delegate
            {
                IntVec3 adjacentInteractionCell_NewTemp = InteractionUtility.GetAdjacentInteractionCell_NewTemp(pawn, job.GetTarget(TargetIndex.A).Thing, job.playerForced);
                pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, adjacentInteractionCell_NewTemp);
                job.targetB = adjacentInteractionCell_NewTemp;
            });
            Toil goToAdjacentCell = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil studyToil;
            if (targetIsPawn)
            {
                studyToil = StudyPawn(TargetIndex.A, numModified);
            }
            else
            {
                studyToil = Toils_General.WaitWith(TargetIndex.A, numModified, useProgressBar: true, maintainPosture: false, maintainSleep: false, TargetIndex.A);
                studyToil.AddPreTickAction(delegate
                {
                    ThingToStudy.TryGetComp<CompObelisk>()?.Notify_InteractedTick(pawn);
                });
            }

            studyToil.AddPreTickAction(delegate
            {
                //Log.Warning($"正在进行对 [{ThingToStudy.def.label.Translate()}] 的研究工作");
                playerInterruptable = false;
                Platform?.Notify_Studying(pawn);
                if (duration != 0)
                {
                    durationTicks++;
                    if (durationTicks % duration == 0)
                    {
                        //Log.Warning("触发一次成功率判断");
                        Platform?.Notify_StudyInterval(compPawnStatus);
                    }
                }
                //pawn.skills.Learn(SkillDefOf.Intellectual, 0.1f);
            });
            studyToil.AddFinishAction(delegate
            {
                if (durationTicks < numModified - 1)
                {
                    LogUtil.Warning($"Abnormality study inturrpted");
                    ThingToStudy.TryGetComp<LC_CompEntity>()?.Notify_Studied(pawn);
                }
            });
            //studyToil.activeSkill = () => SkillDefOf.Intellectual;
            if (StudyComp.KnowledgeCategory != null)
            {
                studyToil.WithEffect(() => EffecterDefOf.StudyHoraxian, base.TargetThingA);
            }

            Toil finishInteraction = ToilMaker.MakeToil("Interaction finish");
            finishInteraction.initAction = DoStudyInteraction;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            int interactions = StudyComp.StudyTimesPeriod - StudyComp.studyInteractions;
            for (int i = 0; i < interactions; i++)
            {
                if (!targetIsPawn && i > 0)
                {
                    yield return findAdjacentCell;
                    yield return goToAdjacentCell;
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
                if (ModsConfig.AnomalyActive && ThingToStudy is Pawn pawn && (!pawn.RaceProps.Humanlike || pawn.IsMutant))
                {
                    TaleRecorder.RecordTale(TaleDefOf.StudiedEntity, base.pawn, pawn);
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
                    PawnUtility.ForceWait(recipient, duration, study.actor);
                    actor.interactions.TryInteractWith(recipient, InteractionDefOf.PrisonerStudyAnomaly);
                }
            };
            study.tickAction = delegate
            {
                study.actor.rotationTracker.FaceTarget(study.actor.CurJob.GetTarget(pawnIndex));
            };
            study.handlingFacing = true;
            study.defaultCompleteMode = ToilCompleteMode.Delay;
            study.defaultDuration = duration;
            study.WithProgressBarToilDelay(pawnIndex);
            return study;
        }

        private void DoStudyInteraction()
        {
            float anomalyKnowledgeAmount = 0f;
            if (ModsConfig.AnomalyActive)
            {
                anomalyKnowledgeAmount = StudyComp.AdjustedAnomalyKnowledgePerStudy * base.pawn.GetStatValue(StatDefOf.StudyEfficiency);
            }

            StudyComp.Study(base.pawn, 0.87f, anomalyKnowledgeAmount);
            studyInteractions++;
            if (ModsConfig.AnomalyActive && ThingToStudy is Pawn pawn)
            {
                pawn.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                pawn.mindState.interactionsToday++;
            }
        }

        public override bool? IsSameJobAs(Job j)
        {
            return j.targetA == base.TargetThingA;
        }

        protected override string ReportStringProcessed(string str)
        {
            return JobUtility.GetResolvedJobReport(str, ThingToStudy);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref studyInteractions, "studyInteractions", 0);
        }
    }
}
