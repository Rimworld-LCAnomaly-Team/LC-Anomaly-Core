using LCAnomalyLibrary.Defs;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace LCAnomalyLibrary.Gizmo
{
    /// <summary>
    /// 仪式Gizmo（和原版基本一致）
    /// </summary>
    public static class ExtractRitualGizmo
    {
        private static readonly List<ExtractRitualDef_InvocationCircle> tmpVisibleRituals = new List<ExtractRitualDef_InvocationCircle>(16);

        public static IEnumerable<Verse.Gizmo> GetGizmos(Thing target)
        {
            IOrderedEnumerable<ExtractRitualDef_InvocationCircle> orderedEnumerable = from ritualDef in VisibleRituals()
                                                                                      orderby ritualDef.label
                                                                                      select ritualDef;
            foreach (ExtractRitualDef_InvocationCircle ritualDef2 in orderedEnumerable)
            {
                Command_PsychicRitual command_PsychicRitual = new Command_PsychicRitual
                {
                    defaultLabel = ritualDef2.LabelCap,
                    defaultDesc = ritualDef2.description + $"\n\n{ritualDef2.TimeAndOfferingLabel()}",
                    action = delegate
                    {
                        InitializePsychicRitual(ritualDef2, target);
                    },
                    icon = ritualDef2.uiIcon
                };
                AcceptanceReport acceptanceReport = Find.PsychicRitualManager.CanInvoke(ritualDef2, target.Map);
                if ((bool)acceptanceReport)
                {
                    command_PsychicRitual.Disabled = false;
                    command_PsychicRitual.disabledReason = null;
                }
                else
                {
                    command_PsychicRitual.Disable(acceptanceReport.Reason.CapitalizeFirst());
                }

                yield return command_PsychicRitual;
            }
        }

        public static Verse.Gizmo CancelGizmo(PsychicRitual psychicRitual)
        {
            return new Command_Action
            {
                defaultLabel = "CommandCancelPsychicRitual".Translate(psychicRitual.def.label).CapitalizeFirst(),
                defaultDesc = "CommandCancelPsychicRitualDesc".Translate(psychicRitual.def.label).CapitalizeFirst().EndWithPeriod(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CancelPsychicRitual_Gizmo"),
                action = delegate
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CommandCancelPsychicRitualConfirm".Translate(psychicRitual.def.label), delegate
                    {
                        psychicRitual.CancelPsychicRitual(TaggedString.Empty);
                    }));
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                },
                hotKey = KeyBindingDefOf.Misc6
            };
        }

        public static Verse.Gizmo LeaveGizmo(PsychicRitual psychicRitual, Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "CommandLeavePsychicRitual".Translate(psychicRitual.def.label).CapitalizeFirst(),
                defaultDesc = "CommandLeavePsychicRitualDesc".Translate(psychicRitual.def.label).CapitalizeFirst().EndWithPeriod(),
                action = delegate
                {
                    psychicRitual.LeavePsychicRitual(pawn, TaggedString.Empty);
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                },
                hotKey = KeyBindingDefOf.Misc5
            };
        }

        private static List<ExtractRitualDef_InvocationCircle> VisibleRituals()
        {
            tmpVisibleRituals.Clear();
            foreach (PsychicRitualDef allDef in DefDatabase<PsychicRitualDef>.AllDefs)
            {
                if (allDef.Visible && allDef is ExtractRitualDef_InvocationCircle item)
                {
                    tmpVisibleRituals.Add(item);
                }
            }

            return tmpVisibleRituals;
        }

        private static void InitializePsychicRitual(ExtractRitualDef_InvocationCircle psychicRitualDef, Thing target)
        {
            TargetInfo target2 = new TargetInfo(target);
            PsychicRitualRoleAssignments assignments = psychicRitualDef.BuildRoleAssignments(target2);
            PsychicRitualCandidatePool candidatePool = psychicRitualDef.FindCandidatePool();
            Map currentMap = Find.CurrentMap;
            psychicRitualDef.InitializeCast(currentMap);
            Find.WindowStack.Add(new Dialog_BeginPsychicRitual(psychicRitualDef, candidatePool, assignments, currentMap));
        }
    }
}