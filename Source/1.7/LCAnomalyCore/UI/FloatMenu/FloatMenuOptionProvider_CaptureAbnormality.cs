using LCAnomalyCore.Comp;
using LCAnomalyCore.Interface;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.UI.FloatMenu
{
    /// <summary>表示 <c>FloatMenuOptionProvider_CaptureAbnormality</c> 类型。</summary>
    public class FloatMenuOptionProvider_CaptureAbnormality : FloatMenuOptionProvider
    {
        /// <inheritdoc />
        protected override bool Drafted => true;

        /// <inheritdoc />
        protected override bool Undrafted => true;

        /// <inheritdoc />
        protected override bool Multiselect => false;

        /// <inheritdoc />
        protected override bool RequiresManipulation => true;

        /// <inheritdoc />
        protected override bool AppliesInt(FloatMenuContext context)
        {
            return true;
        }

        /// <inheritdoc />
        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!clickedThing.TryGetComp(out CompAbnormalityHoldingPlatformTarget holdComp) || !holdComp.CanBeCaptured || !holdComp.StudiedAtHoldingPlatform)
            {
                yield break;
            }

            if (!context.FirstSelectedPawn.CanReserveAndReach(clickedThing, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, ignoreOtherReservations: true))
            {
                yield return new FloatMenuOption("CannotGenericWorkCustom".Translate("CaptureLower".Translate(clickedThing)) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                yield break;
            }

            IEnumerable<Building> buildings = from x in context.FirstSelectedPawn.Map.listerBuildings.allBuildingsColonist
                                              where x is IAbnormalityContainmentUnit unit
                                                  && !unit.Occupied
                                                  && unit.HolderComp != null
                                                  && unit.HolderComp.CanAcceptAbnormality(clickedThing, out _)
                                                  && context.FirstSelectedPawn.CanReserveAndReach(x, PathEndMode.Touch, Danger.Deadly)
                                              select x;
            Thing building = GenClosest.ClosestThing_Global_Reachable(context.FirstSelectedPawn.Position, context.FirstSelectedPawn.Map, buildings, PathEndMode.ClosestTouch, TraverseParms.For(context.FirstSelectedPawn, Danger.Some), 9999f, null, delegate (Thing t)
            {
                var compEntityHolder = t.TryGetComp<CompAbnormalityHolder>();
                return (compEntityHolder != null && Util.LCContainmentUtility.SafelyContains(t, clickedThing)) ? (compEntityHolder.ContainmentStrength / Mathf.Max(clickedThing.PositionHeld.DistanceTo(t.Position), 1f)) : 0f;
            });
            if (building == null)
            {
                yield return new FloatMenuOption("CannotGenericWorkCustom".Translate("LC_CaptureLower".Translate(clickedThing)) + ": " + "LC_NoHoldingPlatformsAvailable".Translate().CapitalizeFirst(), null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("LC_Capture".Translate(clickedThing.Label, clickedThing), delegate
            {
                bool safeContainerExists = false;
                float minStrength = Util.LCContainmentUtility.GetMinimumContainmentStrength(clickedThing);
                foreach (Building holderBuilding in context.FirstSelectedPawn.Map.listerBuildings.allBuildingsColonist)
                {
                    if (!(holderBuilding is IAbnormalityContainmentUnit unit) || unit.Occupied)
                    {
                        continue;
                    }

                    var holderComp = unit.HolderComp;
                    if (holderComp != null && holderComp.CanAcceptAbnormality(clickedThing, out _) && holderComp.ContainmentStrength >= minStrength)
                    {
                        safeContainerExists = true;
                        break;
                    }
                }

                if (!safeContainerExists)
                {
                    Messages.Message("LC_MessageNoPlatformWithMinimumContainmentStrength".Translate(clickedThing.Label), MessageTypeDefOf.ThreatSmall);
                }

                holdComp.targetHolder = building;
                Job job = JobMaker.MakeJob(Defs.JobDefOf.CarryToAbnormalityHolder, building, clickedThing);
                job.count = 1;
                context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }), context.FirstSelectedPawn, clickedThing);
            if (buildings.Count() > 1)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("LC_Capture".Translate(clickedThing.Label, clickedThing) + " (" + "LC_ChooseEntityHolder".Translate() + "...)", delegate
                {
                    Util.StudyUtil.TargetHoldingPlatformForEntity(context.FirstSelectedPawn, clickedThing);
                }), context.FirstSelectedPawn, clickedThing);
            }
        }
    }
}
