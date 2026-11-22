using System.Collections.Generic;
using System.Linq;
using System.Text;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Defs;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 工具型异想体可复用基类组件：统一管理使用次数/时长、冷却、四维门槛/经验、研究解锁、收容平台交互与退化还原。
    /// </summary>
    public class CompAbnormalityTool : ThingComp, IThingHolder
    {
        protected int useCount;
        protected int totalUseTicks;
        protected int lastUsedTick = -999999;
        protected Pawn activeContinuousUser;
        protected int currentSessionTicks;
        protected bool stopRequested;
        protected Pawn activeEquipper;
        protected int currentEquipTicks;
        protected int equipTickAccumulator;
        protected ThingOwner<Thing> enclosedContainer;
        protected int enclosedTickAccumulator;
        private bool revertedToEgg;
        private bool tickingEnclosed;
        private Sustainer enclosedUsingSustainer;

        public CompProperties_AbnormalityTool Props => (CompProperties_AbnormalityTool)props;

        public CompAbnormalityStudyUnlocks StudyUnlocksComp => parent.GetComp<CompAbnormalityStudyUnlocks>();

        /// <summary>返回当前被封闭在工具内部腔室中的单位（若有）。</summary>
        public Pawn EnclosedOccupant => enclosedContainer?.FirstOrDefault(t => t is Pawn) as Pawn;

        /// <summary>指示工具内部封闭腔室当前是否容纳了单位。</summary>
        public bool HasEnclosedOccupant => EnclosedOccupant != null;

        protected ThingOwner<Thing> EnsureEnclosedContainer() =>
            enclosedContainer ??= new ThingOwner<Thing>(this, oneStackOnly: false);

        /// <inheritdoc />
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        /// <inheritdoc />
        public ThingOwner GetDirectlyHeldThings() => EnsureEnclosedContainer();

        /// <summary>若该工具实体正收容于平台上，则返回所在收容平台。</summary>
        public Building_AbnormalityHoldingPlatform HeldPlatform => parent?.ParentHolder as Building_AbnormalityHoldingPlatform;

        /// <summary>返回供殖民者寻路与保留的实际交互目标（优先为所在收容平台，其次为实体自身）。</summary>
        public Thing InteractionTarget => (Thing)HeldPlatform ?? (parent != null && parent.Spawned ? parent : null);

        /// <summary>获取当前有效地图。</summary>
        public Map ActiveMap => HeldPlatform?.Map ?? parent?.MapHeld;

        /// <summary>获取当前有效格坐标。</summary>
        public IntVec3 ActivePosition
        {
            get
            {
                Building_AbnormalityHoldingPlatform plat = HeldPlatform;
                if (plat != null && plat.Spawned)
                {
                    return plat.Position;
                }

                return parent != null ? parent.PositionHeld : IntVec3.Invalid;
            }
        }

        /// <summary>获取当前有效世界绘制坐标。</summary>
        public Vector3 ActiveDrawPos => HeldPlatform != null ? HeldPlatform.DrawPos : (parent != null ? parent.DrawPos : Vector3.zero);

        /// <summary>指示该工具当前是否处于可交互状态（位于有效收容平台上或直接生成在地图上）。</summary>
        public bool IsOperational =>
            (HeldPlatform != null && HeldPlatform.Spawned && HeldPlatform.Map != null)
            || (parent != null && parent.Spawned && parent.Map != null);

        public int UseCount => useCount;

        public int TotalUseTicks => totalUseTicks;

        public float TotalUseSeconds => totalUseTicks / 60f;

        public Pawn ActiveContinuousUser => activeContinuousUser;

        public bool IsActiveContinuous => activeContinuousUser != null && !activeContinuousUser.Dead && !activeContinuousUser.Destroyed;

        public int CurrentSessionTicks => currentSessionTicks;

        public float CurrentSessionSeconds => currentSessionTicks / 60f;

        public bool StopRequested => stopRequested;

        /// <summary>返回当前佩戴该工具型异想体的殖民者（若有）。</summary>
        public Pawn ActiveEquipper => activeEquipper;

        /// <summary>指示该工具当前是否处于被殖民者佩戴外借状态。</summary>
        public bool IsEquipped => activeEquipper != null && !activeEquipper.Dead && !activeEquipper.Destroyed;

        /// <summary>返回当前本次佩戴已持续的 Tick 数。</summary>
        public int CurrentEquipTicks => currentEquipTicks;

        /// <summary>返回当前本次佩戴已持续的秒数。</summary>
        public float CurrentEquipSeconds => currentEquipTicks / 60f;

        public float CurrentStudyMetricValue =>
            Props.studyMetric == ToolStudyMetric.UseSeconds ? TotalUseSeconds : useCount;

        public int CooldownTicksRemaining
        {
            get
            {
                if (Props.cooldownTicks <= 0 || Find.TickManager == null)
                {
                    return 0;
                }

                int elapsed = Find.TickManager.TicksGame - lastUsedTick;
                return Mathf.Max(0, Props.cooldownTicks - elapsed);
            }
        }

        public bool IsOnCooldown => CooldownTicksRemaining > 0;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (parent.Spawned && parent.Map != null && !parent.Position.Fogged(parent.Map))
            {
                AbnormalityUtility.Discover(parent.def);
            }
        }

        /// <summary>判断指定殖民者是否满足使用该工具型异想体的前提条件。</summary>
        public virtual AcceptanceReport CanUseTool(Pawn pawn)
        {
            if (!IsOperational)
            {
                return false;
            }

            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            if (Props.toolMode == AbnormalityToolMode.Continuous && IsActiveContinuous)
            {
                if (activeContinuousUser != pawn)
                {
                    return "LC_Tool_InUse".Translate(activeContinuousUser.LabelShortCap);
                }

                // 同一使用者正在持续交互期间跳过冷却检查
                return true;
            }

            if (Props.toolMode == AbnormalityToolMode.Equippable && IsEquipped)
            {
                if (activeEquipper != pawn)
                {
                    return "LC_Tool_EquippedBy".Translate(activeEquipper.LabelShortCap);
                }

                // 佩戴者本人前来归还时跳过冷却检查
                return true;
            }

            if (IsOnCooldown)
            {
                return "LC_Tool_OnCooldown".Translate(CooldownTicksRemaining.ToStringTicksToPeriod());
            }

            if (HasStatRequirements())
            {
                CompPawnStatus status = pawn.GetComp<CompPawnStatus>();
                if (status == null || !status.Enabled)
                {
                    return "LC_Tool_InsufficientStat".Translate();
                }

                if ((int)status.GetPawnStatusELevel(EPawnStatus.Fortitude) < Props.minFortitudeLevel
                    || (int)status.GetPawnStatusELevel(EPawnStatus.Prudence) < Props.minPrudenceLevel
                    || (int)status.GetPawnStatusELevel(EPawnStatus.Temperance) < Props.minTemperanceLevel
                    || (int)status.GetPawnStatusELevel(EPawnStatus.Justice) < Props.minJusticeLevel)
                {
                    return "LC_Tool_InsufficientStat".Translate();
                }
            }

            return true;
        }

        private bool HasStatRequirements()
        {
            return Props.minFortitudeLevel > 0
                || Props.minPrudenceLevel > 0
                || Props.minTemperanceLevel > 0
                || Props.minJusticeLevel > 0;
        }

        /// <summary>工具使用工作完成时由 <c>JobDriver_UseAbnormalityTool</c> 调用。</summary>
        public virtual void OnToolUsed(Pawn user)
        {
            RecordToolUse(user, 1, 0);
        }

        /// <summary>记录工具使用次数/时长，发放员工四维经验并推进图鉴研究解锁。</summary>
        public void RecordToolUse(Pawn user, int countDelta = 1, int ticksDelta = 0)
        {
            if (countDelta > 0)
            {
                useCount += countDelta;
                lastUsedTick = Find.TickManager?.TicksGame ?? 0;
            }

            totalUseTicks += Mathf.Max(0, ticksDelta);

            CompPawnStatus status = user?.GetComp<CompPawnStatus>();
            if (status != null && status.Enabled)
            {
                if (countDelta > 0 && Props.expGainOnUse > 0f)
                {
                    status.GetPawnStatusLevel(Props.expStatusType).Exp += Props.expGainOnUse * countDelta;
                }

                if (ticksDelta > 0 && Props.expGainPerContinuousSecond > 0f)
                {
                    status.GetPawnStatusLevel(Props.expStatusType).Exp += Props.expGainPerContinuousSecond * (ticksDelta / 60f);
                }
            }

            AbnormalityUtility.Discover(parent.def);
            StudyUnlocksComp?.OnStudied(user, CurrentStudyMetricValue);
        }

        /// <summary>当前持续交互会话是否允许玩家手动中断。</summary>
        public virtual bool CanManuallyStopContinuous(Pawn user)
        {
            return Props.allowManualStopContinuous;
        }

        /// <summary>持续驻留使用开始时由 <c>JobDriver_UseAbnormalityTool</c> 调用。</summary>
        public virtual void OnContinuousStart(Pawn user)
        {
            activeContinuousUser = user;
            currentSessionTicks = 0;
            stopRequested = false;
            AbnormalityUtility.Discover(parent.def);
        }

        /// <summary>持续驻留使用按间隔推进时由 <c>JobDriver_UseAbnormalityTool</c> 调用。</summary>
        public virtual void OnContinuousTick(Pawn user, int intervalTicks, int sessionTicks)
        {
            currentSessionTicks = sessionTicks;
            RecordToolUse(user, countDelta: 0, ticksDelta: intervalTicks);
        }

        /// <summary>持续驻留使用结束（正常完成、手动中断或意外打断）时调用。</summary>
        public virtual void OnContinuousEnd(Pawn user, int sessionTicks, bool completedNaturally)
        {
            activeContinuousUser = null;
            currentSessionTicks = 0;
            stopRequested = false;

            if (sessionTicks > 0)
            {
                useCount++;
                lastUsedTick = Find.TickManager?.TicksGame ?? 0;
            }

            StudyUnlocksComp?.OnStudied(user, CurrentStudyMetricValue);
        }

        /// <summary>可佩戴工具型异想体被殖民者佩戴取走时调用。</summary>
        public virtual void OnEquipped(Pawn user)
        {
            activeEquipper = user;
            currentEquipTicks = 0;
            equipTickAccumulator = 0;
            AbnormalityUtility.Discover(parent.def);

            if (Props.equipHediffDef != null && user?.health?.hediffSet != null)
            {
                if (!user.health.hediffSet.HasHediff(Props.equipHediffDef))
                {
                    Hediff hediff = HediffMaker.MakeHediff(Props.equipHediffDef, user);
                    user.health.AddHediff(hediff);
                }
            }
        }

        /// <summary>可佩戴工具型异想体佩戴期间按间隔 Tick 时调用。</summary>
        public virtual void OnEquippableTick(Pawn user, int intervalTicks, int equippedTicks)
        {
            RecordToolUse(user, countDelta: 0, ticksDelta: intervalTicks);
        }

        /// <summary>可佩戴工具型异想体归还或卸载时调用。</summary>
        public virtual void OnUnequipped(Pawn user, int equippedTicks, bool returnedNormally)
        {
            RemoveEquipHediff(user);
            activeEquipper = null;
            currentEquipTicks = 0;
            equipTickAccumulator = 0;

            if (equippedTicks > 0)
            {
                useCount++;
                lastUsedTick = Find.TickManager?.TicksGame ?? 0;
            }

            StudyUnlocksComp?.OnStudied(user, CurrentStudyMetricValue);
        }

        /// <summary>可佩戴工具型异想体在佩戴者死亡、销毁或状态异常丢失时调用。</summary>
        public virtual void OnEquippableCarrierLost(Pawn lastUser, int equippedTicks)
        {
            RemoveEquipHediff(lastUser);
            activeEquipper = null;
            currentEquipTicks = 0;
            equipTickAccumulator = 0;
            StudyUnlocksComp?.OnStudied(lastUser, CurrentStudyMetricValue);
        }

        /// <summary>命令当前佩戴者返回收容平台归还可佩戴工具。</summary>
        public virtual bool TryOrderReturnEquipped()
        {
            if (Props.toolMode != AbnormalityToolMode.Equippable || !IsEquipped)
            {
                return false;
            }

            return TryOrderUse(activeEquipper);
        }

        protected void RemoveEquipHediff(Pawn user)
        {
            if (Props.equipHediffDef == null || user?.health?.hediffSet == null)
            {
                return;
            }

            Hediff existing = user.health.hediffSet.GetFirstHediffOfDef(Props.equipHediffDef);
            if (existing != null)
            {
                user.health.RemoveHediff(existing);
            }
        }

        /// <summary>请求中断当前持续使用工作。</summary>
        public void RequestStopContinuous()
        {
            if (IsActiveContinuous)
            {
                stopRequested = true;
                if (Props.enclosesUserInside && HasEnclosedOccupant && !tickingEnclosed && CanManuallyStopContinuous(activeContinuousUser))
                {
                    FinishEnclosedSession(completedNaturally: true);
                }
            }
        }

        /// <summary>将符合条件的殖民者真正收入内部封闭容器并从地图上隐藏。</summary>
        public virtual bool TryAcceptEnclosedUser(Pawn user)
        {
            if (Props.toolMode != AbnormalityToolMode.Continuous
                || !Props.enclosesUserInside
                || user == null
                || user.Dead
                || user.Destroyed
                || HasEnclosedOccupant
                || !CanUseTool(user).Accepted)
            {
                return false;
            }

            ThingOwner<Thing> container = EnsureEnclosedContainer();
            bool wasSelected = user.DeSpawnOrDeselect();
            bool added = user.holdingOwner != null
                ? user.holdingOwner.TryTransferToContainer(user, container)
                : container.TryAdd(user);

            if (!added)
            {
                if (ActiveMap != null && ActivePosition.IsValid && !user.Spawned)
                {
                    GenPlace.TryPlaceThing(user, ActivePosition, ActiveMap, ThingPlaceMode.Near);
                }
                return false;
            }

            Thing selectTarget = InteractionTarget;
            if (wasSelected && selectTarget != null && selectTarget.Spawned)
            {
                Find.Selector?.Select(selectTarget, playSound: false, forceDesignatorDeselect: false);
            }

            enclosedTickAccumulator = 0;
            OnContinuousStart(user);
            Find.ColonistBar?.MarkColonistsDirty();
            return true;
        }

        /// <summary>将内部封闭容器中的内容释放回工具所在的有效格。</summary>
        public virtual void EjectEnclosedContents()
        {
            if (enclosedContainer == null || enclosedContainer.Count == 0)
            {
                return;
            }

            Map map = ActiveMap;
            IntVec3 pos = ActivePosition;
            if (map != null && pos.IsValid && pos.InBounds(map))
            {
                enclosedContainer.TryDropAll(pos, map, ThingPlaceMode.Near);
            }

            Find.ColonistBar?.MarkColonistsDirty();
        }

        public override void CompTick()
        {
            base.CompTick();
            CheckContinuousUserValidity();
            TickEnclosedContinuous();
            TickEquipped();
        }

        /// <summary>收容在平台内时由 <see cref="LC_ToolEntityPawn.TickHolded"/> 驱动的每 Tick 检查。</summary>
        public virtual void TickWhileHeld()
        {
            CheckContinuousUserValidity();
            TickEnclosedContinuous();
            TickEquipped();
        }

        private void TickEquipped()
        {
            if (Props.toolMode != AbnormalityToolMode.Equippable || activeEquipper == null)
            {
                return;
            }

            bool missingHediff = Props.equipHediffDef != null
                && !(activeEquipper.health?.hediffSet?.HasHediff(Props.equipHediffDef) ?? false);
            bool carrierLost = activeEquipper.Dead || activeEquipper.Destroyed || missingHediff;

            if (carrierLost)
            {
                Pawn prevUser = activeEquipper;
                int ticks = currentEquipTicks;
                OnEquippableCarrierLost(prevUser, ticks);
                return;
            }

            currentEquipTicks++;
            equipTickAccumulator++;

            int interval = Mathf.Max(1, Props.equipTickInterval);
            if (equipTickAccumulator >= interval)
            {
                int delta = equipTickAccumulator;
                equipTickAccumulator = 0;
                OnEquippableTick(activeEquipper, delta, currentEquipTicks);
            }
        }

        private void CheckContinuousUserValidity()
        {
            if (activeContinuousUser == null)
            {
                return;
            }

            if (Props.enclosesUserInside && enclosedContainer != null && enclosedContainer.Contains(activeContinuousUser))
            {
                return;
            }

            bool invalidUser = activeContinuousUser.Dead
                || activeContinuousUser.Destroyed
                || !activeContinuousUser.Spawned
                || (activeContinuousUser.CurJobDef != null && activeContinuousUser.CurJobDef != Defs.JobDefOf.LC_UseAbnormalityTool);

            if (invalidUser)
            {
                Pawn endingUser = activeContinuousUser;
                int ticks = currentSessionTicks;
                OnContinuousEnd(endingUser, ticks, completedNaturally: false);
            }
        }

        private void TickEnclosedContinuous()
        {
            if (Props.toolMode != AbnormalityToolMode.Continuous || !Props.enclosesUserInside)
            {
                return;
            }

            if (activeContinuousUser == null)
            {
                if (enclosedContainer != null && enclosedContainer.Count > 0)
                {
                    EjectEnclosedContents();
                }
                return;
            }

            if (enclosedContainer == null || !enclosedContainer.Contains(activeContinuousUser))
            {
                return;
            }

            tickingEnclosed = true;
            try
            {
                if (Props.soundUsing != null && IsOperational)
                {
                    Thing soundHost = InteractionTarget ?? parent;
                    if (enclosedUsingSustainer == null || enclosedUsingSustainer.Ended)
                    {
                        enclosedUsingSustainer = Props.soundUsing.TrySpawnSustainer(SoundInfo.InMap(soundHost, MaintenanceType.PerTick));
                    }
                    enclosedUsingSustainer?.Maintain();
                }

                currentSessionTicks++;
                enclosedTickAccumulator++;

                int interval = Mathf.Max(1, Props.continuousTickInterval);
                if (enclosedTickAccumulator >= interval)
                {
                    int delta = enclosedTickAccumulator;
                    enclosedTickAccumulator = 0;
                    OnContinuousTick(activeContinuousUser, delta, currentSessionTicks);
                }

                bool maxReached = Props.maxContinuousDurationTicks > 0
                    && currentSessionTicks >= Props.maxContinuousDurationTicks;
                bool userGoneOrDead = activeContinuousUser == null
                    || activeContinuousUser.Dead
                    || activeContinuousUser.Destroyed
                    || !enclosedContainer.Contains(activeContinuousUser);

                if (stopRequested || maxReached || userGoneOrDead)
                {
                    FinishEnclosedSession(completedNaturally: (stopRequested || maxReached) && !userGoneOrDead);
                }
            }
            finally
            {
                tickingEnclosed = false;
            }
        }

        private void FinishEnclosedSession(bool completedNaturally)
        {
            Pawn endingUser = activeContinuousUser ?? EnclosedOccupant;
            int ticks = currentSessionTicks;

            if (enclosedUsingSustainer != null && !enclosedUsingSustainer.Ended)
            {
                enclosedUsingSustainer.End();
            }
            enclosedUsingSustainer = null;
            enclosedTickAccumulator = 0;

            EjectEnclosedContents();
            OnContinuousEnd(endingUser, ticks, completedNaturally);

            if (completedNaturally && Props.soundUsed != null && ActiveMap != null && ActivePosition.IsValid)
            {
                Props.soundUsed.PlayOneShot(new TargetInfo(ActivePosition, ActiveMap));
            }
        }

        /// <summary>当工具实体离开收容平台或平台销毁时调用，安全中止持续使用及附加效果。</summary>
        public virtual void OnLeftPlatform()
        {
            if (enclosedUsingSustainer != null && !enclosedUsingSustainer.Ended)
            {
                enclosedUsingSustainer.End();
            }
            enclosedUsingSustainer = null;

            if (HasEnclosedOccupant || (enclosedContainer != null && enclosedContainer.Count > 0))
            {
                FinishEnclosedSession(completedNaturally: false);
            }

            if (activeContinuousUser != null)
            {
                Pawn endingUser = activeContinuousUser;
                int ticks = currentSessionTicks;
                if (endingUser.jobs != null && endingUser.CurJobDef == Defs.JobDefOf.LC_UseAbnormalityTool)
                {
                    endingUser.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
                }

                if (activeContinuousUser != null)
                {
                    OnContinuousEnd(endingUser, ticks, completedNaturally: false);
                }
            }

            if (Props.toolMode == AbnormalityToolMode.Equippable && activeEquipper != null)
            {
                Pawn prevUser = activeEquipper;
                int ticks = currentEquipTicks;
                OnUnequipped(prevUser, ticks, returnedNormally: false);
            }
        }

        /// <summary>无条件洁净退化为对应的异想体蛋（保留生物特征且不触发警报/不留尸体）。</summary>
        public virtual void RevertToEgg(Map map, IntVec3 pos)
        {
            if (revertedToEgg)
            {
                return;
            }

            revertedToEgg = true;
            OnLeftPlatform();

            if (Props.eggDef != null && map != null)
            {
                IntVec3 fallbackPos = ActivePosition;
                IntVec3 dropPos = pos.IsValid && pos.InBounds(map)
                    ? pos
                    : (fallbackPos.IsValid && fallbackPos.InBounds(map) ? fallbackPos : map.Center);

                Thing egg = ThingMaker.MakeThing(Props.eggDef);
                CompAbnormality abn = parent?.GetComp<CompAbnormality>();
                if (abn != null)
                {
                    AbnormalityUtility.TransferSignature(egg, abn.biosignature);
                }

                GenPlace.TryPlaceThing(egg, dropPos, map, ThingPlaceMode.Near);
                FleckMaker.ThrowDustPuff(dropPos.ToVector3Shifted(), map, 1.2f);
            }

            if (parent is Pawn pawn)
            {
                if (pawn.Spawned)
                {
                    pawn.DeSpawn(DestroyMode.Vanish);
                }

                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
            }
            else if (parent != null && !parent.Destroyed)
            {
                parent.Destroy(DestroyMode.Vanish);
            }
        }

        /// <summary>提供供收容平台转发给玩家的自定义贴图路径（默认不覆盖）。</summary>
        protected virtual string GetHeldOverrideTexPath()
        {
            if (Props.toolMode == AbnormalityToolMode.Equippable
                && IsEquipped
                && !Props.emptyPlatformTexPath.NullOrEmpty())
            {
                return Props.emptyPlatformTexPath;
            }

            return null;
        }

        /// <summary>提供供收容平台转发给玩家的自定义染色（默认白色不染色）。</summary>
        protected virtual Color GetHeldOverrideColor() => Color.white;

        /// <summary>尝试由工具组件接管平台内实体的绘制；返回 true 时跳过默认 PawnRenderer 绘制。</summary>
        public virtual bool TryDrawHeldOverride(DrawPhase phase, Vector3 drawLoc)
        {
            string overrideTex = GetHeldOverrideTexPath();
            Color overrideColor = GetHeldOverrideColor();
            if (overrideTex == null && overrideColor == Color.white)
            {
                return false;
            }

            if (phase != DrawPhase.Draw)
            {
                return true;
            }

            Pawn pawn = parent as Pawn;
            GraphicData bodyGraphic = pawn?.kindDef?.lifeStages?.FirstOrDefault()?.bodyGraphicData;
            string texPath = overrideTex ?? bodyGraphic?.texPath;
            if (texPath.NullOrEmpty())
            {
                return false;
            }

            Vector2 drawSize = bodyGraphic?.drawSize ?? new Vector2(2.0f, 2.0f);
            CompAbnormalityHoldingPlatformTarget targetComp = pawn?.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (targetComp != null && targetComp.Props.heldDrawScale > 0f && Mathf.Abs(targetComp.Props.heldDrawScale - 1f) > 0.0001f)
            {
                drawSize *= targetComp.Props.heldDrawScale;
            }

            float y = HeldPlatform != null ? HeldPlatform.HeldPawnDrawPos_Y : drawLoc.y;
            Vector3 adjustedLoc = new Vector3(drawLoc.x, y, drawLoc.z);
            Graphic graphic = GraphicDatabase.Get<Graphic_Single>(texPath, ShaderDatabase.Cutout, drawSize, overrideColor);
            graphic?.Draw(adjustedLoc, Rot4.North, (Thing)HeldPlatform ?? parent, 0f);
            return true;
        }

        /// <summary>选中收容平台或工具实体时绘制额外连线等选中叠加层。</summary>
        public virtual void DrawExtraSelectionOverlaysForPlatform(Building_AbnormalityHoldingPlatform platform)
        {
        }

        /// <summary>根据工具交互方式（内部封闭进入格内 vs 外部相邻）返回寻路终点模式。</summary>
        public PathEndMode GetInteractionPathEndMode(Thing target)
        {
            if (Props.enclosesUserInside && target != null && target.def.passability != Traversability.Impassable)
            {
                return PathEndMode.OnCell;
            }

            return target != null && target.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.Touch;
        }

        /// <summary>命令指定 Pawn 前往使用该工具。</summary>
        public bool TryOrderUse(Pawn pawn)
        {
            Thing target = InteractionTarget;
            if (pawn == null || target == null || !CanUseTool(pawn).Accepted)
            {
                return false;
            }

            PathEndMode peMode = GetInteractionPathEndMode(target);
            if (!pawn.CanReach(target, peMode, Danger.Deadly) || !pawn.CanReserve(target))
            {
                return false;
            }

            Job job = JobMaker.MakeJob(Defs.JobDefOf.LC_UseAbnormalityTool, target);
            return pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            Thing target = InteractionTarget;
            if (selPawn == null || target == null || ActiveMap == null)
            {
                yield break;
            }

            AcceptanceReport canUse = CanUseTool(selPawn);
            bool isReturningEquippable = Props.toolMode == AbnormalityToolMode.Equippable
                && IsEquipped
                && activeEquipper == selPawn;
            string baseLabel = isReturningEquippable
                ? Props.returnLabelKey.Translate(parent.LabelCap)
                : Props.useLabelKey.Translate(parent.LabelCap);
            if (!canUse.Accepted)
            {
                string disabledLabel = canUse.Reason.NullOrEmpty()
                    ? baseLabel
                    : $"{baseLabel} ({canUse.Reason})";
                yield return new FloatMenuOption(disabledLabel, null);
                yield break;
            }

            PathEndMode peMode = GetInteractionPathEndMode(target);
            if (!selPawn.CanReach(target, peMode, Danger.Deadly))
            {
                yield return new FloatMenuOption($"{baseLabel} ({"NoPath".Translate()})", null);
                yield break;
            }

            if (!selPawn.CanReserve(target))
            {
                yield return new FloatMenuOption($"{baseLabel} ({"Reserved".Translate()})", null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(baseLabel, delegate
                {
                    TryOrderUse(selPawn);
                }),
                selPawn,
                target);
        }

        /// <summary>返回供收容平台或独立实体展示的工具专属 Gizmo（不含图鉴按钮以避免平台重复显示）。</summary>
        public virtual IEnumerable<Gizmo> GetPlatformGizmos()
        {
            Map map = ActiveMap;
            if (!IsOperational || map == null)
            {
                yield break;
            }

            Texture2D icon = !Props.useIconPath.NullOrEmpty()
                ? ContentFinder<Texture2D>.Get(Props.useIconPath, false)
                : null;
            icon ??= parent.def.uiIcon;

            bool continuousBusy = Props.toolMode == AbnormalityToolMode.Continuous && IsActiveContinuous;
            if (continuousBusy && CanManuallyStopContinuous(activeContinuousUser))
            {
                Texture2D stopIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false) ?? icon;
                yield return new Command_Action
                {
                    defaultLabel = Props.stopGizmoLabelKey.Translate(),
                    defaultDesc = Props.stopGizmoDescKey.Translate(activeContinuousUser.LabelShortCap),
                    icon = stopIcon,
                    action = delegate
                    {
                        RequestStopContinuous();
                    }
                };
            }

            bool equippableBusy = Props.toolMode == AbnormalityToolMode.Equippable && IsEquipped;
            if (equippableBusy)
            {
                Texture2D returnIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false) ?? icon;
                yield return new Command_Action
                {
                    defaultLabel = Props.returnGizmoLabelKey.Translate(),
                    defaultDesc = Props.returnGizmoDescKey.Translate(activeEquipper.LabelShortCap),
                    icon = returnIcon,
                    action = delegate
                    {
                        TryOrderReturnEquipped();
                    }
                };
            }

            Command_Action useCmd = new Command_Action
            {
                defaultLabel = Props.useGizmoLabelKey.Translate(),
                defaultDesc = Props.useGizmoDescKey.Translate(parent.LabelCap),
                icon = icon,
                action = delegate
                {
                    Map currentMap = ActiveMap;
                    if (currentMap == null)
                    {
                        return;
                    }

                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (Pawn pawn in currentMap.mapPawns.FreeColonistsSpawned)
                    {
                        AcceptanceReport report = CanUseTool(pawn);
                        if (report.Accepted)
                        {
                            Pawn captured = pawn;
                            options.Add(new FloatMenuOption(captured.LabelShortCap, delegate
                            {
                                TryOrderUse(captured);
                            }));
                        }
                        else if (!report.Reason.NullOrEmpty())
                        {
                            options.Add(new FloatMenuOption($"{pawn.LabelShortCap} ({report.Reason})", null));
                        }
                    }

                    if (options.Count == 0)
                    {
                        options.Add(new FloatMenuOption("LC_Tool_NoEligibleUser".Translate(), null));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
            if (continuousBusy)
            {
                useCmd.Disable("LC_Tool_InUse".Translate(activeContinuousUser.LabelShortCap));
            }
            else if (equippableBusy)
            {
                useCmd.Disable("LC_Tool_EquippedBy".Translate(activeEquipper.LabelShortCap));
            }
            yield return useCmd;

            if (HeldPlatform != null && Props.eggDef != null)
            {
                Texture2D eggIcon = Props.eggDef.uiIcon ?? icon;
                Command_Action revertCmd = new Command_Action
                {
                    defaultLabel = "LC_Tool_RevertToEggLabel".Translate(),
                    defaultDesc = "LC_Tool_RevertToEggDesc".Translate(parent.LabelCap),
                    icon = eggIcon,
                    action = delegate
                    {
                        Building_AbnormalityHoldingPlatform plat = HeldPlatform;
                        Map targetMap = ActiveMap;
                        IntVec3 targetPos = ActivePosition;
                        plat?.EjectContents();
                        if (!revertedToEgg && targetMap != null && targetPos.IsValid)
                        {
                            RevertToEgg(targetMap, targetPos);
                        }
                    }
                };
                if (continuousBusy && !CanManuallyStopContinuous(activeContinuousUser))
                {
                    revertCmd.Disable("LC_Tool_InUse".Translate(activeContinuousUser.LabelShortCap));
                }
                else if (equippableBusy)
                {
                    revertCmd.Disable("LC_Tool_EquippedBy".Translate(activeEquipper.LabelShortCap));
                }
                yield return revertCmd;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return Util.Gizmos.Get_EntityCodex(parent);

            if (HeldPlatform == null && IsOperational)
            {
                foreach (Gizmo g in GetPlatformGizmos())
                {
                    yield return g;
                }
            }
        }

        /// <summary>供子类附加自定义检查栏状态文本。</summary>
        public virtual string GetToolStatusInspectString() => null;

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();

            if (Props.studyMetric == ToolStudyMetric.UseSeconds)
            {
                sb.AppendLine("LC_Tool_InspectDuration".Translate(TotalUseSeconds.ToString("0.#")));
            }
            else
            {
                sb.AppendLine("LC_Tool_InspectUses".Translate(useCount));
            }

            CompAbnormalityStudyUnlocks study = StudyUnlocksComp;
            if (study != null && study.Props?.studyNotes != null)
            {
                sb.AppendLine("LC_Tool_InspectProgress".Translate(study.Progress, study.Props.studyNotes.Count));
            }

            if (IsOnCooldown)
            {
                sb.AppendLine("LC_Tool_InspectCooldown".Translate(CooldownTicksRemaining.ToStringTicksToPeriod()));
            }

            if (IsActiveContinuous)
            {
                sb.AppendLine("LC_Tool_InspectActiveUser".Translate(activeContinuousUser.LabelShortCap, CurrentSessionSeconds.ToString("0.#")));
            }

            if (Props.toolMode == AbnormalityToolMode.Equippable && IsEquipped)
            {
                sb.AppendLine("LC_Tool_InspectEquippedBy".Translate(activeEquipper.LabelShortCap, CurrentEquipSeconds.ToString("0.#")));
            }

            string custom = GetToolStatusInspectString();
            if (!custom.NullOrEmpty())
            {
                sb.AppendLine(custom);
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref useCount, "lcToolUseCount", 0);
            Scribe_Values.Look(ref totalUseTicks, "lcToolTotalUseTicks", 0);
            Scribe_Values.Look(ref lastUsedTick, "lcToolLastUsedTick", -999999);
            Scribe_References.Look(ref activeContinuousUser, "lcToolActiveUser");
            Scribe_Values.Look(ref currentSessionTicks, "lcToolCurrentSessionTicks", 0);
            Scribe_Values.Look(ref stopRequested, "lcToolStopRequested", false);
            Scribe_References.Look(ref activeEquipper, "lcToolActiveEquipper");
            Scribe_Values.Look(ref currentEquipTicks, "lcToolCurrentEquipTicks", 0);
            Scribe_Values.Look(ref equipTickAccumulator, "lcToolEquipTickAcc", 0);
            Scribe_Values.Look(ref enclosedTickAccumulator, "lcToolEnclosedTickAcc", 0);
            Scribe_Deep.Look(ref enclosedContainer, "lcToolEnclosedContainer", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureEnclosedContainer();
            }
        }
    }
}
