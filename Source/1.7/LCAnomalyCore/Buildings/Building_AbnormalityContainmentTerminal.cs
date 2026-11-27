using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Interface;
using LCAnomalyCore.UI;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAnomalyCore.Buildings
{
    /// <summary>
    /// 房间式收容控制终端：绑定并管理所在封闭房间内活体收容的异想体，负责工作分配、HUD、研究结算与房间完整性监控。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_AbnormalityContainmentTerminal : Building, IAbnormalityContainmentUnit, IRoofCollapseAlert
    {
        /// <summary>收容室熔毁由工作及时解除时触发。</summary>
        public static event Action<Building_AbnormalityContainmentTerminal> OnAnyMeltdownResolved;

        /// <summary>收容室熔毁倒计时归零突破时触发（参数：终端、损失 PE-Box 数）。</summary>
        public static event Action<Building_AbnormalityContainmentTerminal, int> OnAnyMeltdownBreached;

        private static readonly Color TerminalBadgeColor = new Color(0.95f, 0.92f, 0.55f);

        private static readonly Texture2D TexRoomHeaderFrame = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RoomHeaderFrame", false);
        private static readonly Texture2D TexRoomHeaderCompactFrame = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RoomHeaderCompactFrame", false);
        private static readonly Texture2D TexMeltdownBannerFrame = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/MeltdownBannerFrame", false);
        private static readonly Texture2D TexRiskZayin = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RiskBadge_ZAYIN", false);
        private static readonly Texture2D TexRiskTeth = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RiskBadge_TETH", false);
        private static readonly Texture2D TexRiskHe = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RiskBadge_HE", false);
        private static readonly Texture2D TexRiskWaw = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RiskBadge_WAW", false);
        private static readonly Texture2D TexRiskAleph = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RiskBadge_ALEPH", false);
        private static readonly Texture2D TexRiskUnknown = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/RiskBadge_Unknown", false);
        private static readonly Texture2D TexQliphothFrame = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/QliphothFrame", false);
        private static readonly Texture2D TexQliphothAlertFrame = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/QliphothAlertFrame", false);
        private static readonly Texture2D TexPeBoxIcon = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/PeBoxIcon", false);
        private static readonly Texture2D TexEnergyBarFrame = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/EnergyBarFrame", false);
        private static readonly Texture2D TexEnergyCubePE = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/EnergyCube_PE", false);
        private static readonly Texture2D TexEnergyCubeNE = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/EnergyCube_NE", false);
        private static readonly Texture2D TexEnergyCubeEmpty = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/EnergyCube_Empty", false);
        private static readonly Texture2D TexResultGood = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/ResultBadge_Good", false);
        private static readonly Texture2D TexResultNormal = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/ResultBadge_Normal", false);
        private static readonly Texture2D TexResultBad = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/ResultBadge_Bad", false);
        private static readonly Texture2D TexWorkInstinct = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Instinct", false);
        private static readonly Texture2D TexWorkInsight = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Insight", false);
        private static readonly Texture2D TexWorkAttachment = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Attachment", false);
        private static readonly Texture2D TexWorkRepression = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Repression", false);

        private Pawn boundPawn;
        private int breachGraceTicks;
        protected EAnomalyWorkType curWorkType = EAnomalyWorkType.Instinct;
        protected StringBuilder inspectTextSB = new StringBuilder();

        private CompWorkableUI compWorkable;
        private CompAssignableToPawn_LC_Entity compAssignable;
        private CompAbnormality cachedEntity;
        private CompAbnormalityTool cachedTool;
        private CompAbnormalityStudiable compAbnormalityStudiable;
        private Sustainer workingSustainer;

        private int activeSessionPeCount;
        private int activeSessionNeCount;
        private int activeSessionMaxBoxes;
        private bool isStudySessionActive;
        private int lastStudyActivityTick = -999999;
        private LC_StudyResult? lastStudyResult;
        private int resultDisplayUntilTick = -999999;
        private Pawn activeStudier;
        private int nextNarrationTick = -999999;
        private int narrationStep;
        private int narrationSeed;
        private bool meltdownActive;
        private int meltdownTicksRemaining;
        private int meltdownTicksTotal;

        private int cachedRoomAnchorId = int.MinValue;
        private int cachedRoomAnchorTick = -999999;
        private Vector3 cachedRoomTopAnchor;
        private Vector3 cachedRoomLeftAnchor;
        private float cachedRoomWidthCells = 5f;
        private float cachedRoomHeightCells = 5f;

        /// <inheritdoc />
        public Thing UnitThing => this;

        /// <inheritdoc />
        public CompAbnormalityHolder HolderComp => GetComp<CompAbnormalityHolder>();

        /// <inheritdoc />
        public Pawn HeldPawn => boundPawn != null && !boundPawn.Destroyed && !boundPawn.Dead ? boundPawn : null;

        /// <inheritdoc />
        public bool Occupied => HeldPawn != null;

        /// <inheritdoc />
        public bool IsRoomContainment => true;

        /// <inheritdoc />
        public CompAbnormalityTool HeldToolComp => cachedTool ?? HeldPawn?.GetComp<CompAbnormalityTool>();

        /// <inheritdoc />
        public bool IsHoldingTool => HeldToolComp != null;

        /// <inheritdoc />
        public CompAbnormalityStudiable CompAbnormalityStudiable
        {
            get
            {
                if (HeldPawn != null)
                {
                    compAbnormalityStudiable ??= HeldPawn.GetComp<CompAbnormalityStudiable>();
                }
                return compAbnormalityStudiable;
            }
        }

        /// <summary>可工作 UI 组件。</summary>
        protected CompWorkableUI CompWorkable => compWorkable ??= GetComp<CompWorkableUI>();

        /// <inheritdoc />
        public CompAssignableToPawn_LC_Entity CompAssignable => compAssignable ??= GetComp<CompAssignableToPawn_LC_Entity>();

        /// <inheritdoc />
        public Room ContainmentRoom => Spawned ? this.GetRoom() : null;

        /// <inheritdoc />
        public EAnomalyWorkType CurWorkType
        {
            get => curWorkType;
            set => curWorkType = value;
        }

        /// <summary>当前缓存的异常核心组件是否存在。</summary>
        protected bool EntityCached => cachedEntity != null;

        /// <summary>当前逆卡巴拉计数器值。</summary>
        public int QliphothCounter => EntityCached ? cachedEntity.QliphothCountCurrent : 0;

        /// <summary>当前逆卡巴拉计数器上限。</summary>
        public int QliphothCounterMax => EntityCached ? cachedEntity.Props.qliphothCountMax : 0;

        /// <summary>当前工作会话已产出的 PE-Box 格数。</summary>
        public int ActiveSessionPeCount => activeSessionPeCount;

        /// <summary>当前工作会话已产出的 NE-Box 格数。</summary>
        public int ActiveSessionNeCount => activeSessionNeCount;

        /// <summary>当前工作会话最大 PE/NE-Box 格数上限。</summary>
        public int ActiveSessionMaxBoxes => activeSessionMaxBoxes;

        /// <summary>当前是否处于工作进行中。</summary>
        public bool IsStudySessionActive => isStudySessionActive;

        /// <summary>当前工作会话中的研究员工（若仍在工作）。</summary>
        public Pawn ActiveStudier => isStudySessionActive && activeStudier != null && !activeStudier.Destroyed ? activeStudier : null;

        /// <summary>当前收容室所收容实体的 CompAbnormality。</summary>
        public CompAbnormality HeldAbnormality => cachedEntity ?? HeldPawn?.GetComp<CompAbnormality>();

        /// <summary>最近一次完成的工作评级结果。</summary>
        public LC_StudyResult? LastStudyResult => lastStudyResult;

        /// <summary>当前收容室是否正处于逆卡巴拉熔毁倒计时状态。</summary>
        public bool IsInMeltdown => meltdownActive && Occupied && !IsHoldingTool;

        /// <summary>逆卡巴拉熔毁剩余 Tick 数。</summary>
        public int MeltdownTicksRemaining => IsInMeltdown ? Mathf.Max(0, meltdownTicksRemaining) : 0;

        /// <summary>逆卡巴拉熔毁剩余秒数。</summary>
        public float MeltdownSecondsRemaining => MeltdownTicksRemaining / 60f;

        /// <summary>逆卡巴拉熔毁剩余时间比例（0~1）。</summary>
        public float MeltdownRemainingRatio => (IsInMeltdown && meltdownTicksTotal > 0)
            ? Mathf.Clamp01((float)meltdownTicksRemaining / meltdownTicksTotal)
            : 0f;

        /// <summary>当前独立 PE-Box 数量。</summary>
        public int PeBoxCounter
        {
            get
            {
                if (EntityCached && Util.Components.LC != null)
                {
                    Util.Components.LC.TryGetAnomalyStatusSaved(cachedEntity.parent.def, out var saved);
                    return saved.IndiPeBoxAmount;
                }
                return 0;
            }
        }

        /// <inheritdoc />
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RefreshCachedEntity();
        }

        /// <inheritdoc />
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            CompAbnormalityTool tool = HeldToolComp;
            if (tool != null)
            {
                Map prevMap = Map;
                IntVec3 prevPos = Position;
                tool.OnLeftPlatform();
                if (mode != DestroyMode.Vanish && prevMap != null)
                {
                    EjectContents();
                    tool.RevertToEgg(prevMap, prevPos);
                }
            }
            else if (mode != DestroyMode.Vanish && boundPawn != null)
            {
                EjectContents();
            }

            StopWorkingSustainer();
            base.DeSpawn(mode);
        }

        /// <inheritdoc />
        protected override void Tick()
        {
            base.Tick();

            if (isStudySessionActive && Find.TickManager != null && Find.TickManager.TicksGame - lastStudyActivityTick > 120)
            {
                isStudySessionActive = false;
                activeStudier = null;
                nextNarrationTick = -999999;
                StopWorkingSustainer();
                NotifyBorderingDoorsWorkLockChanged(lockNow: false);
            }

            if (boundPawn == null)
            {
                if (meltdownActive)
                {
                    ClearMeltdown(resolvedByWork: false);
                }
                return;
            }

            if (boundPawn.Destroyed || boundPawn.Dead || !boundPawn.Spawned)
            {
                ClearMeltdown(resolvedByWork: false);
                boundPawn = null;
                breachGraceTicks = 0;
                OnContentChanged();
                return;
            }

            if (meltdownActive)
            {
                if (IsHoldingTool)
                {
                    ClearMeltdown(resolvedByWork: false);
                }
                else if (isStudySessionActive)
                {
                    ClearMeltdown(resolvedByWork: true);
                }
                else
                {
                    meltdownTicksRemaining--;
                    if (meltdownTicksRemaining <= 0)
                    {
                        FailMeltdown();
                        if (boundPawn == null)
                        {
                            return;
                        }
                    }
                }
            }

            if (this.IsHashIntervalTick(60))
            {
                bool roomOk = IsRoomValidForContainment(out _);
                bool pawnInside = boundPawn.GetRoom() == ContainmentRoom;
                if (!roomOk || !pawnInside)
                {
                    breachGraceTicks += 60;
                    int graceLimit = (HolderComp as CompAbnormalityHolderRoom)?.Props.breachGraceTicks ?? 120;
                    if (breachGraceTicks >= graceLimit)
                    {
                        breachGraceTicks = 0;
                        boundPawn.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.Escape();
                    }
                }
                else
                {
                    breachGraceTicks = 0;
                }
            }
        }

        /// <summary>校验当前终端所在房间是否满足封闭室内且唯一终端的收容条件。</summary>
        public bool IsRoomValidForContainment(out string failReason)
        {
            if (!Spawned || Map == null)
            {
                failReason = "LC_ContainmentRoom_NotEnclosed".Translate();
                return false;
            }

            Room room = this.GetRoom();
            if (room == null || room.TouchesMapEdge || room.PsychologicallyOutdoors)
            {
                failReason = "LC_ContainmentRoom_NotEnclosed".Translate();
                return false;
            }

            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Building_AbnormalityContainmentTerminal other && other != this)
                {
                    failReason = "LC_ContainmentRoom_DuplicateTerminal".Translate();
                    return false;
                }
            }

            failReason = null;
            return true;
        }

        /// <summary>获取房间内用于生成或定点驻留异想体的安全站位格（优先房间几何中心）。</summary>
        public IntVec3 GetContainmentSpawnCell()
        {
            if (!Spawned || Map == null)
            {
                return Position;
            }

            Room room = this.GetRoom();
            if (room == null || room.TouchesMapEdge)
            {
                return Position;
            }

            float sumX = 0f;
            float sumZ = 0f;
            int count = 0;
            foreach (IntVec3 c in room.Cells)
            {
                sumX += c.x;
                sumZ += c.z;
                count++;
            }

            Vector3 center = count > 0 ? new Vector3(sumX / count, 0f, sumZ / count) : Position.ToVector3Shifted();
            IntVec3 best = IntVec3.Invalid;
            float bestDistSq = float.MaxValue;

            foreach (IntVec3 c in room.Cells)
            {
                if (!c.InBounds(Map) || c == Position || !c.Standable(Map) || c.GetEdifice(Map) != null || c.GetDoor(Map) != null)
                {
                    continue;
                }

                float dx = c.x - center.x;
                float dz = c.z - center.z;
                float dSq = dx * dx + dz * dz;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = c;
                }
            }

            if (best.IsValid)
            {
                return best;
            }

            foreach (IntVec3 c in room.Cells)
            {
                if (c.InBounds(Map) && c.Standable(Map) && c.GetDoor(Map) == null)
                {
                    return c;
                }
            }

            return Position;
        }

        /// <inheritdoc />
        public bool CanAcceptCapture(Thing candidate, out string failReason)
        {
            if (Occupied)
            {
                failReason = "LC_MessageHolderReserved".Translate(LabelCap);
                return false;
            }

            return IsRoomValidForContainment(out failReason);
        }

        /// <inheritdoc />
        public bool TryCaptureThing(Thing takee)
        {
            if (takee == null || !CanAcceptCapture(takee, out _))
            {
                return false;
            }

            IntVec3 spawnCell = GetContainmentSpawnCell();
            CompAbnormalityHoldingPlatformTarget targetComp = takee.TryGetComp<CompAbnormalityHoldingPlatformTarget>();

            if (targetComp != null && targetComp.Props.heldPawnKind != null)
            {
                Pawn newPawn = targetComp.CreateHeldPawnFromEgg();
                if (newPawn == null)
                {
                    return false;
                }

                GenSpawn.Spawn(newPawn, spawnCell, Map);
                boundPawn = newPawn;
                breachGraceTicks = 0;
                newPawn.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.Notify_ContainedInRoom(this);

                if (takee.holdingOwner != null)
                {
                    takee.holdingOwner.Remove(takee);
                }

                if (!takee.Destroyed)
                {
                    takee.Destroy(DestroyMode.Vanish);
                }

                OnContentChanged();
                return true;
            }

            if (takee is Pawn existingPawn)
            {
                if (existingPawn.holdingOwner != null)
                {
                    existingPawn.holdingOwner.Remove(existingPawn);
                }

                if (existingPawn.Spawned)
                {
                    existingPawn.Position = spawnCell;
                    existingPawn.pather?.StopDead();
                    existingPawn.Notify_Teleported(true, true);
                }
                else
                {
                    GenSpawn.Spawn(existingPawn, spawnCell, Map);
                }

                boundPawn = existingPawn;
                breachGraceTicks = 0;
                existingPawn.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.Notify_ContainedInRoom(this);
                OnContentChanged();
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public void EjectContents()
        {
            ClearMeltdown(resolvedByWork: false);
            StopWorkingSustainer();
            Pawn prev = boundPawn;
            boundPawn = null;
            breachGraceTicks = 0;

            prev?.Drawer?.renderer?.SetAnimation(null);
            prev?.GetComp<CompAbnormalityHoldingPlatformTarget>()?.Notify_ReleasedFromContainment();
            prev?.jobs?.EndCurrentJob(JobCondition.InterruptForced);

            OnContentChanged();
        }

        /// <summary>判断当前收容室是否具备进入逆卡巴拉熔毁倒计时的条件。</summary>
        public bool CanTriggerMeltdownNow()
        {
            return Spawned
                && Map != null
                && Occupied
                && !IsHoldingTool
                && !isStudySessionActive
                && !meltdownActive
                && HeldPawn != null
                && !HeldPawn.Dead
                && !HeldPawn.Downed
                && cachedEntity != null;
        }

        /// <summary>触发当前收容室的逆卡巴拉熔毁倒计时警报。</summary>
        public bool TriggerMeltdown(int durationTicks)
        {
            if (!CanTriggerMeltdownNow())
            {
                return false;
            }

            meltdownActive = true;
            meltdownTicksTotal = Mathf.Max(60, durationTicks);
            meltdownTicksRemaining = meltdownTicksTotal;

            string abnoName = HeldPawn?.LabelShortCap ?? LabelCap;
            WorkNarrationUtility.SpawnNarration(
                Map,
                DrawPos + new Vector3(0f, 0f, 0.45f),
                "LC_Meltdown_RoomTriggeredMote".Translate(abnoName),
                new Color(1.0f, 0.34f, 0.22f));
            return true;
        }

        /// <summary>解除当前收容室的逆卡巴拉熔毁状态。</summary>
        public void ClearMeltdown(bool resolvedByWork)
        {
            bool wasMeltdown = meltdownActive;
            meltdownActive = false;
            meltdownTicksRemaining = 0;
            meltdownTicksTotal = 0;

            if (wasMeltdown && resolvedByWork && Spawned && Map != null)
            {
                Defs.SoundDefOf.LC_Sound_QliphothMeltdown_Clear?.PlayOneShot(new TargetInfo(Position, Map));
                string abnoName = HeldPawn?.LabelShortCap ?? LabelCap;
                WorkNarrationUtility.SpawnNarration(
                    Map,
                    DrawPos + new Vector3(0f, 0f, 0.45f),
                    "LC_Meltdown_RoomClearedMote".Translate(abnoName),
                    new Color(0.32f, 0.94f, 0.56f));
                OnAnyMeltdownResolved?.Invoke(this);
            }
        }

        private void FailMeltdown()
        {
            Pawn held = HeldPawn;
            CompAbnormality abn = cachedEntity ?? held?.GetComp<CompAbnormality>();
            ClearMeltdown(resolvedByWork: false);

            int lostPe = 0;
            if (held != null && Util.Components.LC != null && abn != null)
            {
                Util.Components.LC.TryGetAnomalyStatusSaved(held.def, out var saved);
                int before = saved.IndiPeBoxAmount;
                lostPe = Mathf.Min(before, 5);
                if (lostPe > 0)
                {
                    saved.IndiPeBoxAmount = before - lostPe;
                    Util.Components.LC.AnomalyStatusSavedDict[held.def] = saved;
                }
            }

            if (Spawned && Map != null)
            {
                Defs.SoundDefOf.LC_Sound_QliphothMeltdown_Fail?.PlayOneShotOnCamera(Map);
            }

            if (abn != null)
            {
                Find.LetterStack?.ReceiveLetter(
                    "LC_Meltdown_RoomFailedLabel".Translate(),
                    "LC_Meltdown_RoomFailedText".Translate(held?.LabelCap ?? LabelCap, lostPe),
                    LetterDefOf.ThreatBig,
                    held ?? (Thing)this);

                if (abn.QliphothEnabled)
                {
                    abn.QliphothCountCurrent = 0;
                }
            }

            OnAnyMeltdownBreached?.Invoke(this, lostPe);
        }

        /// <inheritdoc />
        public void Notify_StudyStart(Pawn studier)
        {
            if (meltdownActive)
            {
                ClearMeltdown(resolvedByWork: true);
            }

            int curTick = Find.TickManager?.TicksGame ?? 0;
            activeSessionPeCount = 0;
            activeSessionNeCount = 0;
            activeSessionMaxBoxes = cachedEntity?.PeBoxComp?.Props.amountProdueMax ?? 0;
            isStudySessionActive = true;
            lastStudyActivityTick = curTick;
            lastStudyResult = null;
            resultDisplayUntilTick = -999999;
            activeStudier = studier;
            narrationStep = 0;
            narrationSeed = Rand.Range(0, 1000);
            nextNarrationTick = curTick + Rand.Range(210, 280);

            cachedEntity?.Notify_StudyStart(studier);
            NotifyBorderingDoorsWorkLockChanged(lockNow: true);

            if (Spawned && Map != null)
            {
                Defs.SoundDefOf.LC_Sound_ContainmentWork_Start?.PlayOneShot(new TargetInfo(Position, Map));
                if (HeldPawn != null && !IsHoldingTool)
                {
                    string startLine = WorkNarrationUtility.ResolveStartLine(studier, HeldPawn, CurWorkType, GetWorkTypeLabel());
                    if (!startLine.NullOrEmpty())
                    {
                        WorkNarrationUtility.SpawnNarration(Map, GetNarrationSpawnPos(studier), startLine, WorkNarrationUtility.GetWorkTypeAccentColor(CurWorkType));
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Notify_Studying(Pawn studier)
        {
            if (cachedEntity == null)
            {
                return;
            }

            int curTick = Find.TickManager?.TicksGame ?? 0;
            isStudySessionActive = true;
            lastStudyActivityTick = curTick;
            if (studier != null)
            {
                activeStudier = studier;
            }
            cachedEntity.Notify_Studying(studier);

            SoundDef workSound = cachedEntity.Props.soundWorking ?? Defs.SoundDefOf.LC_Sound_ContainmentWork_DefaultLoop;
            if (workSound != null && Spawned && Map != null)
            {
                if (workingSustainer == null || workingSustainer.Ended)
                {
                    workingSustainer = workSound.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                }
                workingSustainer?.Maintain();
            }

            if (curTick >= nextNarrationTick && Spawned && Map != null && HeldPawn != null && !IsHoldingTool)
            {
                string midLine = WorkNarrationUtility.ResolveMidLine(activeStudier ?? studier, HeldPawn, CurWorkType, GetWorkTypeLabel(), narrationSeed + narrationStep);
                narrationStep++;
                nextNarrationTick = curTick + Rand.Range(230, 320);
                if (!midLine.NullOrEmpty())
                {
                    WorkNarrationUtility.SpawnNarration(Map, GetNarrationSpawnPos(activeStudier ?? studier), midLine, WorkNarrationUtility.GetWorkTypeAccentColor(CurWorkType));
                }
            }
        }

        /// <inheritdoc />
        public void Notify_StudyInterval(CompPawnStatus studier)
        {
            if (cachedEntity == null)
            {
                return;
            }

            isStudySessionActive = true;
            lastStudyActivityTick = Find.TickManager?.TicksGame ?? 0;
            bool success = cachedEntity.Notify_StudyInterval(studier, CurWorkType);
            if (cachedEntity.PeBoxComp != null)
            {
                activeSessionMaxBoxes = Mathf.Max(activeSessionMaxBoxes, cachedEntity.PeBoxComp.Props.amountProdueMax);
                if (success)
                {
                    activeSessionPeCount++;
                }
                else
                {
                    activeSessionNeCount++;
                }
            }

            if (Spawned && Map != null)
            {
                (success ? Defs.SoundDefOf.LC_Sound_ContainmentWork_PeBox : Defs.SoundDefOf.LC_Sound_ContainmentWork_NeBox)?.PlayOneShot(new TargetInfo(Position, Map));
            }
        }

        /// <summary>工作完成结算时通知房间终端切换为结果展示阶段。</summary>
        public void Notify_StudyFinished(LC_StudyResult result)
        {
            bool wasActive = isStudySessionActive;
            Pawn finishStudier = activeStudier;
            isStudySessionActive = false;
            lastStudyResult = result;
            resultDisplayUntilTick = (Find.TickManager?.TicksGame ?? 0) + 210;
            activeStudier = null;
            nextNarrationTick = -999999;

            StopWorkingSustainer();
            ReleaseHeldPawnFromStudyWait();
            if (wasActive)
            {
                NotifyBorderingDoorsWorkLockChanged(lockNow: false);
            }

            if (Spawned && Map != null)
            {
                Defs.SoundDefOf.LC_Sound_ContainmentWork_Finish?.PlayOneShot(new TargetInfo(Position, Map));
                if (HeldPawn != null && !IsHoldingTool)
                {
                    string resultLine = WorkNarrationUtility.ResolveResultLine(finishStudier, HeldPawn, result);
                    if (!resultLine.NullOrEmpty())
                    {
                        WorkNarrationUtility.SpawnNarration(Map, GetNarrationSpawnPos(finishStudier), resultLine, WorkNarrationUtility.GetResultAccentColor(result));
                    }
                    Map.GetComponent<GameComponent.MapComponent_LCQliphothMeltdown>()?.Notify_ContainmentWorkCompleted(this);
                }
            }
        }

        /// <summary>工作会话结束或被中断时解除邻接门锁并刷新可达性缓存。</summary>
        public void Notify_StudySessionEnded()
        {
            StopWorkingSustainer();
            activeStudier = null;
            nextNarrationTick = -999999;
            ReleaseHeldPawnFromStudyWait();
            if (isStudySessionActive)
            {
                isStudySessionActive = false;
                NotifyBorderingDoorsWorkLockChanged(lockNow: false);
            }
        }

        private void StopWorkingSustainer()
        {
            if (workingSustainer != null && !workingSustainer.Ended)
            {
                workingSustainer.End();
            }
            workingSustainer = null;
        }

        private Vector3 GetNarrationSpawnPos(Pawn studier)
        {
            Vector3 anchor;
            if (HeldPawn != null && HeldPawn.Spawned)
            {
                anchor = (studier != null && studier.Spawned && studier.Map == HeldPawn.Map)
                    ? (HeldPawn.DrawPos + studier.DrawPos) * 0.5f
                    : HeldPawn.DrawPos;
            }
            else
            {
                anchor = DrawPos;
            }

            return anchor + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, 0.55f);
        }

        private void ReleaseHeldPawnFromStudyWait()
        {
            if (HeldPawn is Pawn held
                && held.Spawned
                && !held.Dead
                && !held.Downed
                && held.CurJobDef == RimWorld.JobDefOf.Wait)
            {
                held.jobs?.EndCurrentJob(JobCondition.Succeeded, startNewJob: true);
            }
        }

        private void ClearStudySessionState()
        {
            ClearMeltdown(resolvedByWork: false);
            StopWorkingSustainer();
            bool wasActive = isStudySessionActive;
            activeSessionPeCount = 0;
            activeSessionNeCount = 0;
            activeSessionMaxBoxes = 0;
            isStudySessionActive = false;
            lastStudyActivityTick = -999999;
            lastStudyResult = null;
            resultDisplayUntilTick = -999999;
            activeStudier = null;
            nextNarrationTick = -999999;
            if (wasActive || Spawned)
            {
                NotifyBorderingDoorsWorkLockChanged(lockNow: false);
            }
        }

        private void NotifyBorderingDoorsWorkLockChanged(bool lockNow)
        {
            if (!Spawned || Map == null)
            {
                return;
            }

            Map.reachability?.ClearCache();
            if (!lockNow)
            {
                return;
            }

            Room room = ContainmentRoom;
            if (room == null || room.TouchesMapEdge)
            {
                return;
            }

            foreach (IntVec3 c in room.Cells)
            {
                for (int d = 0; d < GenAdj.CardinalDirections.Length; d++)
                {
                    IntVec3 adj = c + GenAdj.CardinalDirections[d];
                    if (adj.InBounds(Map) && adj.GetDoor(Map) is Building_LCContainmentDoor lcDoor)
                    {
                        lcDoor.ForceCloseForWorkLock();
                    }
                }
            }
        }

        private void OnContentChanged()
        {
            RefreshCachedEntity();
            GetComp<CompAssignableToPawn_LC_Entity>()?.ClearAllAssignments();
        }

        private void RefreshCachedEntity()
        {
            ClearStudySessionState();
            Pawn pawn = HeldPawn;
            if (pawn == null)
            {
                cachedEntity = null;
                compAbnormalityStudiable = null;
                cachedTool = null;
                return;
            }

            cachedEntity = pawn.GetComp<CompAbnormality>();
            compAbnormalityStudiable = pawn.GetComp<CompAbnormalityStudiable>();
            cachedTool = pawn.GetComp<CompAbnormalityTool>();
        }

        private static Color GetRiskBadgeColor(string riskTag)
        {
            switch (riskTag)
            {
                case "ZAYIN": return new Color(0.118f, 0.941f, 0.118f);
                case "TETH": return new Color(0.110f, 0.643f, 0.973f);
                case "HE": return new Color(0.969f, 0.906f, 0.047f);
                case "WAW": return new Color(0.518f, 0.165f, 0.941f);
                case "ALEPH": return new Color(0.957f, 0.094f, 0.094f);
                default: return TerminalBadgeColor;
            }
        }

        private static Texture2D GetRiskBadgeTexture(string riskTag)
        {
            switch (riskTag)
            {
                case "ZAYIN": return TexRiskZayin;
                case "TETH": return TexRiskTeth;
                case "HE": return TexRiskHe;
                case "WAW": return TexRiskWaw;
                case "ALEPH": return TexRiskAleph;
                default: return TexRiskUnknown;
            }
        }

        private static void DrawRiskBadge(Rect rect, string riskTag, Color riskColor)
        {
            GUI.color = Color.white;
            Texture2D texture = GetRiskBadgeTexture(riskTag);
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture);
                return;
            }

            Widgets.DrawBoxSolid(rect, riskColor);
            GUI.color = new Color(0.03f, 0.03f, 0.04f);
            Widgets.DrawBox(rect, 1);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.LabelFit(rect, riskTag.NullOrEmpty() ? "???" : riskTag);
        }

        private static void DrawCompactHeaderFrame(Rect rect)
        {
            GUI.color = Color.white;
            if (TexRoomHeaderCompactFrame != null)
            {
                GUI.DrawTexture(rect, TexRoomHeaderCompactFrame);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.027f, 0.027f, 0.035f));
                GUI.color = new Color(0.937f, 0.898f, 0.541f);
                Widgets.DrawBox(rect, 1);
            }
        }

        // 1px stepped chamfers keep the stamp sharp at gameplay scale.
        private static void DrawAngularHudTag(Rect rect, Color accent)
        {
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y, rect.width - 2f, rect.height), new Color(0.031f, 0.031f, 0.039f));
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f), new Color(0.031f, 0.031f, 0.039f));
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y, rect.width - 2f, 1f), accent);
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.yMax - 1f, rect.width - 2f, 1f), accent);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 1f, 1f, rect.height - 2f), accent);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y + 1f, 1f, rect.height - 2f), accent);
        }

        private static void DrawGaugeThreshold(Rect barRect, float y, Color color)
        {
            GUI.color = Color.white;
            float lineY = Mathf.Round(y);
            float thickness = barRect.width >= 32f ? 2f : 1f;
            float ear = barRect.width >= 32f ? 4f : 3f;
            Widgets.DrawBoxSolid(new Rect(barRect.x - ear, lineY, barRect.width + ear * 2f, thickness), color);
            Widgets.DrawBoxSolid(new Rect(barRect.x - ear, lineY - ear * 0.5f, thickness, thickness + ear), color);
            Widgets.DrawBoxSolid(new Rect(barRect.xMax + ear - thickness, lineY - ear * 0.5f, thickness, thickness + ear), color);
        }

        private static void DrawCenteredScaledLabel(Rect rect, string text, float scaleFactor)
        {
            if (scaleFactor <= 1.02f)
            {
                Widgets.Label(rect, text);
                return;
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            try
            {
                GUIUtility.ScaleAroundPivot(new Vector2(scaleFactor, scaleFactor), rect.center);
                Widgets.Label(rect, text);
            }
            finally
            {
                GUI.matrix = oldMatrix;
            }
        }

        private static Texture2D GetWorkTypeIcon(EAnomalyWorkType workType)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct: return TexWorkInstinct;
                case EAnomalyWorkType.Insight: return TexWorkInsight;
                case EAnomalyWorkType.Attachment: return TexWorkAttachment;
                case EAnomalyWorkType.Repression: return TexWorkRepression;
                default: return TexWorkInstinct;
            }
        }

        private static Texture2D GetResultBadgeTexture(LC_StudyResult result)
        {
            switch (result)
            {
                case LC_StudyResult.Good: return TexResultGood;
                case LC_StudyResult.Normal: return TexResultNormal;
                case LC_StudyResult.Bad: return TexResultBad;
                default: return TexResultNormal;
            }
        }

        private static Vector2 WorldToScreenGUI(Vector3 worldPos)
        {
            Vector2 screenPos = Find.Camera.WorldToScreenPoint(worldPos) / Prefs.UIScale;
            screenPos.y = Verse.UI.screenHeight - screenPos.y;
            return screenPos;
        }

        private void EnsureRoomHudAnchors()
        {
            int curTick = Find.TickManager?.TicksGame ?? 0;
            Room room = ContainmentRoom;
            int roomId = (room != null && !room.TouchesMapEdge && !room.PsychologicallyOutdoors) ? room.ID : -1;

            if (roomId == cachedRoomAnchorId && curTick - cachedRoomAnchorTick < 60 && cachedRoomAnchorTick != -999999)
            {
                return;
            }

            cachedRoomAnchorId = roomId;
            cachedRoomAnchorTick = curTick;

            if (roomId >= 0)
            {
                int minX = int.MaxValue;
                int maxX = int.MinValue;
                int minZ = int.MaxValue;
                int maxZ = int.MinValue;
                bool any = false;
                foreach (IntVec3 c in room.Cells)
                {
                    any = true;
                    if (c.x < minX) minX = c.x;
                    if (c.x > maxX) maxX = c.x;
                    if (c.z < minZ) minZ = c.z;
                    if (c.z > maxZ) maxZ = c.z;
                }

                if (any)
                {
                    cachedRoomWidthCells = (maxX - minX + 1) + 2f;
                    cachedRoomHeightCells = (maxZ - minZ + 1) + 2f;
                    cachedRoomTopAnchor = new Vector3((minX + maxX + 1f) * 0.5f, 0f, maxZ + 2.02f);
                    cachedRoomLeftAnchor = new Vector3(minX - 1.05f, 0f, (minZ + maxZ + 1f) * 0.5f);
                    return;
                }
            }

            cachedRoomWidthCells = 3f;
            cachedRoomHeightCells = 3f;
            cachedRoomTopAnchor = DrawPos + new Vector3(0f, 0f, 0.85f);
            cachedRoomLeftAnchor = DrawPos + new Vector3(-0.75f, 0f, 0f);
        }

        private string GetWorkTypeLabel()
        {
            if (IsHoldingTool)
            {
                return "TOOL";
            }

            string tipKey = "TipDialogWorkType_" + CurWorkType;
            if (tipKey.CanTranslate())
            {
                return tipKey.Translate().ToString();
            }

            string cmdKey = "CommandAssignmentWorkTypeLabel_" + CurWorkType;
            if (cmdKey.CanTranslate())
            {
                return cmdKey.Translate().ToString();
            }

            return CurWorkType.ToString();
        }

        /// <inheritdoc />
        public override void DrawGUIOverlay()
        {
            base.DrawGUIOverlay();
            if (HeldPawn == null || Find.Camera == null)
            {
                return;
            }

            EnsureRoomHudAnchors();
            Vector2 topScreen = WorldToScreenGUI(cachedRoomTopAnchor);
            bool topVisible = topScreen.x >= -180f && topScreen.x <= Verse.UI.screenWidth + 180f
                && topScreen.y >= -100f && topScreen.y <= Verse.UI.screenHeight + 100f;

            float cellPx = (Find.CameraDriver != null && Find.CameraDriver.CellSizePixels > 0.1f)
                ? Find.CameraDriver.CellSizePixels
                : 42f;
            float roomScreenW = Mathf.Max(3f, cachedRoomWidthCells) * cellPx;
            float roomScreenH = Mathf.Max(3f, cachedRoomHeightCells) * cellPx;
            bool isSelected = Find.Selector != null && (Find.Selector.IsSelected(this) || (HeldPawn != null && Find.Selector.IsSelected(HeldPawn)));

            bool showDetailRow = CompWorkable == null || CompWorkable.UIAllowed;
            bool isMelting = IsInMeltdown;
            string riskTag = cachedEntity?.AbnormalityCategory?.defName;
            Color riskColor = GetRiskBadgeColor(riskTag);
            bool qliphothEnabled = cachedEntity != null && cachedEntity.QliphothEnabled;
            bool qliphothCritical = isMelting || (qliphothEnabled && QliphothCounter <= 1);
            Color qliphothNumColor = qliphothCritical
                ? new Color(1.0f, 0.941f, 0.933f)
                : new Color(0.980f, 0.949f, 0.588f);
            string qDisplay = qliphothEnabled ? QliphothCounter.ToString() : "-";

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                if (topVisible)
                {
                    if (cellPx < 17f)
                    {
                        // Tier 3 (Ultra-far zoom): hide wide bar; show only a compact Qliphoth pod when melting, critical, working, or selected
                        if ((isMelting || qliphothCritical || isStudySessionActive || isSelected) && (cellPx >= 9f || qliphothCritical || isMelting))
                        {
                            float qPodSize = 20f;
                            Rect qPodRect = new Rect(topScreen.x - qPodSize * 0.5f, topScreen.y - qPodSize - 2f, qPodSize, qPodSize);
                            GUI.color = Color.white;
                            Texture2D qTex = qliphothCritical ? (TexQliphothAlertFrame ?? TexQliphothFrame) : TexQliphothFrame;
                            GUI.DrawTexture(qPodRect, qTex ?? BaseContent.WhiteTex);
                            Text.Font = GameFont.Tiny;
                            Text.Anchor = TextAnchor.MiddleCenter;
                            GUI.color = qliphothNumColor;
                            Widgets.Label(new Rect(qPodRect.x, qPodRect.y + qPodSize * 0.0625f, qPodRect.width, qPodRect.height), qDisplay);

                            if (isMelting)
                            {
                                Rect miniBadge = new Rect(qPodRect.center.x - 22f, qPodRect.y - 16f, 44f, 14f);
                                DrawMeltdownRoomBanner(miniBadge, isCompact: true);
                            }
                        }
                    }
                    else if (cellPx < 30f || roomScreenW < 150f || !showDetailRow)
                    {
                        // Tier 2 and detail-off share one inked strip and docked counter.
                        bool detailOff = cellPx >= 30f && roomScreenW >= 150f;
                        float detailScale = detailOff ? Mathf.Clamp(cellPx / 46f, 0.75f, 1.75f) : 1f;
                        float tagH = detailOff ? Mathf.Round(Mathf.Clamp(26f * detailScale, 22f, 44f)) : 21f;
                        float qPodSize = tagH;
                        float mainW = Mathf.Round(detailOff
                            ? Mathf.Clamp(roomScreenW * 0.80f, 124f, 360f)
                            : Mathf.Clamp(roomScreenW * 1.08f, 78f, 132f));
                        float overlap = Mathf.Round(mainW * 0.03f);
                        float totalW = mainW + qPodSize - overlap;
                        Rect stripRect = new Rect(topScreen.x - totalW * 0.5f, topScreen.y - tagH - 3f, mainW, tagH);
                        Rect qPodRect = new Rect(stripRect.xMax - overlap, stripRect.y, qPodSize, qPodSize);

                        if (isMelting)
                        {
                            float compactMeltH = detailOff ? Mathf.Round(Mathf.Clamp(18f * detailScale, 16f, 28f)) : 16f;
                            DrawMeltdownRoomBanner(new Rect(stripRect.x, stripRect.y - compactMeltH - 2f, totalW, compactMeltH), isCompact: true);
                        }

                        DrawCompactHeaderFrame(stripRect);
                        GUI.color = Color.white;
                        Widgets.DrawBoxSolid(new Rect(stripRect.x + 4f, stripRect.y + 5f, 2f, tagH - 10f), riskColor);
                        float nameX = stripRect.x + 9f;
                        if (detailOff && mainW >= 140f)
                        {
                            float rH = Mathf.Round(tagH * 0.68f);
                            Rect riskRect = new Rect(nameX, stripRect.center.y - rH * 0.5f, Mathf.Round(rH * 2.5f), rH);
                            DrawRiskBadge(riskRect, riskTag, riskColor);
                            nameX = riskRect.xMax + Mathf.Round(3f * Mathf.Min(detailScale, 1.4f));
                        }

                        bool showCompactPe = !IsHoldingTool && mainW >= 106f;
                        float rightEdge = stripRect.x + mainW * 0.92f;
                        float peScale = detailOff ? Mathf.Min(detailScale, 1.4f) : 1f;
                        float peWidth = showCompactPe ? Mathf.Clamp((24f + PeBoxCounter.ToString().Length * 5f) * peScale, 34f, 84f) : 0f;
                        Text.Font = (detailOff && tagH >= 32f) ? GameFont.Small : GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        GUI.color = new Color(0.965f, 0.949f, 0.871f);
                        Widgets.LabelFit(new Rect(nameX, stripRect.y + 2f, Mathf.Max(12f, rightEdge - peWidth - nameX - 3f), tagH - 4f), HeldPawn.LabelShortCap);
                        if (showCompactPe)
                        {
                            Text.Anchor = TextAnchor.MiddleRight;
                            GUI.color = new Color(0.169f, 0.941f, 0.627f);
                            Widgets.LabelFit(new Rect(rightEdge - peWidth, stripRect.y + 2f, peWidth, tagH - 4f), $"PE:{PeBoxCounter}");
                        }

                        GUI.color = Color.white;
                        Texture2D qTex = qliphothCritical ? (TexQliphothAlertFrame ?? TexQliphothFrame) : TexQliphothFrame;
                        GUI.DrawTexture(qPodRect, qTex ?? BaseContent.WhiteTex);
                        Text.Font = (detailOff && qPodSize >= 32f) ? GameFont.Small : GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = qliphothNumColor;
                        float compactQScale = (detailOff && qPodSize >= 32f) ? Mathf.Clamp(qPodSize / 34f, 1.0f, 1.35f) : 1.0f;
                        DrawCenteredScaledLabel(new Rect(qPodRect.x, qPodRect.y + qPodSize * 0.0625f, qPodRect.width, qPodRect.height), qDisplay, compactQScale);
                    }
                    else
                    {
                        // Tier 1 (Close / Normal zoom): dual-deck RoomHeaderFrame continuously scaled to zoom & room width
                        float scale = Mathf.Clamp(cellPx / 46f, 0.68f, 1.85f);
                        float qPodSize = Mathf.Round(Mathf.Clamp(56f * scale, 36f, 102f));
                        float roomCapFactor = cellPx <= 50f ? 1.18f : Mathf.Lerp(1.18f, 0.88f, Mathf.InverseLerp(50f, 95f, cellPx));
                        float maxTotalW = Mathf.Clamp(roomScreenW * roomCapFactor, 178f, 560f);
                        const float overlapRatio = 0.03f;
                        float maxMainW = Mathf.Max(146f, (maxTotalW - qPodSize) / (1f - overlapRatio));
                        float mainWidth = Mathf.Round(Mathf.Clamp(232f * scale, 146f, maxMainW));
                        float height = Mathf.Round(Mathf.Clamp(64f * scale, 48f, 114f));
                        float overlap = Mathf.Round(mainWidth * overlapRatio);
                        float totalWidth = mainWidth + qPodSize - overlap;

                        Rect mainRect = new Rect(topScreen.x - totalWidth * 0.5f, topScreen.y - height - 3f, mainWidth, height);
                        Rect qPodRect = new Rect(mainRect.xMax - overlap, mainRect.center.y - qPodSize * 0.5f, qPodSize, qPodSize);

                        if (isMelting)
                        {
                            float meltH = Mathf.Round(Mathf.Clamp(28f * scale, 24f, 48f));
                            DrawMeltdownRoomBanner(new Rect(mainRect.x, mainRect.y - meltH - 3f, mainWidth, meltH), isCompact: false);
                        }

                        GUI.color = Color.white;
                        GUI.DrawTexture(mainRect, TexRoomHeaderFrame ?? BaseContent.WhiteTex);

                        bool useLargeHud = height >= 84f && mainWidth >= 300f;
                        bool useSmallFont = height >= 50f && mainWidth >= 190f;
                        // Native cavity x=38..461, y=23..112; cables begin at x=472.
                        float padX = Mathf.Round(mainWidth * 0.08f);
                        float padY = Mathf.Round(height * 0.20f);
                        float innerW = Mathf.Round(mainWidth * 0.90f) - padX;
                        float bayGap = Mathf.Round(height * 0.055f);
                        float bayH = height * 0.30f;
                        Rect upperRect = new Rect(mainRect.x + padX, mainRect.y + padY, innerW, bayH);
                        float riskH = Mathf.Min(bayH * 0.94f, Mathf.Round(20f * scale));
                        Rect riskRect = new Rect(upperRect.x, upperRect.center.y - riskH * 0.5f, Mathf.Round(riskH * 2.5f), riskH);
                        DrawRiskBadge(riskRect, riskTag, riskColor);
                        float nameX = riskRect.xMax + Mathf.Round(4f * scale);

                        Text.Font = useLargeHud ? GameFont.Medium : (useSmallFont ? GameFont.Small : GameFont.Tiny);
                        Text.Anchor = TextAnchor.MiddleLeft;
                        GUI.color = new Color(0.965f, 0.949f, 0.871f);
                        Widgets.LabelFit(new Rect(nameX, upperRect.y, upperRect.xMax - nameX, upperRect.height), HeldPawn.LabelShortCap);

                        // Lower Deck: work label, discrete PE counter and angular status stamp.
                        Rect lowerRect = new Rect(mainRect.x + padX, mainRect.y + padY + bayH + bayGap, innerW, bayH);
                        float curX = lowerRect.x;

                        float statusW = Mathf.Round(Mathf.Clamp(48f * scale, 36f, 94f));
                        float workIconSize = Mathf.Min(bayH * 0.88f, Mathf.Round(17f * scale));
                        float peIconSize = Mathf.Min(bayH * 0.86f, Mathf.Round(16f * scale));
                        float peNumW = IsHoldingTool ? 0f : Mathf.Round(Mathf.Clamp(32f * scale, 23f, 66f));
                        float gap = Mathf.Round(Mathf.Clamp(4f * scale, 3f, 8f));
                        float iconGap = Mathf.Round(gap * 0.75f);
                        float rightReserved = statusW + gap + (IsHoldingTool ? 0f : (peIconSize + iconGap + peNumW + gap));

                        if (!IsHoldingTool)
                        {
                            Texture2D workIcon = GetWorkTypeIcon(CurWorkType);
                            if (workIcon != null)
                            {
                                GUI.color = Color.white;
                                GUI.DrawTexture(new Rect(curX, lowerRect.center.y - workIconSize * 0.5f, workIconSize, workIconSize), workIcon);
                                curX += workIconSize + iconGap;
                            }
                        }

                        string workText = GetWorkTypeLabel();
                        Text.Font = (height >= 94f && mainWidth >= 340f) ? GameFont.Medium : (useSmallFont ? GameFont.Small : GameFont.Tiny);
                        Text.Anchor = TextAnchor.MiddleLeft;
                        GUI.color = new Color(0.980f, 0.949f, 0.588f);
                        float workWidth = Mathf.Max(12f, lowerRect.xMax - rightReserved - curX);
                        Widgets.LabelFit(new Rect(curX, lowerRect.y, workWidth, lowerRect.height), workText);
                        curX += workWidth + gap;

                        if (!IsHoldingTool)
                        {
                            Texture2D peIcon = TexPeBoxIcon;
                            if (peIcon != null)
                            {
                                GUI.color = Color.white;
                                GUI.DrawTexture(new Rect(curX, lowerRect.center.y - peIconSize * 0.5f, peIconSize, peIconSize), peIcon);
                                curX += peIconSize + iconGap;
                            }

                            GUI.color = new Color(0.169f, 0.941f, 0.627f);
                            Widgets.LabelFit(new Rect(curX, lowerRect.y, peNumW, lowerRect.height), PeBoxCounter.ToString());
                        }

                        string statusTag;
                        Color statusColor;
                        if (IsHoldingTool)
                        {
                            statusTag = HeldToolComp.IsOnCooldown
                                ? "LC_ContainmentRoom_HudCooldown".Translate().ToString()
                                : "LC_ContainmentRoom_HudReady".Translate().ToString();
                            statusColor = HeldToolComp.IsOnCooldown
                                ? new Color(0.949f, 0.231f, 0.212f)
                                : new Color(0.169f, 0.941f, 0.627f);
                        }
                        else if (isStudySessionActive)
                        {
                            statusTag = "LC_ContainmentRoom_HudWork".Translate().ToString();
                            statusColor = new Color(0.980f, 0.949f, 0.588f);
                        }
                        else if (CompAssignable != null && CompAssignable.AssignedPawns.Any())
                        {
                            statusTag = "LC_ContainmentRoom_HudAuto".Translate().ToString();
                            statusColor = new Color(0.937f, 0.898f, 0.541f);
                        }
                        else if (CompAbnormalityStudiable != null && CompAbnormalityStudiable.TicksTilNextStudy <= 0)
                        {
                            statusTag = "LC_ContainmentRoom_HudReady".Translate().ToString();
                            statusColor = new Color(0.169f, 0.941f, 0.627f);
                        }
                        else
                        {
                            statusTag = "LC_ContainmentRoom_HudWait".Translate().ToString();
                            statusColor = new Color(0.52f, 0.54f, 0.38f);
                        }

                        float stH = Mathf.Min(bayH - 2f, Mathf.Round(19f * scale));
                        Rect statusPillRect = new Rect(lowerRect.xMax - statusW, lowerRect.center.y - stH * 0.5f, statusW, stH);
                        DrawAngularHudTag(statusPillRect, statusColor);
                        GUI.color = statusColor;
                        Text.Font = useLargeHud ? GameFont.Small : GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.LabelFit(statusPillRect, statusTag);

                        // Right attached Qliphoth Counter Pod
                        GUI.color = Color.white;
                        Texture2D qTex = qliphothCritical ? (TexQliphothAlertFrame ?? TexQliphothFrame) : TexQliphothFrame;
                        GUI.DrawTexture(qPodRect, qTex ?? BaseContent.WhiteTex);
                        Text.Font = useSmallFont ? GameFont.Medium : GameFont.Small;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = qliphothNumColor;
                        float qNumScale = Mathf.Clamp(qPodSize / 56f, 1.0f, 1.65f);
                        DrawCenteredScaledLabel(new Rect(qPodRect.x, qPodRect.y + qPodSize * 0.0625f, qPodRect.width, qPodRect.height), qDisplay, qNumScale);
                    }
                }

                // Left-Edge Vertical PE-Box / NE-Box Live Energy Gauge during active work & brief post-work result hold
                int curTick = Find.TickManager?.TicksGame ?? 0;
                bool showGauge = cellPx >= 18f
                    && !IsHoldingTool
                    && activeSessionMaxBoxes > 0
                    && (isStudySessionActive || curTick <= resultDisplayUntilTick);

                if (showGauge)
                {
                    Vector2 leftScreen = WorldToScreenGUI(cachedRoomLeftAnchor);
                    if (leftScreen.x >= -80f && leftScreen.x <= Verse.UI.screenWidth + 80f
                        && leftScreen.y >= -120f && leftScreen.y <= Verse.UI.screenHeight + 120f)
                    {
                        int maxBoxes = Mathf.Max(1, activeSessionMaxBoxes);
                        int peCount = Mathf.Clamp(activeSessionPeCount, 0, maxBoxes);
                        int neCount = Mathf.Clamp(activeSessionNeCount, 0, maxBoxes - peCount);

                        float barWidth = Mathf.Round(Mathf.Clamp(cellPx * 0.54f, 16f, 46f));
                        float barHeight = Mathf.Round(Mathf.Clamp(roomScreenH * 0.78f, 64f, 360f));
                        Rect barRect = new Rect(leftScreen.x - barWidth - 4f, leftScreen.y - barHeight * 0.5f, barWidth, barHeight);

                        GUI.color = Color.white;
                        GUI.DrawTexture(barRect, TexEnergyBarFrame ?? BaseContent.WhiteTex);

                        float padX = Mathf.Round(barWidth * 0.1406f);
                        float padY = Mathf.Round(barHeight * 0.0547f);
                        Rect trackRect = new Rect(barRect.x + padX, barRect.y + padY, barRect.width - padX * 2f, barRect.height - padY * 2f);
                        float slotH = trackRect.height / maxBoxes;
                        float cellGap = slotH > 5f ? 0.8f : 0.3f;

                        // Slots 0..maxBoxes-1 counted from bottom (0) to top (maxBoxes - 1)
                        for (int s = 0; s < maxBoxes; s++)
                        {
                            float slotY = trackRect.yMax - (s + 1) * slotH;
                            Rect cellRect = new Rect(trackRect.x, slotY + cellGap * 0.5f, trackRect.width, Mathf.Max(1.5f, slotH - cellGap));

                            Texture2D segTex;
                            if (s < peCount)
                            {
                                segTex = TexEnergyCubePE ?? BaseContent.WhiteTex;
                            }
                            else if (s >= maxBoxes - neCount)
                            {
                                segTex = TexEnergyCubeNE ?? BaseContent.WhiteTex;
                            }
                            else
                            {
                                segTex = TexEnergyCubeEmpty ?? BaseContent.WhiteTex;
                            }

                            GUI.color = Color.white;
                            GUI.DrawTexture(cellRect, segTex);
                        }

                        // Bad/Normal and Normal/Good threshold tick lines
                        CompProperties_PeBoxProduce peProps = cachedEntity?.PeBoxComp?.Props;
                        if (peProps != null)
                        {
                            int badThreshold = Mathf.Clamp(peProps.amountProdueRangeNormal.min, 1, maxBoxes);
                            int goodThreshold = Mathf.Clamp(peProps.amountProdueRangeNormal.max + 1, 1, maxBoxes);

                            float badLineY = trackRect.yMax - badThreshold * slotH;
                            float goodLineY = trackRect.yMax - goodThreshold * slotH;

                            DrawGaugeThreshold(barRect, badLineY, new Color(0.961f, 0.608f, 0.137f));
                            DrawGaugeThreshold(barRect, goodLineY, new Color(0.157f, 0.941f, 0.596f));
                        }

                        // Opaque ink tag keeps the readout legible on all terrain.
                        bool largeReadout = barWidth >= 34f;
                        float lineH = largeReadout ? 20f : 15f;
                        float tagW = Mathf.Max(42f, barWidth + 18f);
                        float tagH = neCount > 0 ? (lineH * 2f + 4f) : (lineH + 4f);
                        Rect readoutRect = new Rect(barRect.center.x - tagW * 0.5f, barRect.yMax + 3f, tagW, tagH);
                        GUI.color = Color.white;
                        Widgets.DrawBoxSolid(new Rect(readoutRect.x + 2f, readoutRect.y, readoutRect.width - 4f, readoutRect.height), new Color(0.027f, 0.027f, 0.035f));
                        Widgets.DrawBoxSolid(new Rect(readoutRect.x, readoutRect.y + 2f, readoutRect.width, readoutRect.height - 4f), new Color(0.027f, 0.027f, 0.035f));
                        Widgets.DrawBoxSolid(new Rect(readoutRect.x + 3f, readoutRect.y, readoutRect.width - 6f, 1f), new Color(0.937f, 0.898f, 0.541f));
                        Text.Font = largeReadout ? GameFont.Small : GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = new Color(0.157f, 0.941f, 0.596f);
                        Widgets.Label(new Rect(readoutRect.x, readoutRect.y + 2f, readoutRect.width, lineH), $"+{peCount}");
                        if (neCount > 0)
                        {
                            GUI.color = new Color(0.949f, 0.231f, 0.212f);
                            Widgets.Label(new Rect(readoutRect.x, readoutRect.y + 2f + lineH, readoutRect.width, lineH), $"-{neCount}");
                        }

                        // Post-work final result face badge above gauge
                        if (!isStudySessionActive && lastStudyResult.HasValue)
                        {
                            Texture2D badgeTex = GetResultBadgeTexture(lastStudyResult.Value);
                            if (badgeTex != null)
                            {
                                float badgeSize = Mathf.Round(Mathf.Clamp(barWidth * 1.15f, 18f, 52f));
                                GUI.color = Color.white;
                                GUI.DrawTexture(new Rect(barRect.center.x - badgeSize * 0.5f, barRect.y - badgeSize - 4f, badgeSize, badgeSize), badgeTex);
                            }
                        }
                    }
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        private void DrawMeltdownRoomBanner(Rect bannerRect, bool isCompact)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 8.0f);
                GUI.color = Color.white;
                Rect strip;
                if (!isCompact)
                {
                    if (TexMeltdownBannerFrame != null)
                    {
                        GUI.DrawTexture(bannerRect, TexMeltdownBannerFrame);
                    }
                    else
                    {
                        Widgets.DrawBoxSolid(bannerRect, new Color(0.102f, 0.024f, 0.031f));
                        GUI.color = new Color(0.949f, 0.282f, 0.133f);
                        Widgets.DrawBox(bannerRect, 1);
                    }
                    // Native safe strip: x=90..446, y=12..60 of 512x72.
                    strip = new Rect(bannerRect.x + bannerRect.width * (90f / 512f),
                        bannerRect.y + bannerRect.height / 6f,
                        bannerRect.width * (356f / 512f), bannerRect.height * (48f / 72f));
                }
                else
                {
                    Widgets.DrawBoxSolid(bannerRect, new Color(0.027f, 0.027f, 0.035f));
                    GUI.color = new Color(0.949f, 0.282f, 0.133f);
                    Widgets.DrawBox(bannerRect, 1);
                    strip = new Rect(bannerRect.x + 2f, bannerRect.y + 2f, bannerRect.width - 4f, bannerRect.height - 4f);
                }

                GUI.color = Color.white;
                float ratio = Mathf.Clamp01(MeltdownRemainingRatio);
                if (ratio > 0f)
                {
                    Rect fill = new Rect(strip.x, strip.y, strip.width * ratio, strip.height);
                    Widgets.DrawBoxSolid(fill, Color.Lerp(new Color(0.65f, 0.09f, 0.07f), new Color(0.949f, 0.282f, 0.133f), pulse));
                }

                Text.Font = (!isCompact && bannerRect.height >= 34f) ? GameFont.Small : GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                int secs = Mathf.CeilToInt(MeltdownSecondsRemaining);
                string label = isCompact ? $"{secs}s" : "LC_Meltdown_HudBanner".Translate(secs).ToString();
                GUI.color = new Color(0.027f, 0.027f, 0.035f);
                Widgets.LabelFit(new Rect(strip.x + 1f, strip.y + 1f, strip.width - 1f, strip.height - 1f), label);
                GUI.color = Color.Lerp(new Color(0.980f, 0.949f, 0.588f), Color.white, pulse);
                Widgets.LabelFit(strip, label);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        /// <inheritdoc />
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            Room room = ContainmentRoom;
            if (room != null && !room.TouchesMapEdge && !room.PsychologicallyOutdoors)
            {
                GenDraw.DrawFieldEdges(room.Cells.ToList(), new Color(0.96f, 0.90f, 0.44f));
            }

            if (HeldPawn != null && HeldPawn.Spawned)
            {
                GenDraw.DrawLineBetween(DrawPos, HeldPawn.DrawPos, SimpleColor.Yellow);
            }

            HeldToolComp?.DrawExtraSelectionOverlaysForUnit(this);
        }

        /// <inheritdoc />
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (IsHoldingTool)
            {
                foreach (Gizmo toolGizmo in HeldToolComp.GetPlatformGizmos())
                {
                    yield return toolGizmo;
                }
            }
            else if (CompWorkable != null)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "CommandToggleShowWorkableLabel".Translate(),
                    defaultDesc = "CommandToggleShowWorkableDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/WorkableUI"),
                    isActive = () => CompWorkable.UIAllowed,
                    toggleAction = () => CompWorkable.UIAllowed = !CompWorkable.UIAllowed
                };

                Command_Action workCmd = new Command_Action
                {
                    defaultLabel = "CommandAssignmentWorkTypeLabel".Translate(),
                    defaultDesc = "CommandAssignmentWorkTypeDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/" + CurWorkType, true),
                    action = () => Find.WindowStack.Add(new Dialog_LC_AssignWorkType(this))
                };
                workCmd.onHover = () => workCmd.defaultDesc = ("CommandAssignmentWorkTypeOnHoverDesc_" + CurWorkType).Translate();
                if (!EntityCached)
                {
                    workCmd.Disable("LC_AssignmentGizmo_HoldingPlatform_NoAbnormalityOnPlatform_Desc".Translate());
                }
                yield return workCmd;

                if (EntityCached)
                {
                    foreach (Gizmo entityGizmo in cachedEntity.GetPlatformGizmos())
                    {
                        yield return entityGizmo;
                    }
                }
            }

            yield return Util.Gizmos.Get_EntityCodex(HeldPawn);

            if (DebugSettings.ShowDevGizmos && Occupied && !IsHoldingTool)
            {
                yield return new Command_Action
                {
                    defaultLabel = IsInMeltdown ? "DEV: Clear Meltdown" : "DEV: Trigger Meltdown",
                    action = delegate
                    {
                        if (IsInMeltdown)
                        {
                            ClearMeltdown(resolvedByWork: true);
                        }
                        else
                        {
                            int secs = Mathf.Clamp(Settings.Setting_LCAnomalyCore_Main.Settings?.QliphothMeltdown_CountdownSeconds ?? 60, 20, 180);
                            TriggerMeltdown(secs * 60);
                        }
                    }
                };
            }
        }

        /// <inheritdoc />
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
            {
                yield return opt;
            }

            if (IsHoldingTool)
            {
                foreach (FloatMenuOption toolOpt in HeldToolComp.CompFloatMenuOptions(selPawn))
                {
                    yield return toolOpt;
                }
            }
            else if (EntityCached)
            {
                foreach (FloatMenuOption entityOpt in cachedEntity.GetPlatformFloatMenuOptions(selPawn))
                {
                    yield return entityOpt;
                }
            }
        }

        /// <inheritdoc />
        public override string GetInspectString()
        {
            inspectTextSB.Clear();

            string baseInspect = base.GetInspectString();
            if (!baseInspect.NullOrEmpty())
            {
                inspectTextSB.AppendLine(baseInspect);
            }

            if (IsRoomValidForContainment(out string failReason))
            {
                inspectTextSB.Append("LC_ContainmentRoom_StatusValid".Translate().Colorize(ColorLibrary.Green));
            }
            else
            {
                inspectTextSB.Append("LC_ContainmentRoom_StatusInvalid".Translate(failReason ?? string.Empty).Colorize(ColorLibrary.RedReadable));
            }

            Pawn heldPawn = HeldPawn;
            if (heldPawn != null)
            {
                TaggedString ts = "LC_HoldingThing".Translate() + ": " + heldPawn.NameShortColored.CapitalizeFirst();
                bool safe = LCContainmentUtility.SafelyContains(this, heldPawn);
                if (!safe)
                {
                    float minimumStrength = LCContainmentUtility.GetMinimumContainmentStrength(heldPawn);
                    ts += " (" + "LC_HoldingPlatformRequiresStrength".Translate(Defs.StatDefOf.LC_MinimumContainmentStrength.Worker.ValueToString(minimumStrength, finalized: false)) + ")";
                }

                inspectTextSB.Append("\n").Append(ts.Colorize(safe ? Color.white : ColorLibrary.RedReadable));

                if (IsHoldingTool)
                {
                    string toolInspect = HeldToolComp.CompInspectStringExtra();
                    if (!toolInspect.NullOrEmpty())
                    {
                        inspectTextSB.Append("\n").Append(toolInspect);
                    }
                }
                else if (EntityCached)
                {
                    string qLine = cachedEntity.QliphothEnabled
                        ? "LC_ContainmentRoom_InspectQliphoth".Translate(QliphothCounter, QliphothCounterMax)
                        : "LC_ContainmentRoom_InspectQliphothNone".Translate();
                    Color qColor = (cachedEntity.QliphothEnabled && QliphothCounter <= 1) ? ColorLibrary.RedReadable : Color.white;
                    inspectTextSB.Append("\n").Append(qLine.Colorize(qColor));
                    inspectTextSB.Append("\n").Append("LC_ContainmentRoom_InspectPeBox".Translate(PeBoxCounter).Colorize(new Color(0.55f, 0.95f, 0.72f)));
                    if (IsInMeltdown)
                    {
                        inspectTextSB.Append("\n").Append("LC_ContainmentRoom_InspectMeltdown".Translate(Mathf.CeilToInt(MeltdownSecondsRemaining)).Colorize(ColorLibrary.RedReadable));
                    }
                    if (isStudySessionActive && activeSessionMaxBoxes > 0)
                    {
                        inspectTextSB.Append("\n").Append("LC_ContainmentRoom_InspectWorkProgress".Translate(activeSessionPeCount, activeSessionNeCount, activeSessionMaxBoxes));
                    }

                    string entityInspect = cachedEntity.GetPlatformInspectString();
                    if (!entityInspect.NullOrEmpty())
                    {
                        inspectTextSB.Append("\n").Append(entityInspect);
                    }
                }
            }
            else
            {
                inspectTextSB.Append("\n").Append("LC_HoldingThing".Translate() + ": " + "Nothing".Translate().CapitalizeFirst());
            }

            return inspectTextSB.ToString();
        }

        /// <inheritdoc />
        public RoofCollapseResponse Notify_OnBeforeRoofCollapse()
        {
            if (!Occupied)
            {
                return RoofCollapseResponse.None;
            }

            if (HeldPawn is IRoofCollapseAlert roofCollapseAlert)
            {
                roofCollapseAlert.Notify_OnBeforeRoofCollapse();
            }

            foreach (IRoofCollapseAlert comp in HeldPawn.GetComps<IRoofCollapseAlert>())
            {
                comp.Notify_OnBeforeRoofCollapse();
            }

            return RoofCollapseResponse.None;
        }

        /// <inheritdoc />
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref curWorkType, "curWorkType", EAnomalyWorkType.Instinct);
            Scribe_References.Look(ref boundPawn, "boundPawn");
            Scribe_Values.Look(ref breachGraceTicks, "breachGraceTicks", 0);
            Scribe_Values.Look(ref meltdownActive, "meltdownActive", false);
            Scribe_Values.Look(ref meltdownTicksRemaining, "meltdownTicksRemaining", 0);
            Scribe_Values.Look(ref meltdownTicksTotal, "meltdownTicksTotal", 0);
        }
    }
}
