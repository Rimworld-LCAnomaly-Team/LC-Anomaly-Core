using LCAnomalyCore.Building;
using LCAnomalyLibrary.Comp.Pawns;
using LCAnomalyLibrary.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Jobs
{
    public class JobDriver_BecomeEmployee : JobDriver
    {
        private const TargetIndex PawnToBecomeEmployeeIndex = TargetIndex.A;
        private const TargetIndex DepartmentCoreIndex = TargetIndex.B;

        public Pawn PawnToBecomeEmployee => base.TargetThingA as Pawn;
        private CompPawnStatus compStatus => PawnToBecomeEmployee.GetComp<CompPawnStatus>();
        private Building_DepartmentCore DepartmentCore => base.TargetThingB as Building_DepartmentCore;

        public override bool PlayerInterruptable => playerInterruptable;
        protected bool playerInterruptable = true;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (DepartmentCore != null)
            {
                if (pawn.Reserve(DepartmentCore, job, 1, -1, null, errorOnFailed))
                {
                    return pawn.Reserve(DepartmentCore, job, 1, -1, null, errorOnFailed);
                }
                return false;
            }

            return pawn.Reserve(base.TargetThingA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => PawnToBecomeEmployee == null || DepartmentCore == null || compStatus == null || compStatus.Triggered);

            Toil goToAdjacentCell = Toils_Goto.Goto(TargetIndex.B, PathEndMode.ClosestTouch);

            Toil studyToil = BecomeEmoloyee(TargetIndex.A, TargetIndex.B, 120);
            studyToil.AddFinishAction(delegate
            {
                PawnToBecomeEmployee?.GetComp<CompPawnStatus>().TriggerPawnStatus();

                LogUtil.Warning($"成为了员工");
            });
            studyToil.WithEffect(() => EffecterDefOf.StudyHoraxian, base.TargetThingA);

            yield return goToAdjacentCell;
            yield return studyToil;

            //Toil toil = ToilMaker.MakeToil("Study finish");
            //toil.initAction = delegate
            //{
            //    LogUtil.Warning("1");
            //};
            //yield return toil;
        }

        private Toil BecomeEmoloyee(TargetIndex pawnIndex, TargetIndex departmentCoreIndex, int duration)
        {
            Toil study = ToilMaker.MakeToil("Study pawn");
            study.initAction = delegate
            {
                Pawn actor = study.actor;
                actor.pather.StopDead();
                if (actor.CurJob.GetTarget(departmentCoreIndex).Thing is Building_DepartmentCore core)
                    PawnUtility.ForceWait(actor, duration, core);
            };
            study.tickAction = delegate
            {
                LogUtil.Warning($"正在成为员工");
                //study.actor.rotationTracker.FaceTarget(study.actor.CurJob.GetTarget(departmentCoreIndex));
            };
            study.handlingFacing = true;
            study.defaultCompleteMode = ToilCompleteMode.Delay;
            study.defaultDuration = duration;
            study.WithProgressBarToilDelay(pawnIndex);
            return study;
        }

        public override bool? IsSameJobAs(Job j)
        {
            return j.targetA == base.TargetThingA;
        }
    }
}
