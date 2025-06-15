using HarmonyLib;
using LCAnomalyCore.Comp;
using LCAnomalyCore.GameComponent;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 关于CompHoldingPlatformTarget的补丁（为了提供生物特征可传递的特性）
    /// </summary>
    [HarmonyPatch(typeof(CompHoldingPlatformTarget), nameof(CompHoldingPlatformTarget.Notify_HeldOnPlatform))]
    public class Patch_CompHoldingPlatformTarget_Notify_HeldOnPlatform
    {
        /// <summary>
        /// Prefix方法
        /// </summary>
        /// <param name="newOwner">新的所有者</param>
        /// <param name="__instance">原来的反射对象</param>
        /// <returns>false 终止原方法（会和同样的prefix产生可能的不兼容）</returns>
        private static bool Prefix(ThingOwner newOwner, CompHoldingPlatformTarget __instance)
        {
            //Log.Warning("Patch_CompHoldingPlatformTarget.Notify_HeldOnPlatform 注入成功");

            __instance.targetHolder = null;
            Pawn pawn = null;
            if (__instance.parent is Pawn pawn2)
            {
                pawn2.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn2);
                pawn = pawn2;
            }

            if (newOwner != null)
            {
                if (__instance.Props.heldPawnKind != null)
                {
                    Pawn pawn3 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(__instance.Props.heldPawnKind, Faction.OfEntities, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, 0f));
                    newOwner.TryAdd(pawn3);
                    pawn3.TryGetComp<CompHoldingPlatformTarget>()?.Notify_HeldOnPlatform(newOwner);
                    pawn = pawn3;

                    if (__instance.Props.heldPawnKind == PawnKindDefOf.Revenant)
                    {
                        CompBiosignatureOwner compBiosignatureOwner = __instance.parent.TryGetComp<CompBiosignatureOwner>();
                        if (compBiosignatureOwner != null)
                        {
                            pawn3.TryGetComp<CompRevenant>().biosignature = compBiosignatureOwner.biosignature;
                        }

                        if (pawn3.TryGetComp<CompStudiable>(out var comp))
                        {
                            comp.lastStudiedTick = Find.TickManager.TicksGame;
                        }
                    }
                    //TODO 测试代码 充满冗余，需要正式分函数处理
                    else
                    {
                        LC_CompEgg egg = __instance.parent.GetComp<LC_CompEgg>();
                        if (egg != null)
                        {
                            if (egg.Props.shouldTransferBioSignature)
                            {
                                CompBiosignatureOwner compBiosignatureOwner = __instance.parent.TryGetComp<CompBiosignatureOwner>();
                                if (compBiosignatureOwner != null)
                                {
                                    pawn3.TryGetComp<LC_CompEntity>().biosignature = compBiosignatureOwner.biosignature;
                                    //Log.Warning("Patch_CompHoldingPlatformTarget:::传递生物特征成功");
                                }

                                if (pawn3.TryGetComp<CompStudiable>(out var comp1))
                                {
                                    comp1.lastStudiedTick = Find.TickManager.TicksGame;
                                }
                            }

                            var component = Current.Game.GetComponent<GameComponent_LC>();
                            component.TryGetAnomalyStatusSaved(pawn3.def, out AnomalyStatusSaved saved);

                            var comp2 = pawn3.TryGetComp<LC_CompStudyUnlocks>();
                            comp2?.TransferStudyProgress(saved.StudyProgress);
                        }
                    }

                    //调用独有的绑上平台的回调方法
                    LC_CompEntity tmpComp = pawn3.TryGetComp<LC_CompEntity>();
                    if (tmpComp != null)
                        tmpComp.Notify_Holded();
                    else
                        Find.HiddenItemsManager.SetDiscovered(pawn3.def);

                    __instance.parent.Destroy();
                }

                __instance.containmentMode = EntityContainmentMode.Study;
            }

            if (pawn != null && __instance.HeldPlatform != null)
            {
                pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.MadePrisoner);
                pawn.TryGetComp<CompActivity>()?.Notify_HeldOnPlatform();
                Find.StudyManager.UpdateStudiableCache(__instance.HeldPlatform, __instance.HeldPlatform.Map);
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.CapturingEntities, KnowledgeAmount.Total);
                LessonAutoActivator.TeachOpportunity(ConceptDefOf.ContainingEntities, OpportunityType.Important);
            }

            return false;
        }
    }

    /// <summary>
    /// 关于CompHoldingPlatformTarget的补丁（为了提供出逃回调方法）
    /// </summary>
    [HarmonyPatch(typeof(CompHoldingPlatformTarget), nameof(CompHoldingPlatformTarget.Escape))]
    public class Patch_CompHoldingPlatformTarget_Escape
    {
        /// <summary>
        /// Prefix方法
        /// </summary>
        /// <param name="initiator">是否是初始化</param>
        /// <param name="__instance">原来的反射对象</param>
        /// <returns>视情况而定</returns>
        private static bool Prefix(bool initiator, CompHoldingPlatformTarget __instance)
        {
            //如果不是LC实体就执行原方法
            var entitiyBasePawn = __instance.parent as LC_EntityBasePawn;
            if (entitiyBasePawn == null)
            {
                return true;
            }
            //LC_CompEntity compEntity = __instance.parent.TryGetComp<LC_CompEntity>();
            //if (compEntity == null)
            //    return true;

            //从下面开始不会再执行原方法
            //-------------------------------------------------------------------------------------//

            //重置收容平台状态
            __instance.HeldPlatform?.EjectContents();

            //Pawn不存在则退出
            Pawn pawn = (Pawn)__instance.parent;
            if (pawn == null)
            {
                Log.Warning("pawn is null");
                return false;
            }

            //设置逃跑状态，弹信封，触发回调方法
            __instance.isEscaping = true;
            entitiyBasePawn.EntityComp.Notify_Escaped();

            //设置脱离后的第一个目标（感觉没必要，这是原方法的部分内容）
            //感觉是给幽魂用的
            if (__instance.Props.lookForTargetOnEscape && !pawn.Downed)
            {
                Pawn enemyTarget = (Pawn)AttackTargetFinder.BestAttackTarget(pawn, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, (Thing x) => x is Pawn && (int)x.def.race.intelligence >= 1, 0f, 9999f, default(IntVec3), float.MaxValue, canBashDoors: true, canTakeTargetsCloserThanEffectiveMinRange: true, canBashFences: true);
                pawn.mindState.enemyTarget = enemyTarget;
            }

            //TODO 我也不知道这是什么，还得去看看
            if (!initiator)
            {
                return false;
            }

            //不执行原方法
            return false;
        }
    }

    /// <summary>
    /// 关于CompHoldingPlatformTarget的补丁（为了提供异想体不可随机逃跑的特性）
    /// </summary>
    [HarmonyPatch(typeof(CompHoldingPlatformTarget), "CaptivityTick")]
    public class Patch_CompHoldingPlatformTarget_CaptivityTick()
    {
        /// <summary>
        /// Prefix方法
        /// </summary>
        /// <param name="pawn">异想体</param>
        /// <param name="__instance">原来的反射对象</param>
        /// <returns>false 终止原方法（会和同样的prefix产生可能的不兼容）</returns>
        private static bool Prefix(Pawn pawn, CompHoldingPlatformTarget __instance)
        {
            pawn.mindState.entityTicksInCaptivity++;
            if (__instance.targetHolder is Building_HoldingPlatform building_HoldingPlatform && building_HoldingPlatform != __instance.HeldPlatform && building_HoldingPlatform.Occupied)
            {
                __instance.targetHolder = null;
            }

            if (__instance.parent.IsHashIntervalTick(2500))
            {
                float num = ContainmentUtility.InitiateEscapeMtbDays(pawn);
                if (num >= 0f && Rand.MTBEventOccurs(num, 60000f, 2500f))
                {
                    LC_CompEntity compEntity = pawn.TryGetComp<LC_CompEntity>();
                    if (compEntity != null)
                    {
                        // Log.Warning("Patch：检测到一次实体随机出逃，因为是LC实体所以没有执行");
                        return false;
                    }
                    else
                    {
                        __instance.Escape(initiator: true);
                    }
                }
            }

            return false;
        }
    }
}