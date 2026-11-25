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
        private static readonly Color TerminalBadgeColor = new Color(0.95f, 0.92f, 0.55f);

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

        private readonly List<Graphic> cachedPeBoxBarGraphics = new List<Graphic>();
        private readonly List<Graphic> cachedNeBoxBarGraphics = new List<Graphic>();

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

            base.DeSpawn(mode);
        }

        /// <inheritdoc />
        protected override void Tick()
        {
            base.Tick();

            if (boundPawn == null)
            {
                return;
            }

            if (boundPawn.Destroyed || boundPawn.Dead || !boundPawn.Spawned)
            {
                boundPawn = null;
                breachGraceTicks = 0;
                OnContentChanged();
                return;
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
            Pawn prev = boundPawn;
            boundPawn = null;
            breachGraceTicks = 0;

            prev?.Drawer?.renderer?.SetAnimation(null);
            prev?.GetComp<CompAbnormalityHoldingPlatformTarget>()?.Notify_ReleasedFromContainment();
            prev?.jobs?.EndCurrentJob(JobCondition.InterruptForced);

            OnContentChanged();
        }

        /// <inheritdoc />
        public void Notify_StudyStart(Pawn studier)
        {
            cachedPeBoxBarGraphics.Clear();
            cachedNeBoxBarGraphics.Clear();
            cachedEntity?.Notify_StudyStart(studier);
        }

        /// <inheritdoc />
        public void Notify_Studying(Pawn studier)
        {
            if (cachedEntity == null)
            {
                return;
            }

            cachedEntity.Notify_Studying(studier);
            if (cachedEntity.Props.soundWorking != null)
            {
                if (workingSustainer == null || workingSustainer.Ended)
                {
                    workingSustainer = cachedEntity.Props.soundWorking.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                }
                workingSustainer.Maintain();
            }
        }

        /// <inheritdoc />
        public void Notify_StudyInterval(CompPawnStatus studier)
        {
            if (cachedEntity == null)
            {
                return;
            }

            bool success = cachedEntity.Notify_StudyInterval(studier, CurWorkType);
            if (cachedEntity.PeBoxComp != null)
            {
                if (success)
                {
                    cachedPeBoxBarGraphics.Add(GraphicUtil.CachedTopGraphic_BoxBarUnit_Get("PE", cachedEntity.PeBoxComp.Props.amountProdueMax));
                }
                else
                {
                    cachedNeBoxBarGraphics.Add(GraphicUtil.CachedTopGraphic_BoxBarUnit_Get("NE", cachedEntity.PeBoxComp.Props.amountProdueMax));
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
            Pawn pawn = HeldPawn;
            if (pawn == null)
            {
                cachedEntity = null;
                compAbnormalityStudiable = null;
                cachedTool = null;
                cachedPeBoxBarGraphics.Clear();
                cachedNeBoxBarGraphics.Clear();
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
                case "ZAYIN": return new Color(0.25f, 0.85f, 0.25f);
                case "TETH": return new Color(0.28f, 0.65f, 1.0f);
                case "HE": return new Color(0.95f, 0.82f, 0.20f);
                case "WAW": return new Color(0.72f, 0.35f, 0.96f);
                case "ALEPH": return new Color(0.95f, 0.22f, 0.25f);
                default: return TerminalBadgeColor;
            }
        }

        /// <inheritdoc />
        public override void DrawGUIOverlay()
        {
            base.DrawGUIOverlay();
            if (HeldPawn == null || Find.Camera == null)
            {
                return;
            }

            Vector2 screenPos = Find.Camera.WorldToScreenPoint(DrawPos + new Vector3(0f, 0f, 0.82f)) / Prefs.UIScale;
            screenPos.y = Verse.UI.screenHeight - screenPos.y;
            if (screenPos.x < -120f || screenPos.x > Verse.UI.screenWidth + 120f || screenPos.y < -60f || screenPos.y > Verse.UI.screenHeight + 60f)
            {
                return;
            }

            bool showDetailRow = CompWorkable == null || CompWorkable.UIAllowed;
            float width = 184f;
            float height = showDetailRow ? 36f : 19f;
            Rect panelRect = new Rect(screenPos.x - width * 0.5f, screenPos.y - height * 0.5f, width, height);

            string riskTag = cachedEntity?.AbnormalityCategory?.defName;
            string qText = cachedEntity != null && cachedEntity.QliphothEnabled
                ? $"Q:{QliphothCounter}"
                : "Q:-";

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Widgets.DrawBoxSolid(panelRect, new Color(0.06f, 0.08f, 0.11f, 0.80f));
                Widgets.DrawBoxSolid(new Rect(panelRect.x, panelRect.y, panelRect.width, 1.5f), GetRiskBadgeColor(riskTag));

                Text.Font = GameFont.Tiny;
                Rect headerRect = new Rect(panelRect.x + 4f, panelRect.y + 2f, panelRect.width - 8f, 16f);
                Rect qRect = new Rect(headerRect.xMax - 30f, headerRect.y, 30f, headerRect.height);
                Rect nameRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 32f, headerRect.height);

                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = GetRiskBadgeColor(riskTag);
                string headerLabel = riskTag.NullOrEmpty()
                    ? HeldPawn.LabelShortCap
                    : $"[{riskTag}] {HeldPawn.LabelShortCap}";
                Widgets.LabelFit(nameRect, headerLabel);

                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = (cachedEntity != null && cachedEntity.QliphothEnabled && QliphothCounter <= 1)
                    ? ColorLibrary.RedReadable
                    : TerminalBadgeColor;
                Widgets.LabelFit(qRect, qText);

                if (showDetailRow)
                {
                    Rect detailRect = new Rect(panelRect.x + 4f, panelRect.y + 18f, panelRect.width - 8f, 16f);
                    string workText = IsHoldingTool
                        ? "TOOL"
                        : ("CommandAssignmentWorkTypeLabel_" + CurWorkType).CanTranslate()
                            ? ("CommandAssignmentWorkTypeLabel_" + CurWorkType).Translate().ToString()
                            : CurWorkType.ToString();

                    string statusTag;
                    Color statusColor;
                    if (IsHoldingTool)
                    {
                        statusTag = HeldToolComp.IsOnCooldown ? "CD" : "READY";
                        statusColor = HeldToolComp.IsOnCooldown ? ColorLibrary.Orange : ColorLibrary.Green;
                    }
                    else if (CompAssignable != null && CompAssignable.AssignedPawns.Any())
                    {
                        statusTag = "AUTO";
                        statusColor = ColorLibrary.Cyan;
                    }
                    else if (CompAbnormalityStudiable != null && CompAbnormalityStudiable.TicksTilNextStudy <= 0)
                    {
                        statusTag = "READY";
                        statusColor = ColorLibrary.Green;
                    }
                    else
                    {
                        statusTag = "WAIT";
                        statusColor = Color.gray;
                    }

                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = new Color(0.85f, 0.88f, 0.92f);
                    string leftDetail = IsHoldingTool
                        ? workText
                        : $"{workText}  PE:{PeBoxCounter}";
                    Widgets.LabelFit(new Rect(detailRect.x, detailRect.y, detailRect.width - 42f, detailRect.height), leftDetail);

                    Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = statusColor;
                    Widgets.LabelFit(new Rect(detailRect.xMax - 40f, detailRect.y, 40f, detailRect.height), statusTag);
                }
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
                GenDraw.DrawFieldEdges(room.Cells.ToList(), Color.cyan);
            }

            if (HeldPawn != null && HeldPawn.Spawned)
            {
                GenDraw.DrawLineBetween(DrawPos, HeldPawn.DrawPos, SimpleColor.Cyan);
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
        }
    }
}
