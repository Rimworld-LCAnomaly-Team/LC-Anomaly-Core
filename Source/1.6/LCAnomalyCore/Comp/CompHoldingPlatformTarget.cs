using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse.AI.Group;
using Verse.AI;
using Verse;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Buildings;

namespace LCAnomalyCore.Comp
{
    [StaticConstructorOnStartup]
    public class CompAbnormalityHoldingPlatformTarget : ThingComp
    {
        private const int CheckInitiateEscapeIntervalTicks = 2500;

        private static readonly SimpleCurve JoinEscapeChanceFromEscapeIntervalCurve = new SimpleCurve
    {
        new CurvePoint(120f, 0.33f),
        new CurvePoint(60f, 0.5f),
        new CurvePoint(10f, 0.9f)
    };

        private static readonly CachedTexture CaptureIcon = new CachedTexture("UI/Commands/CaptureEntity");

        private static readonly CachedTexture TransferIcon = new CachedTexture("UI/Commands/TransferEntity");

        private static readonly Texture2D CancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        public Thing targetHolder;

        public bool isEscaping;

        public EntityContainmentMode containmentMode;

        public bool extractBioferrite;

        [Unsaved(false)]
        private CompStudiable compStudiable;

        [Unsaved(false)]
        private CompActivity compActivity;

        [Unsaved(false)]
        private bool didCheckForActivityComp;

        public CompProperties_HoldingPlatformTarget Props => (CompProperties_HoldingPlatformTarget)props;

        public CompStudiable CompStudiable => compStudiable ?? (compStudiable = parent.GetComp<CompStudiable>());

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

        public CompEntityHolder EntityHolder => targetHolder.TryGetComp<CompEntityHolder>();

        public bool StudiedAtHoldingPlatform
        {
            get
            {
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

                    if (pawn.IsMutant && pawn.mutant.Def.canBeCapturedToHoldingPlatform)
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

        public bool CanStudy
        {
            get
            {
                if (containmentMode == EntityContainmentMode.Study)
                {
                    return EverStudiable;
                }

                return false;
            }
        }

        private bool EverStudiable
        {
            get
            {
                if (parent.Destroyed)
                {
                    return false;
                }

                if (parent is Pawn)
                {
                    if (CompStudiable != null)
                    {
                        return CompStudiable.AnomalyKnowledge > 0f;
                    }

                    return false;
                }

                return true;
            }
        }

        public Building_AbnormalyHoldingPlatform HeldPlatform => parent.ParentHolder as Building_AbnormalyHoldingPlatform;

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

                if (pawn.IsMutant && !pawn.mutant.Def.canBeCapturedToHoldingPlatform)
                {
                    return false;
                }

                if (CompStudiable != null && Find.Anomaly.HighestLevelReached < CompStudiable.Props.minMonolithLevelForStudy && Find.Anomaly.GenerateMonolith)
                {
                    return false;
                }

                if (!pawn.Downed && !(pawn.ParentHolder is Pawn_CarryTracker))
                {
                    return CompActivity?.IsDormant ?? false;
                }

                return true;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (targetHolder != null && parent.Map != targetHolder.Map)
            {
                targetHolder = null;
            }
        }

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
                else if (targetHolder != null && (targetHolder.Destroyed || EntityHolder.HeldPawn != null))
                {
                    targetHolder = null;
                }

                if (isEscaping && pawn.mindState.enemyTarget == null)
                {
                    isEscaping = false;
                }
            }

            Building_HoldingPlatform heldPlatform = HeldPlatform;
            if (heldPlatform != null && heldPlatform.HasAttachedBioferriteHarvester)
            {
                extractBioferrite = false;
            }

            if (CanBeCaptured)
            {
                LessonAutoActivator.TeachOpportunity(ConceptDefOf.CapturingEntities, OpportunityType.Important);
            }
        }

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
                    Faction ofEntities = Faction.OfEntities;
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
                            CompBiosignatureOwner compBiosignatureOwner = parent.TryGetComp<CompBiosignatureOwner>();
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

                    //绑上平台
                    LC_CompEntity tmpComp = pawn3.TryGetComp<LC_CompEntity>();
                    if (tmpComp != null)
                        tmpComp.Notify_Holded();

                    parent.Destroy();
                }

                containmentMode = EntityContainmentMode.Study;
            }

            if (pawn != null && HeldPlatform != null)
            {
                pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.MadePrisoner);
                pawn.TryGetComp <CompActivity>()?.Notify_HeldOnPlatform();
                Find.StudyManager.UpdateStudiableCache(HeldPlatform, HeldPlatform.Map);
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.CapturingEntities, KnowledgeAmount.Total);
                LessonAutoActivator.TeachOpportunity(ConceptDefOf.ContainingEntities, OpportunityType.Important);
            }
        }

        public void Notify_ReleasedFromPlatform()
        {
            Find.StudyManager.UpdateStudiableCache(HeldPlatform, HeldPlatform.Map);
        }

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

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (StudiedAtHoldingPlatform && !CurrentlyHeldOnPlatform && CanBeCaptured)
            {
                if (targetHolder != null)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "CancelCapture".Translate(),
                        defaultDesc = "CancelCaptureDesc".Translate(parent).Resolve(),
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
                        defaultLabel = "CaptureEntity".Translate() + "...",
                        defaultDesc = "CaptureEntityDesc".Translate(parent).Resolve(),
                        icon = CaptureIcon.Texture,
                        action = delegate
                        {
                            StudyUtility.TargetHoldingPlatformForEntity(null, parent);
                        },
                        activateSound = SoundDefOf.Click,
                        Disabled = !StudyUtility.HoldingPlatformAvailableOnCurrentMap(),
                        disabledReason = "NoHoldingPlatformsAvailable".Translate()
                    };
                }
            }

            if (CurrentlyHeldOnPlatform)
            {
                if (targetHolder != null && targetHolder != HeldPlatform)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "CancelTransfer".Translate(),
                        defaultDesc = "CancelTransferDesc".Translate(),
                        icon = CancelTex,
                        action = delegate
                        {
                            targetHolder = null;
                        }
                    };
                }
                else
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "TransferEntity".Translate(parent) + "...",
                        defaultDesc = "TransferEntityDesc".Translate(parent).Resolve(),
                        icon = TransferIcon.Texture,
                        action = delegate
                        {
                            StudyUtility.TargetHoldingPlatformForEntity(null, parent, transferBetweenPlatforms: true, HeldPlatform);
                        },
                        activateSound = SoundDefOf.Click
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

        public override string CompInspectStringExtra()
        {
            string text = base.CompInspectStringExtra();
            if (CanBeCaptured && ContainmentUtility.ShowContainmentStats(parent))
            {
                float num = parent.GetStatValue(StatDefOf.MinimumContainmentStrength);
                if (Props.heldPawnKind != null)
                {
                    num = Props.heldPawnKind.race.GetStatValueAbstract(StatDefOf.MinimumContainmentStrength);
                }

                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text += "Capturable".Translate() + ". " + StatDefOf.MinimumContainmentStrength.LabelCap + ": " + num.ToString("F1");
            }

            return text;
        }

        public override void PostExposeData()
        {
            Scribe_References.Look(ref targetHolder, "targetHolder");
            Scribe_Values.Look(ref isEscaping, "isEscaping", defaultValue: false);
            Scribe_Values.Look(ref containmentMode, "containmentMode", EntityContainmentMode.MaintainOnly);
            Scribe_Values.Look(ref extractBioferrite, "extractBioferrite", defaultValue: false);
        }
    }
}
