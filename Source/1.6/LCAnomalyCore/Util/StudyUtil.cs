using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Defs;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 研究工作工具类
    /// </summary>
    public static class StudyUtil
    {
        private static readonly HashSet<Pawn> tmpReservers = new HashSet<Pawn>();

        /// <summary>
        /// 播放工作质量特效
        /// </summary>
        /// <param name="studier">研究者</param>
        /// <param name="entity">被研究者</param>
        /// <param name="result">研究质量</param>
        public static void DoStudyResultEffect(Pawn studier, Pawn entity, LC_StudyResult result)
        {
            switch (result)
            {
                case LC_StudyResult.Good:
                    FleckMaker.Static(entity.PositionHeld, entity.MapHeld, Defs.FleckDefOf.WorkResult_Good);
                    //Log.Message($"工作：{studier.Name} 对异想体 {entity.def.label.Translate()} 工作成功，质量：良好。");
                    break;

                case LC_StudyResult.Normal:
                    FleckMaker.Static(entity.PositionHeld, entity.MapHeld, Defs.FleckDefOf.WorkResult_Normal);
                    //Log.Message($"工作：{studier.Name} 对异想体 {entity.def.label.Translate()} 工作成功，质量：普通。");
                    break;

                case LC_StudyResult.Bad:
                    FleckMaker.Static(entity.PositionHeld, entity.MapHeld, Defs.FleckDefOf.WorkResult_Bad);
                    //Log.Message($"工作：{studier.Name} 对异想体 {entity.def.label.Translate()} 工作失败，质量：差。");
                    break;

                default:
                    //Log.Error("工作：检测到错误的输入。");
                    break;
            }
        }

        /// <summary>
        /// 根据数值计算员工属性等级
        /// </summary>
        /// <returns></returns>
        public static EPawnLevel CalculatePawnLevel(float points)
        {
            if (points > 100)
                return EPawnLevel.EX;
            else if (points >= 85)
                return EPawnLevel.V;
            else if (points >= 65)
                return EPawnLevel.IV;
            else if (points >= 45)
                return EPawnLevel.III;
            else if (points >= 30)
                return EPawnLevel.II;
            else
                return EPawnLevel.I;
        }

        /// <summary>
        /// 计算处理脑叶员工数值变化量
        /// </summary>
        public static float GetPawnStatusIncreaseValue(CompPawnStatus studier, EAnomalyWorkType workType, string abnormalLevel)
        {
            EPawnLevel pawnLevel;
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Fortitude);
                    break;

                case EAnomalyWorkType.Attachment:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Temperance);
                    break;

                case EAnomalyWorkType.Insight:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Prudence);
                    break;

                case EAnomalyWorkType.Repression:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Justice);
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Unknown EPawnStatus");
            }

            return get_value(pawnLevel, abnormalLevel);

            //根据员工技能评级和异常等级来获取增加值
            static float get_value(EPawnLevel pawnLevel, string abnormalLevel)
            {
                //算法表格详见 https://lobotomycorp.fandom.com/zh/wiki/%E8%81%8C%E5%91%98
                if (abnormalLevel == "ZAYIN")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.44f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.3f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.18f;
                    else
                        return 0.08f;
                }
                else if (abnormalLevel == "TETH")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.55f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.4f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.27f;
                    else
                        return 0.16f;
                }
                else if (abnormalLevel == "HE")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.72f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.55f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.5f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.36f;
                    else
                        return 0.24f;
                }
                else if (abnormalLevel == "WAW")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.84f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.66f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.5f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.45f;
                    else
                        return 0.32f;
                }
                else
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.77f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.45f;
                    else
                        return 0.4f;
                }
            }
        }

        public static void TargetHoldingPlatformForEntity(Pawn carrier, Thing abnormality, bool transferBetweenPlatforms = false, Thing sourcePlatform = null)
        {
            //TODO 后续可能要重构 如果带LC Comp或者是蛋def都可以认为是实体
            bool isLCEntity = abnormality.TryGetComp<CompAbnormality>() != null || abnormality.def is ThingDef_AnomalyEgg;

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
                        if (item is Building_AbnormalityHoldingPlatform holdingPlatform && abnormality != holdingPlatform.HeldPawn)
                        {
                            var compHoldingPlatformTarget = holdingPlatform.HeldPawn?.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
                            if (compHoldingPlatformTarget != null && compHoldingPlatformTarget.targetHolder == t.Thing)
                            {
                                Messages.Message("MessageHolderReserved".Translate(t.Thing.Label), MessageTypeDefOf.RejectInput);
                                return;
                            }
                        }
                    }

                    var compHoldingPlatformTarget2 = abnormality.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
                    if (compHoldingPlatformTarget2 != null)
                    {
                        compHoldingPlatformTarget2.targetHolder = t.Thing;
                    }

                    if (carrier != null)
                    {
                        Job job = (transferBetweenPlatforms ? JobMaker.MakeJob(RimWorld.JobDefOf.TransferBetweenEntityHolders, sourcePlatform, t, abnormality) : JobMaker.MakeJob(RimWorld.JobDefOf.CarryToEntityHolder, t, abnormality));
                        job.count = 1;
                        carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }

                    if (t.Thing != null && !t.Thing.SafelyContains(abnormality))
                    {
                        Messages.Message("MessageTargetBelowMinimumContainmentStrength".Translate(t.Thing.Label, abnormality.Label), MessageTypeDefOf.ThreatSmall);
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
                CompAbnormalityHolder compAbnormalityHolder = t.Thing?.TryGetComp<CompAbnormalityHolder>();
                if (compAbnormalityHolder == null)
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

                        if (StudyUtil.AlreadyReserved(p, out reserver))
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
                        label = "FloatMenuContainmentStrength".Translate() + ": " + StatDefOf.ContainmentStrength.Worker.ValueToString(compAbnormalityHolder.ContainmentStrength, finalized: false);
                        label += "\n" + ("FloatMenuContainmentRequires".Translate(abnormality).CapitalizeFirst() + ": " + StatDefOf.MinimumContainmentStrength.Worker.ValueToString(abnormality.GetStatValue(StatDefOf.MinimumContainmentStrength), finalized: false)).Colorize(t.Thing.SafelyContains(abnormality) ? Color.white : Color.red);
                    }

                    Widgets.MouseAttachedLabel(label);
                }
            }, delegate
            {
                foreach (Building item2 in abnormality.MapHeld.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
                {
                    if (ValidateTarget(item2) && (carrier == null || CanReserveForTransfer(item2)))
                    {
                        GenDraw.DrawArrowPointingAt(item2.DrawPos);
                    }
                }
            });
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
                if (t.HasThing && t.Thing.TryGetComp(out CompAbnormalityHolder comp) && comp.HeldPawn == null)
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

        public static bool AlreadyReserved(Thing p, out Pawn reserver)
        {
            Log.Warning("1");
            tmpReservers.Clear();
            Log.Warning("2");
            p.Map.reservationManager.ReserversOf(p, tmpReservers);
            Log.Warning("3");
            reserver = tmpReservers.FirstOrDefault();
            Log.Warning("4");
            if (reserver != null)
            {
                Log.Warning("5");
                return true;
            }
            Log.Warning("6");
            foreach (Thing item in p.Map.listerThings.AllThings.Where(m => m.HasComp<CompAbnormalityHoldingPlatformTarget>()))
            {
                Log.Warning("7");
                Log.Error($"item.TryGetComp<CompAbnormalityHoldingPlatformTarget>().targetHolder == null {item.TryGetComp<CompAbnormalityHoldingPlatformTarget>().targetHolder == null}");
                if (item.TryGetComp<CompAbnormalityHoldingPlatformTarget>().targetHolder == p)
                {
                    Log.Warning("8");
                    reserver = item as Pawn;
                    return true;
                }
            }
            Log.Warning("9");
            return false;
        }

        public static bool HoldingPlatformAvailableOnCurrentMap()
        {
            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                return false;
            }

            Log.Warning((currentMap.listerBuildings.allBuildingsColonist.Count).ToString());
            foreach (Building item in currentMap.listerBuildings.allBuildingsColonist)
            {
                if (item.TryGetComp<CompAbnormalityHolder>(out var comp) && comp.Available && !AlreadyReserved(item, out var _))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 研究质量枚举
    /// </summary>
    public enum LC_StudyResult
    {
        /// <summary>
        /// 良好
        /// </summary>
        Good,

        /// <summary>
        /// 一般
        /// </summary>
        Normal,

        /// <summary>
        /// 差
        /// </summary>
        Bad
    }
}