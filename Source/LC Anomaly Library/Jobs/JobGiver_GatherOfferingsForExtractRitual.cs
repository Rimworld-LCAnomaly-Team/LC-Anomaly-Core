using LCAnomalyLibrary.Defs;
using LCAnomalyLibrary.Toils;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LCAnomalyLibrary.Jobs
{
    /// <summary>
    /// 仪式收集祭品工作（和原版基本一致）
    /// </summary>
    public class JobGiver_GatherOfferingsForExtractRitual : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Lord lord;
            if ((lord = pawn.GetLord()) == null)
            {
                return null;
            }

            if (!(lord.CurLordToil is LordToil_PsychicRitual lordToil_PsychicRitual))
            {
                return null;
            }

            ExtractRitualDef_InvocationCircle ritualDef;
            if ((ritualDef = lordToil_PsychicRitual.RitualData.psychicRitual.def as ExtractRitualDef_InvocationCircle) == null)
            {
                return null;
            }

            if (ritualDef.RequiredOffering == null)
            {
                return null;
            }

            PsychicRitual psychicRitual = lordToil_PsychicRitual.RitualData.psychicRitual;
            PsychicRitualRoleDef role;
            if ((role = psychicRitual.assignments.RoleForPawn(pawn)) == null)
            {
                return null;
            }

            float num = ExtractRitualToil_GatherOfferings.PawnsOfferingCount(psychicRitual.assignments.AssignedPawns(role), ritualDef.RequiredOffering);
            int needed = Mathf.CeilToInt(ritualDef.RequiredOffering.GetBaseCount() - num);
            if (needed == 0)
            {
                return null;
            }

            Thing thing2 = GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways), PathEndMode.Touch, TraverseParms.For(pawn), 9999f, delegate (Thing thing)
            {
                if (!ritualDef.RequiredOffering.filter.Allows(thing))
                {
                    return false;
                }

                if (thing.IsForbidden(pawn))
                {
                    return false;
                }

                int stackCount = Mathf.Min(needed, thing.stackCount);
                return pawn.CanReserve(thing, 10, stackCount);
            });
            if (thing2 == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, thing2);
            job.count = Mathf.Min(needed, thing2.stackCount);
            return job;
        }
    }
}