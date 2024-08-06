using HarmonyLib;
using LCAnomalyCore.Defs;
using LCAnomalyLibrary.Comp;
using LCAnomalyLibrary.Defs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Patch
{
    [HarmonyPatch(typeof(StudyUtility), nameof(StudyUtility.TargetHoldingPlatformForEntity))]
    public class Patch_TargetHoldingPlatformForEntity
    {
        private static bool Prefix(Pawn carrier, Thing entity, bool transferBetweenPlatforms = false, Thing sourcePlatform = null)
        {
            /* 新增方法开始 */

            Log.Warning("TargetHoldingPlatformForEntity：正在使用LCAnomalyCore.Patch提供的特有方法，而非原版方法");
            bool isLCEntity = entity.TryGetComp<LC_CompEntity>() != null || entity.def is ThingDef_AnomalyEgg;

            /* 新增方法结束 */

            Find.Targeter.BeginTargeting(TargetingParameters.ForBuilding(), delegate (LocalTargetInfo t)
            {
                if (carrier != null && !CanReserveForTransfer(t))
                {
                    Messages.Message("MessageHolderReserved".Translate(t.Thing.Label), MessageTypeDefOf.RejectInput);
                }
                else
                {
                    foreach (Thing item in Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.EntityHolder))
                    {
                        CompHoldingPlatformTarget compHoldingPlatformTarget;

                        if (item is Building_HoldingPlatform building_HoldingPlatform && entity != building_HoldingPlatform.HeldPawn && (compHoldingPlatformTarget = building_HoldingPlatform.HeldPawn?.TryGetComp<CompHoldingPlatformTarget>()) != null && compHoldingPlatformTarget.targetHolder == t.Thing)
                        {
                            Messages.Message("MessageHolderReserved".Translate(t.Thing.Label), MessageTypeDefOf.RejectInput);
                            return;
                        }
                    }

                    CompHoldingPlatformTarget compHoldingPlatformTarget2 = entity.TryGetComp<CompHoldingPlatformTarget>();
                    if (compHoldingPlatformTarget2 != null)
                    {
                        compHoldingPlatformTarget2.targetHolder = t.Thing;
                    }

                    if (carrier != null)
                    {
                        Job job = (transferBetweenPlatforms ? JobMaker.MakeJob(RimWorld.JobDefOf.TransferBetweenEntityHolders, sourcePlatform, t, entity) : JobMaker.MakeJob(RimWorld.JobDefOf.CarryToEntityHolder, t, entity));
                        job.count = 1;
                        carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }

                    if (t.Thing != null && !t.Thing.SafelyContains(entity))
                    {
                        Messages.Message("MessageTargetBelowMinimumContainmentStrength".Translate(t.Thing.Label, entity.Label), MessageTypeDefOf.ThreatSmall);
                    }
                }
            }, delegate (LocalTargetInfo t)
            {
                if (ValidateTarget(t))
                {
                    GenDraw.DrawTargetHighlight(t);
                }
            }, ValidateTarget, null, null, BaseContent.ClearTex, playSoundOnAction: true, delegate (LocalTargetInfo t)
            {
                CompEntityHolder compEntityHolder = t.Thing?.TryGetComp<CompEntityHolder>();

                if (compEntityHolder == null)
                {
                    TaggedString label = "ChooseEntityHolder".Translate().CapitalizeFirst() + "...";
                    Widgets.MouseAttachedLabel(label);
                }
                else
                {
                    Pawn pawn = null;
                    Pawn reserver;

                    if (carrier != null)
                    {
                        pawn = t.Thing.Map.reservationManager.FirstRespectedReserver(t.Thing, carrier);
                    }
                    else if (t.Thing is Building_HoldingPlatform p)
                    {
                        /* 新增方法开始 */

                        bool isLCPlatform = p.def is LC_HoldingPlatformDef;

                        if (StudyUtility.AlreadyReserved(p, out reserver))
                        {
                            if ((isLCEntity && isLCPlatform) || (!isLCEntity && !isLCPlatform))
                                pawn = reserver;
                        }

                        /* 新增方法结束 */
                    }

                    TaggedString label;

                    if (pawn != null)
                    {
                        label = string.Format("{0}: {1}", "EntityHolderReservedBy".Translate(), pawn.LabelShortCap);
                    }
                    else
                    {
                        label = "FloatMenuContainmentStrength".Translate() + ": " + StatDefOf.ContainmentStrength.Worker.ValueToString(compEntityHolder.ContainmentStrength, finalized: false);
                        label += "\n" + ("FloatMenuContainmentRequires".Translate(entity).CapitalizeFirst() + ": " + StatDefOf.MinimumContainmentStrength.Worker.ValueToString(entity.GetStatValue(StatDefOf.MinimumContainmentStrength), finalized: false)).Colorize(t.Thing.SafelyContains(entity) ? Color.white : Color.red);
                    }

                    Widgets.MouseAttachedLabel(label);
                }
            }, delegate
            {
                foreach (Verse.Building item2 in entity.MapHeld.listerBuildings.AllBuildingsColonistOfGroup(ThingRequestGroup.EntityHolder))
                {
                    if (ValidateTarget(item2) && (carrier == null || CanReserveForTransfer(item2)))
                    {
                        GenDraw.DrawArrowPointingAt(item2.DrawPos);
                    }
                }
            });

            return false;

            bool CanReserveForTransfer(LocalTargetInfo t)
            {
                if (transferBetweenPlatforms)
                {
                    if (t.HasThing)
                    {
                        return carrier.CanReserve(t.Thing);
                    }

                    return false;
                }

                return true;
            }

            bool ValidateTarget(LocalTargetInfo t)
            {
                if (t.HasThing && t.Thing.TryGetComp(out CompEntityHolder comp) && comp.HeldPawn == null)
                {
                    /* 新增方法开始 */

                    bool isLCPlatform = t.Thing.def is LC_HoldingPlatformDef;

                    if (!((isLCEntity && isLCPlatform) || (!isLCEntity && !isLCPlatform)))
                    {
                        return false;
                    }

                    /* 新增方法结束 */

                    if (carrier != null)
                    {
                        return carrier.CanReserveAndReach(t.Thing, PathEndMode.Touch, Danger.Some);
                    }

                    return true;
                }

                return false;
            }
        }
    }
}