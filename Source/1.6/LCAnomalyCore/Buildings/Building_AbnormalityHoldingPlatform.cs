using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Interface;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.UI;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.Buildings
{
    /// <summary>
    /// 收容平台Building
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_AbnormalityHoldingPlatform : Building, IThingHolderWithDrawnPawn, IThingHolder, IRoofCollapseAlert, ISearchableContents, IHoldingPlatformWorkTypeSelectable
    {
        #region 变量

        #region 基本

        /// <summary>表示 <c>innerContainer</c>。</summary>
        public ThingOwner innerContainer;

        /// <summary>获取 <c>HeldPawn</c>。</summary>
        public Pawn HeldPawn => innerContainer.FirstOrDefault((Thing x) => x is Pawn) as Pawn;

        /// <summary>获取 <c>Occupied</c>。</summary>
        public bool Occupied => HeldPawn != null;

        /// <summary>表示 <c>inspectTextSB</c>。</summary>
        protected StringBuilder inspectTextSB = new StringBuilder();

        private CompProperties_AbnormalityHolderPlatform PlatformProps
        {
            get
            {
                return base.GetComp<CompAbnormalityHolderPlatform>().Props;
            }
        }

        #endregion

        #region 组件

        /// <summary>
        /// 可工作UI Comp
        /// </summary>
        protected CompWorkableUI CompWorkable => compWorkable ??= GetComp<CompWorkableUI>();

        private CompWorkableUI compWorkable;

        /// <summary>
        /// 可指派工作 Comp
        /// </summary>
        public CompAssignableToPawn_LC_Entity CompAssignable => compAssignable ??= GetComp<CompAssignableToPawn_LC_Entity>();

        private CompAssignableToPawn_LC_Entity compAssignable;

        /// <summary>表示 <c>CompAbnormalityStudiable</c>。</summary>
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

        private CompAbnormalityStudiable compAbnormalityStudiable;

        private Sustainer workingSustainer;

        #endregion 组件

        #region 缓存

        /// <summary>
        /// 是否存在缓存对象
        /// </summary>
        protected bool EntityCached => cachedEntity != null;

        private CompAbnormality cachedEntity;

        /// <summary>
        /// 缓存altitude
        /// </summary>
        protected Vector3 altitudesCached = Altitudes.AltIncVect * 2f;

        /// <summary>
        /// 缓存名称贴图
        /// </summary>
        protected Graphic CachedEntityNameGraphic => cachedEntityNameGraphic
            ??= Util.GraphicUtil.EntityNamePlatformTopGraphic_Get(cachedEntity.parent.def.defName, true);

        private Graphic cachedEntityNameGraphic;

        private List<Graphic> cachedPeBoxBarGraphics = new List<Graphic>();
        private List<Graphic> cachedNeBoxBarGraphics = new List<Graphic>();

        #endregion 缓存

        #region 字段

        /// <summary>获取 <c>PawnDrawOffset</c>。</summary>
        public Vector3 PawnDrawOffset => new Vector3(0f, 0f, 0.15f);

        /// <summary>
        /// 逆卡巴拉计数器值
        /// </summary>
        public int QliphothCounter
        {
            get
            {
                if (EntityCached)
                    return cachedEntity.QliphothCountCurrent;

                return 0;
            }
        }

        /// <summary>
        /// 独立PeBox数量
        /// </summary>
        public int PeBoxCounter
        {
            get
            {
                if (EntityCached)
                {
                    var component = Util.Components.LC;
                    if (component != null)
                    {
                        component.TryGetAnomalyStatusSaved(cachedEntity.parent.def, out var saved);
                        return saved.IndiPeBoxAmount;
                    }
                }

                return 0;
            }
        }

        /// <summary>表示 <c>CurWorkType</c>。</summary>
        public EAnomalyWorkType CurWorkType
        {
            get => curWorkType;
            set => curWorkType = value;
        }

        /// <summary>表示 <c>curWorkType</c>。</summary>
        protected EAnomalyWorkType curWorkType = EAnomalyWorkType.Instinct;

        #endregion 字段

        #endregion 变量

        #region 生命周期

        /// <summary>初始化 <c>Building_AbnormalityHoldingPlatform</c> 类的新实例。</summary>
        public Building_AbnormalityHoldingPlatform()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        /// <summary>
        /// 生成时方法
        /// </summary>
        /// <param name="map"></param>
        /// <param name="respawningAfterLoad"></param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            //建造和加载存档后进行初始化和事件绑定
            Init();
            innerContainer.OnContentsChanged += OnContentChanged;
            //if (respawningAfterLoad)
            //{
            //}
        }

        /// <summary>
        /// 销毁时方法
        /// </summary>
        /// <param name="mode"></param>
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);

            innerContainer.OnContentsChanged -= OnContentChanged;
        }

        /// <summary>
        /// Tick
        /// </summary>
        protected override void Tick()
        {
            if (innerContainer.Count > 0)
            {
                var entitiy = innerContainer[0] as LC_EntityBasePawn;
                entitiy?.TickHolded();
            }
        }

        /// <inheritdoc />
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            Pawn heldPawn = this.HeldPawn;
            if (heldPawn != null)
            {
                Rot4 value = Rot4.South;
                heldPawn.Drawer.renderer.DynamicDrawPhaseAt(phase, this.DrawPos + this.PawnDrawOffset, new Rot4?(value), true);
            }
        }

        /// <summary>
        /// 每帧绘制
        /// </summary>
        /// <param name="drawLoc"></param>
        /// <param name="flip"></param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            Vector3 drawPosCached = this.DrawPos + altitudesCached;
            Vector3 drawPosUpperCached = drawPosCached + Vector3.up;

            if (HeldPawn != null)
            {
                #region 右下角可工作状态

                //如果分配工作者，就显示自动图标，否则就显示是否可工作
                if (CompAssignable.AssignedPawns.Any() && CompWorkable.UIAllowed)
                {
                    CompWorkable?.AutoWorkGraphic.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }
                else
                {
                    //工作状态更新
                    if (CompWorkable != null && CompWorkable.UIAllowed)
                    {
                        var comp = HeldPawn.GetComp<CompAbnormalityStudiable>();
                        if (comp != null)
                        {
                            var studiable = !(comp.TicksTilNextStudy > 0);
                            var graphic = studiable ? CompWorkable.AllowWorkGraphic : CompWorkable.NotAllowWorkGraphic;
                            graphic?.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                        }
                    }
                }

                #endregion 右下角可工作状态

                #region 左下角工作类型

                if (cachedEntity != null)
                {
                    var graphic = Util.GraphicUtil.WorkTypePlatformTopGraphic_Get(CurWorkType);
                    graphic?.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }

                #endregion 左下角工作类型

                #region 逆卡巴拉计数器

                //显示不可用
                if (!EntityCached || EntityCached && !cachedEntity.QliphothEnabled)
                {
                    GraphicUtil.CachedTopGraphic_QliphothIndicator_TopNull
                        ?.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }
                //不大于10就显示对应数字
                else if (QliphothCounter < 10)
                {
                    GraphicUtil.QliphothIndicator_GetCachedTopGraphic()[QliphothCounter]
                        ?.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }
                //大于10就显示9+
                else
                {
                    GraphicUtil.CachedTopGraphic_QliphothIndicator_TopMax
                        ?.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }

                #endregion 逆卡巴拉计数器

                #region Pebox计数器

                var list = GraphicUtil.IndiPeBoxIndicator_GetCachedTopGraphic(PeBoxCounter);
                foreach (var graphic in list)
                    graphic?.Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);

                #endregion Pebox计数器

                #region 平台上的异常名字UI

                if (EntityCached)
                {
                    CachedEntityNameGraphic?.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }

                #endregion 平台上的异常名字UI

                #region 分类等级UI

                var abnormalityLevel = cachedEntity?.AbnormalityCategory?.defName;
                if (!abnormalityLevel.NullOrEmpty())
                {
                    var graphicLevel = GraphicUtil.LevelIndicator_GetCachedTopGraphic(abnormalityLevel);
                    graphicLevel.Draw(drawPosCached, base.Rotation, this, 0f);
                }

                #endregion 分类等级UI

                #region 左侧Box条

                if (EntityCached)
                {
                    //NE-BOX
                    if (cachedNeBoxBarGraphics != null && cachedNeBoxBarGraphics.Count > 0)
                    {
                        for (int i = 0; i < cachedNeBoxBarGraphics.Count; i++)
                        {
                            cachedNeBoxBarGraphics[i].Draw(drawPosUpperCached + Vector3.back * cachedEntity.PeBoxComp.Props.boxTexOffsetZ * i, base.Rotation, this, 0f);
                        }
                    }

                    //PE-BOX
                    if (cachedPeBoxBarGraphics != null && cachedPeBoxBarGraphics.Count > 0)
                    {
                        for (int i = 0; i < cachedPeBoxBarGraphics.Count; i++)
                        {
                            cachedPeBoxBarGraphics[i].Draw(drawPosUpperCached + Vector3.forward * cachedEntity.PeBoxComp.Props.boxTexOffsetZ * i, base.Rotation, this, 0f);
                        }
                    }
                }

                #endregion 左侧Box条
            }
        }

        /// <summary>
        /// 保存相关
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref curWorkType, "curWorkType", EAnomalyWorkType.Instinct);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        }

        #endregion 生命周期

        #region 事件方法

        /// <summary>
        /// 收容内容改变时触发的方法
        /// </summary>
        protected void OnContentChanged()
        {
            Init();
            EntityNameUpdateForce();

            //清理原先平台的分配对象
            GetComp<CompAssignableToPawn_LC_Entity>()?.ClearAllAssignments();
        }

        /// <summary>
        /// 正在被研究时每Tick调用
        /// </summary>
        public void Notify_Studying(Pawn pawn)
        {
            cachedEntity.Notify_Studying(pawn);

            //收容室音乐播放
            if (cachedEntity.Props.soundWorking != null)
            {
                if (workingSustainer == null || workingSustainer.Ended)
                {
                    workingSustainer = cachedEntity.Props.soundWorking.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                }
                workingSustainer.Maintain();
            }
        }

        /// <summary>执行 <c>Notify_StudyInterval</c> 定义的操作。</summary>
        public void Notify_StudyInterval(CompPawnStatus studier)
        {
            bool success = cachedEntity.Notify_StudyInterval(studier, CurWorkType);

            if (success)
                cachedPeBoxBarGraphics.Add(GraphicUtil.CachedTopGraphic_BoxBarUnit_Get("PE", cachedEntity.PeBoxComp.Props.amountProdueMax));
            else
                cachedNeBoxBarGraphics.Add(GraphicUtil.CachedTopGraphic_BoxBarUnit_Get("NE", cachedEntity.PeBoxComp.Props.amountProdueMax));
        }

        /// <summary>执行 <c>Notify_StudyStart</c> 定义的操作。</summary>
        public void Notify_StudyStart(Pawn studier)
        {
            cachedPeBoxBarGraphics.Clear();
            cachedNeBoxBarGraphics.Clear();
        }

        /// <summary>
        /// 初始化方法
        /// </summary>
        protected void Init()
        {
            if (HeldPawn == null)
                return;

            var entity = HeldPawn.GetComp<CompAbnormality>();
            cachedEntity = entity ?? null;

            var studiable = HeldPawn.GetComp<CompAbnormalityStudiable>();
            compAbnormalityStudiable = studiable ?? null;
        }

        /// <summary>
        /// 释放平台内容
        /// </summary>
        public void EjectContents()
        {
            HeldPawn?.Drawer.renderer.SetAnimation(null);
            HeldPawn?.GetComp<CompAbnormalityHoldingPlatformTarget>()?.Notify_ReleasedFromPlatform();
            innerContainer.TryDropAll(base.Position, base.Map, ThingPlaceMode.Near);
        }

        #endregion 事件方法

        #region 工具方法

        /// <summary>
        /// 强制更新名称贴图
        /// </summary>
        protected void EntityNameUpdateForce()
        {
            if (EntityCached)
                cachedEntityNameGraphic = Util.GraphicUtil.EntityNamePlatformTopGraphic_Get(cachedEntity.parent.def.defName, true);
            //Log.Warning("Building_HoldingPlatform：检测到容器内异想体变化，强制更新名称贴图");
        }

        #endregion 工具方法

        #region UI

        /// <summary>
        /// Gizmo
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Verse.Gizmo> GetGizmos()
        {
            foreach (Verse.Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (CompWorkable != null)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "CommandToggleShowWorkableLabel".Translate(),
                    defaultDesc = "CommandToggleShowWorkableDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/WorkableUI"),
                    isActive = (() => this.CompWorkable.UIAllowed),
                    toggleAction = delegate ()
                    {
                        this.CompWorkable.UIAllowed = !this.CompWorkable.UIAllowed;
                    }
                };

                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "CommandAssignmentWorkTypeLabel".Translate();
                command_Action.defaultDesc = "CommandAssignmentWorkTypeDesc".Translate();
                command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/" + CurWorkType.ToString(), true);
                command_Action.onHover = () =>
                {
                    command_Action.defaultDesc = ("CommandAssignmentWorkTypeOnHoverDesc_" + CurWorkType.ToString()).Translate();
                };
                command_Action.action = () =>
                {
                    Find.WindowStack.Add(new Dialog_LC_AssignWorkType(this));
                };

                if (!EntityCached)
                    command_Action.Disable("LC_AssignmentGizmo_HoldingPlatform_NoAbnormalityOnPlatform_Desc".Translate());

                yield return command_Action;
            }
        }

        /// <inheritdoc />
        public override string GetInspectString()
        {
            inspectTextSB.Clear();

            Pawn heldPawn = HeldPawn;
            if (heldPawn != null)
            {
                TaggedString ts = "HoldingThing".Translate() + ": " + heldPawn.NameShortColored.CapitalizeFirst();
                bool flag = LCContainmentUtility.SafelyContains(this, heldPawn);
                if (!flag)
                {
                    float minimumStrength = LCContainmentUtility.GetMinimumContainmentStrength(heldPawn);
                    ts += " (" + "LC_HoldingPlatformRequiresStrength".Translate(Defs.StatDefOf.LC_MinimumContainmentStrength.Worker.ValueToString(minimumStrength, finalized: false)) + ")";
                }

                inspectTextSB.Append(ts.Colorize(flag ? Color.white : ColorLibrary.RedReadable));
            }
            else
            {
                inspectTextSB.Append("HoldingThing".Translate() + ": " + "Nothing".Translate().CapitalizeFirst());
            }

            //if (heldPawn != null && heldPawn.def.IsStudiable)
            //{
            //    string inspectStringExtraFor = heldPawn.def.label.Translate();
            //    if (!inspectStringExtraFor.NullOrEmpty())
            //    {
            //        text = text + "\n" + inspectStringExtraFor;
            //    }
            //}

            return inspectTextSB.ToString();
        }

        #endregion UI

        #region IThingHolderWithDrawnPawn

        /// <summary>获取 <c>HeldPawnDrawPos_Y</c>。</summary>
        public float HeldPawnDrawPos_Y => DrawPos.y + 0.03658537f;
        /// <summary>获取 <c>HeldPawnBodyAngle</c>。</summary>
        public float HeldPawnBodyAngle => base.Rotation.AsAngle;
        /// <summary>获取 <c>HeldPawnPosture</c>。</summary>
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

        #endregion

        #region IThingHolder

        /// <summary>执行 <c>GetChildHolders</c> 定义的操作。</summary>
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        /// <summary>执行 <c>GetDirectlyHeldThings</c> 定义的操作。</summary>
        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        #endregion

        #region IRoofCollapseAlert

        /// <summary>执行 <c>Notify_OnBeforeRoofCollapse</c> 定义的操作。</summary>
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

        #endregion

        #region ISearchableContents

        /// <summary>获取 <c>SearchableContents</c>。</summary>
        public ThingOwner SearchableContents => innerContainer;

        #endregion
    }
}
