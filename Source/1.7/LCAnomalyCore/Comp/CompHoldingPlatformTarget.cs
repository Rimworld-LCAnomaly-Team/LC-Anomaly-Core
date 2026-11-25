using LCAnomalyCore.Buildings;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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

        /// <summary>房间收容模式绑定的收容单元引用。</summary>
        public Thing boundContainmentUnit;

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

        /// <summary>获取绑定的房间收容控制终端（若当前处于房间收容）。</summary>
        public Building_AbnormalityContainmentTerminal BoundTerminal => boundContainmentUnit as Building_AbnormalityContainmentTerminal;

        /// <summary>获取当前所在的统一收容单元。</summary>
        public IAbnormalityContainmentUnit HeldUnit => boundContainmentUnit as IAbnormalityContainmentUnit;

        /// <summary>指示当前是否正处于房间收容控制终端的有效收容中。</summary>
        public bool CurrentlyContainedInRoom =>
            boundContainmentUnit != null
            && !boundContainmentUnit.Destroyed
            && boundContainmentUnit.Spawned
            && parent.Spawned
            && !isEscaping;

        /// <summary>指示当前是否正处于有效收容中。</summary>
        public bool CurrentlyHeld => CurrentlyContainedInRoom;

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

                if (boundContainmentUnit != null && (boundContainmentUnit.Destroyed || !boundContainmentUnit.Spawned))
                {
                    boundContainmentUnit = null;
                }

                if (CurrentlyHeld)
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

        /// <summary>根据蛋上的 heldPawnKind 生成对应异常实体并继承生物特征与研究进度。</summary>
        internal Pawn CreateHeldPawnFromEgg()
        {
            if (Props.heldPawnKind == null)
            {
                return null;
            }

            PawnKindDef heldPawnKind = Props.heldPawnKind;
            Faction ofEntities = LCContainmentUtility.AbnormalityFaction;
            float? fixedBiologicalAge = 0f;
            Pawn pawn3 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(heldPawnKind, ofEntities, PawnGenerationContext.NonPlayer, null, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, fixedBiologicalAge));

            LC_CompEgg egg = parent.GetComp<LC_CompEgg>();
            if (egg != null)
            {
                if (egg.Props.shouldTransferBioSignature)
                {
                    AbnormalityUtility.TransferSignature(pawn3, egg.biosignature);

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

            return pawn3;
        }

        /// <summary>绑定并进入指定收容室终端的房间活体收容状态。</summary>
        public void Notify_ContainedInRoom(Building_AbnormalityContainmentTerminal terminal)
        {
            targetHolder = null;
            boundContainmentUnit = terminal;
            isEscaping = false;

            if (parent is Pawn pawn)
            {
                if (pawn.mindState != null)
                {
                    pawn.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                    pawn.mindState.enemyTarget = null;
                }

                PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn);
                AbnormalityUtility.Discover(pawn.def);
                pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.MadePrisoner);
                pawn.TryGetComp<CompAbnormality>()?.Notify_Holded();
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);

                if (ModsConfig.AnomalyActive)
                {
                    pawn.TryGetComp<CompActivity>()?.Notify_HeldOnPlatform();
                    if (terminal != null && terminal.Map != null)
                    {
                        Find.StudyManager.UpdateStudiableCache(terminal, terminal.Map);
                    }
                }

                if (ConceptDefOf.CapturingEntities != null)
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.CapturingEntities, KnowledgeAmount.Total);
                if (ConceptDefOf.ContainingEntities != null)
                    LessonAutoActivator.TeachOpportunity(ConceptDefOf.ContainingEntities, OpportunityType.Important);
            }
        }

        /// <summary>从房间收容状态解除绑定时调用。</summary>
        public void Notify_ReleasedFromContainment()
        {
            boundContainmentUnit = null;
            parent.TryGetComp<CompAbnormalityTool>()?.OnLeftPlatform();
        }

        /// <summary>执行 <c>Escape</c> 定义的操作。</summary>
        public void Escape()
        {
            if (parent.TryGetComp<CompAbnormalityTool>() != null)
            {
                (boundContainmentUnit as IAbnormalityContainmentUnit)?.EjectContents();
                return;
            }

            var entitiyBasePawn = parent as LC_EntityBasePawn;
            if (entitiyBasePawn == null)
                return;

            //重置收容单元状态
            if (boundContainmentUnit is IAbnormalityContainmentUnit unit)
            {
                unit.EjectContents();
            }

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
            if (StudiedAtHoldingPlatform && !CurrentlyHeld && CanBeCaptured)
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

            if (CurrentlyHeld)
            {
                if (targetHolder != null && targetHolder != boundContainmentUnit)
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

            if (DebugSettings.ShowDevGizmos && CurrentlyHeld)
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
            Scribe_References.Look(ref boundContainmentUnit, "boundContainmentUnit");
            Scribe_Values.Look(ref isEscaping, "isEscaping", defaultValue: false);
        }
    }
}
