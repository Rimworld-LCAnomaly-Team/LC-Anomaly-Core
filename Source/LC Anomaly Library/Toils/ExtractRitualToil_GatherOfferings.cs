using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LCAnomalyLibrary.Toils
{
    /// <summary>
    /// 提取意识的聚集Toil（和原版基本一致）
    /// </summary>
    public class ExtractRitualToil_GatherOfferings : PsychicRitualToil
    {
        protected PsychicRitualRoleDef gathererRole;

        protected bool offeringsGathered;

        protected IngredientCount requiredOffering;

        protected ExtractRitualToil_GatherOfferings()
        {
        }

        public ExtractRitualToil_GatherOfferings(PsychicRitualRoleDef offeringGatherer, IngredientCount requiredOffering)
        {
            gathererRole = offeringGatherer;
            this.requiredOffering = requiredOffering;
        }

        public override void UpdateAllDuties(PsychicRitual psychicRitual, PsychicRitualGraph parent)
        {
            DutyDef def;
            if (offeringsGathered || PawnsHaveOfferings(psychicRitual))
            {
                offeringsGathered = true;
                def = DutyDefOf.Idle;
            }
            else
            {
                def = Defs.DutyDefOf.GatherOfferingsForExtractRitual;
            }

            foreach (Pawn item in psychicRitual.assignments.AssignedPawns(gathererRole))
            {
                SetPawnDuty(item, psychicRitual, parent, def);
            }
        }

        public override bool Tick(PsychicRitual psychicRitual, PsychicRitualGraph parent)
        {
            return offeringsGathered;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref gathererRole, "gathererRole");
            Scribe_Values.Look(ref offeringsGathered, "offeringsGathered", defaultValue: false);
            Scribe_Deep.Look(ref requiredOffering, "requiredOffering");
        }

        public static float PawnsOfferingCount(IEnumerable<Pawn> pawns, IngredientCount offering)
        {
            float num = 0f;
            foreach (Pawn pawn in pawns)
            {
                foreach (var item in pawn.inventory.GetDirectlyHeldThings().Where(item => offering.filter.Allows(item)))
                {
                    num += (float)item.stackCount;
                    if (num >= offering.GetBaseCount())
                    {
                        return offering.GetBaseCount();
                    }
                }
            }

            return num;
        }

        private bool PawnsHaveOfferings(PsychicRitual psychicRitual)
        {
            float baseCount = requiredOffering.GetBaseCount();
            return PawnsOfferingCount(psychicRitual.assignments.AssignedPawns(gathererRole), requiredOffering) >= baseCount;
        }

        public override void Notify_PawnJobDone(PsychicRitual psychicRitual, PsychicRitualGraph parent, Pawn pawn, Job job, JobCondition condition)
        {
            base.Notify_PawnJobDone(psychicRitual, parent, pawn, job, condition);
            if (psychicRitual.assignments.RoleForPawn(pawn) == gathererRole && (offeringsGathered || PawnsHaveOfferings(psychicRitual)))
            {
                offeringsGathered = true;
                SetPawnDuty(pawn, psychicRitual, parent, DutyDefOf.Idle);
            }
        }

        public override ThinkResult Notify_DutyResult(PsychicRitual psychicRitual, PsychicRitualGraph parent, ThinkResult result, Pawn pawn, JobIssueParams issueParams)
        {
            result = base.Notify_DutyResult(psychicRitual, parent, result, pawn, issueParams);

            if (result.Job != null)
            {
                return result;
            }

            if (psychicRitual.assignments.RoleForPawn(pawn) != gathererRole)
            {
                return result;
            }

            if (offeringsGathered || PawnsHaveOfferings(psychicRitual))
            {
                offeringsGathered = true;
                SetPawnDuty(pawn, psychicRitual, parent, DutyDefOf.Idle);
                return new ThinkResult(JobMaker.MakeJob(JobDefOf.Wait, 1), null);
            }

            TaggedString reason = "PsychicRitualToil_GatherOfferings_OfferingUnavailable".Translate(pawn.Named("PAWN"), requiredOffering.filter.Summary);
            psychicRitual.LeaveOrCancelPsychicRitual(gathererRole, pawn, reason);
            return result;
        }

        public override string GetJobReport(PsychicRitual psychicRitual, PsychicRitualGraph parent, Pawn pawn)
        {
            if (psychicRitual.assignments.RoleForPawn(pawn) == gathererRole)
            {
                return "PsychicRitualToil_GatherOfferings_JobReport".Translate();
            }

            return base.GetJobReport(psychicRitual, parent, pawn);
        }
    }
}