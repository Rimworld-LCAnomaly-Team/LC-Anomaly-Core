using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse.AI.Group;
using Verse.AI;
using Verse;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Util;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompAbnormalityHoldingPlatformTarget</c> 类型。</summary>
    [StaticConstructorOnStartup]
    public class CompAbnormalityHoldingPlatformTarget : ThingComp
    {
        private static readonly CachedTexture CaptureIcon = new CachedTexture("UI/Commands/Assignment/HoldingPlatform");

        private static readonly Texture2D CancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        /// <summary>表示 <c>targetHolder</c>。</summary>
        public Thing targetHolder;

        /// <summary>表示 <c>isEscaping</c>。</summary>
        public bool isEscaping;

        [Unsaved(false)]
        private CompAbnormalityStudiable compAbnormalityStudiable;

        [Unsaved(false)]
        private CompActivity compActivity;

        [Unsaved(false)]
        private bool didCheckForActivityComp;

        /// <summary>获取 <c>Props</c>。</summary>
        public CompProperties_AbnormalityHoldingPlatformTarget Props => (CompProperties_AbnormalityHoldingPlatformTarget)props;

        /// <summary>获取 <c>CompAbnormalityStudiable</c>。</summary>
        public CompAbnormalityStudiable CompAbnormalityStudiable => compAbnormalityStudiable ?? (compAbnormalityStudiable = parent.GetComp<CompAbnormalityStudiable>());

        /// <summary>表示 <c>CompActivity</c>。</summary>
        public CompActivity CompActivity
        {
            get
            {
                if (didCheckForActivityComp)
                {
                    return compActivity;
                }

                if (compActivity == null)
                {
                    compActivity = (parent as Pawn)?.activity ?? parent.GetComp<CompActivity>();
                }

                didCheckForActivityComp = true;
                return compActivity;
            }
        }

        /// <summary>获取 <c>EntityHolder</c>。</summary>
        public CompAbnormalityHolder EntityHolder => targetHolder?.TryGetComp<CompAbnormalityHolder>();

        /// <summary>表示 <c>StudiedAtHoldingPlatform</c>。</summary>
        public bool StudiedAtHoldingPlatform
        {
            get
            {
                if (parent.TryGetComp<CompAbnormality>() != null)
                {
                    return true;
                }

                if (!ModsConfig.AnomalyActive)
                {
                    return false;
                }

                if (!EverStudiable)
                {
                    return false;
                }

                if (parent is Pawn pawn)
                {
                    if (pawn.IsAnimal && !pawn.RaceProps.IsAnomalyEntity)
                    {
                        return false;
                    }

                    if (pawn.IsMutant && ModLister.AnomalyInstalled && pawn.mutant.Def.canBeCapturedToHoldingPlatform)
                    {
                        return true;
                    }

                    if (!pawn.RaceProps.Humanlike && !pawn.Inhumanized())
                    {
                        return !pawn.kindDef.studiableAsPrisoner;
                    }

                    return false;
                }

                return true;
            }
        }

        /// <summary>获取 <c>CanStudy</c>。</summary>
        public bool CanStudy => true;

        private bool EverStudiable
        {
            get
            {
                if (parent.Destroyed)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>获取 <c>HeldPlatform</c>。</summary>
        public Building_AbnormalityHoldingPlatform HeldPlatform => parent.ParentHolder as Building_AbnormalityHoldingPlatform;

        /// <summary>表示 <c>CurrentlyHeldOnPlatform</c>。</summary>
        public bool CurrentlyHeldOnPlatform
        {
            get
            {
                if (HeldPlatform != null)
                {
                    return parent.SpawnedOrAnyParentSpawned;
                }

                return false;
            }
        }

        /// <summary>表示 <c>CanBeCaptured</c>。</summary>
        public bool CanBeCaptured
        {
            get
            {
                if (!(parent is Pawn pawn))
                {
                    return true;
                }

                if (pawn.Faction == Faction.OfPlayer)
                {
                    return false;
                }

                bool isLCAbnormality = parent.TryGetComp<CompAbnormality>() != null;
                if (!isLCAbnormality && pawn.IsMutant && ModsConfig.AnomalyActive && !pawn.mutant.Def.canBeCapturedToHoldingPlatform)
                {
                    return false;
                }

                if (!pawn.Downed && !(pawn.ParentHolder is Pawn_CarryTracker))
                {
                    return !isLCAbnormality && ModsConfig.AnomalyActive && (CompActivity?.IsDormant ?? false);
                }

                return true;
            }
        }

        /// <inheritdoc />
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (targetHolder != null && parent.Map != targetHolder.Map)
            {
                targetHolder = null;
            }
        }

        /// <inheritdoc />
        public override void CompTick()
        {
            base.CompTick();
            if (parent is Pawn pawn)
            {
                if (!pawn.SpawnedOrAnyParentSpawned)
                {
                    return;
                }

                if (CurrentlyHeldOnPlatform)
                {
                    //CaptivityTick(pawn);
                }
                else if (targetHolder != null && (targetHolder.Destroyed || EntityHolder == null || EntityHolder.HeldPawn != null))
                {
                    targetHolder = null;
                }

                if (isEscaping && pawn.mindState.enemyTarget == null)
                {
                    isEscaping = false;
                }
            }

            if (CanBeCaptured)
            {
                if (ConceptDefOf.CapturingEntities != null)
                    LessonAutoActivator.TeachOpportunity(ConceptDefOf.CapturingEntities, OpportunityType.Important);
            }
        }

        /// <summary>执行 <c>Notify_HeldOnPlatform</c> 定义的操作。</summary>
        public void Notify_HeldOnPlatform(ThingOwner newOwner)
        {
            targetHolder = null;
            Pawn pawn = null;
            if (parent is Pawn pawn2)
            {
                pawn2.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn2);
                pawn = pawn2;
            }

            if (newOwner != null)
            {
                if (Props.heldPawnKind != null)
                {
                    PawnKindDef heldPawnKind = Props.heldPawnKind;
                    Faction ofEntities = LCContainmentUtility.AbnormalityFaction;
                    float? fixedBiologicalAge = 0f;
                    Pawn pawn3 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(heldPawnKind, ofEntities, PawnGenerationContext.NonPlayer, null, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, fixedBiologicalAge));
                    newOwner.TryAdd(pawn3);
                    pawn3.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.Notify_HeldOnPlatform(newOwner);
                    pawn = pawn3;

                    //传输蛋对象的生物特征和研究进度
                    LC_CompEgg egg = parent.GetComp<LC_CompEgg>();
                    if (egg != null)
                    {
                        if (egg.Props.shouldTransferBioSignature)
                        {
                            var sourceCompAbnormality = parent.TryGetComp<CompAbnormality>();
                            if (sourceCompAbnormality != null)
                            {
                                pawn3.TryGetComp<CompAbnormality>().biosignature = sourceCompAbnormality.biosignature;
                            }

                            if (pawn3.TryGetComp<CompAbnormalityStudiable>(out var comp1))
                            {
                                comp1.lastStudiedTick = Find.TickManager.TicksGame;
                            }
                        }

                        var component = Components.LC;
                        var comp2 = pawn3.TryGetComp<CompAbnormalityStudyUnlocks>();
                        if (component != null)
                        {
                            component.TryGetAnomalyStatusSaved(pawn3.def, out AnomalyStatusSaved saved);
                            comp2?.TransferStudyProgress(saved.StudyProgress);
                        }
                    }

                    //绑上平台
                    CompAbnormality tmpComp = pawn3.TryGetComp<CompAbnormality>();
                    if (tmpComp != null)
                        tmpComp.Notify_Holded();

                    parent.Destroy();
                }
            }

            if (pawn != null && HeldPlatform != null)
            {
                pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.MadePrisoner);
                if (ModsConfig.AnomalyActive)
                    pawn.TryGetComp<CompActivity>()?.Notify_HeldOnPlatform();
                if (ModsConfig.AnomalyActive)
                    Find.StudyManager.UpdateStudiableCache(HeldPlatform, HeldPlatform.Map);
                if (ConceptDefOf.CapturingEntities != null)
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.CapturingEntities, KnowledgeAmount.Total);
                if (ConceptDefOf.ContainingEntities != null)
                    LessonAutoActivator.TeachOpportunity(ConceptDefOf.ContainingEntities, OpportunityType.Important);
            }
        }

        /// <summary>执行 <c>Notify_ReleasedFromPlatform</c> 定义的操作。</summary>
        public void Notify_ReleasedFromPlatform()
        {
            if (ModsConfig.AnomalyActive)
                Find.StudyManager.UpdateStudiableCache(HeldPlatform, HeldPlatform.Map);
        }

        /// <summary>执行 <c>Escape</c> 定义的操作。</summary>
        public void Escape()
        {
            var entitiyBasePawn = parent as LC_EntityBasePawn;
            if (entitiyBasePawn == null)
                return;

            //重置收容平台状态
            HeldPlatform?.EjectContents();

            //Pawn不存在则退出
            Pawn pawn = (Pawn)parent;
            if (pawn == null)
            {
                Log.Warning("pawn is null");
                return;
            }

            //设置逃跑状态，弹信封，触发回调方法
            isEscaping = true;
            entitiyBasePawn.EntityComp.Notify_Escaped();

            //设置脱离后的第一个目标（感觉没必要，这是原方法的部分内容）
            //感觉是给幽魂用的
            if (Props.lookForTargetOnEscape && !pawn.Downed)
            {
                Pawn enemyTarget = (Pawn)AttackTargetFinder.BestAttackTarget(pawn, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, (Thing x) => x is Pawn && (int)x.def.race.intelligence >= 1, 0f, 9999f, default(IntVec3), float.MaxValue, canBashDoors: true, canTakeTargetsCloserThanEffectiveMinRange: true, canBashFences: true);
                pawn.mindState.enemyTarget = enemyTarget;
            }
        }

        /// <inheritdoc />
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (StudiedAtHoldingPlatform && !CurrentlyHeldOnPlatform && CanBeCaptured)
            {
                if (targetHolder != null)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "LC_CancelCapture".Translate(),
                        defaultDesc = "LC_CancelCaptureDesc".Translate(parent).Resolve(),
                        icon = CancelTex,
                        action = delegate
                        {
                            targetHolder = null;
                        }
                    };
                }
                else if (parent.Spawned)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "LC_CaptureEntity".Translate() + "...",
                        defaultDesc = "LC_CaptureEntityDesc".Translate(parent).Resolve(),
                        icon = CaptureIcon.Texture,
                        action = delegate
                        {
                            StudyUtil.TargetHoldingPlatformForEntity(null, parent);
                        },
                        activateSound = SoundDefOf.Click,
                        Disabled = !StudyUtil.HoldingPlatformAvailableOnCurrentMap(),
                        disabledReason = "LC_NoHoldingPlatformsAvailable".Translate()
                    };
                }
            }

            if (CurrentlyHeldOnPlatform)
            {
                if (targetHolder != null && targetHolder != HeldPlatform)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "LC_CancelTransfer".Translate(),
                        defaultDesc = "LC_CancelTransferDesc".Translate(),
                        icon = CancelTex,
                        action = delegate
                        {
                            targetHolder = null;
                        }
                    };
                }
            }

            if (DebugSettings.ShowDevGizmos && CurrentlyHeldOnPlatform)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Escape",
                    action = delegate
                    {
                        Escape();
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Kill",
                    action = delegate
                    {
                        parent.Kill();
                    }
                };
            }
        }

        /// <inheritdoc />
        public override string CompInspectStringExtra()
        {
            string text = base.CompInspectStringExtra();
            if (CanBeCaptured && parent.TryGetComp<CompAbnormalityHoldingPlatformTarget>(out var targetComp) && targetComp.Props.heldPawnKind != null)
            {
                float num = LCContainmentUtility.GetMinimumContainmentStrength(parent);
                if (Props.heldPawnKind != null)
                {
                    num = LCContainmentUtility.GetMinimumContainmentStrength(Props.heldPawnKind.race);
                }

                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text += "LC_Capturable".Translate() + ". " + Defs.StatDefOf.LC_MinimumContainmentStrength.LabelCap + ": " + num.ToString("F1");
            }

            return text;
        }

        /// <inheritdoc />
        public override void PostExposeData()
        {
            Scribe_References.Look(ref targetHolder, "targetHolder");
            Scribe_Values.Look(ref isEscaping, "isEscaping", defaultValue: false);
        }
    }
}
