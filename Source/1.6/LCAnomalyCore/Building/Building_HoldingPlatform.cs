﻿using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Interface;
using LCAnomalyCore.UI;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.Building
{
    /// <summary>
    /// 收容平台Building
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_HoldingPlatform : RimWorld.Building_HoldingPlatform, IHoldingPlatformWorkTypeSelectable
    {
        #region 变量

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

        public LC_CompStudiable CompStudiable
        {
            get
            {
                if (HeldPawn != null)
                {
                    compStudiable ??= HeldPawn.GetComp<LC_CompStudiable>();
                }

                return compStudiable;
            }
        }

        private LC_CompStudiable compStudiable;

        private Sustainer workingSustainer;

        #endregion 组件

        #region 缓存

        /// <summary>
        /// 是否存在缓存对象
        /// </summary>
        protected bool EntityCached => cachedEntity != null;

        private LC_CompEntity cachedEntity;

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
                    Util.Components.LC.TryGetAnomalyStatusSaved(cachedEntity.parent.def, out var saved);
                    return saved.IndiPeBoxAmount;
                }

                return 0;
            }
        }

        public EAnomalyWorkType CurWorkType
        {
            get => curWorkType;
            set => curWorkType = value;
        }

        protected EAnomalyWorkType curWorkType = EAnomalyWorkType.Instinct;

        #endregion 字段

        #endregion 变量

        #region 生命周期

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
            //if (AllComps != null)
            //{
            //    int i = 0;
            //    for (int count = AllComps.Count; i < count; i++)
            //        AllComps[i].CompTick();
            //}

            //innerContainer.ThingOwnerTick();

            if (innerContainer.Count > 0)
            {
                var entitiy = innerContainer[0] as LC_EntityBasePawn;
                entitiy?.TickHolded();
            }
        }

        /// <summary>
        /// 每帧绘制
        /// </summary>
        /// <param name="drawLoc"></param>
        /// <param name="flip"></param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DrawAvoidParent(drawLoc, flip);

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
                        var comp = HeldPawn.GetComp<CompStudiable>();
                        if (comp != null)
                        {
                            var studiable = !(comp.EverStudiable() && comp.TicksTilNextStudy > 0);
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

                #region 可工作UI

                var category = HeldPawn.def.entityCodexEntry.category.defName;
                var graphicLevel = GraphicUtil.LevelIndicator_GetCachedTopGraphic(category);
                graphicLevel.Draw(drawPosCached, base.Rotation, this, 0f);

                #endregion 可工作UI

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

        public void Notify_StudyInterval(CompPawnStatus studier)
        {
            bool success = cachedEntity.Notify_StudyInterval(studier, CurWorkType);

            if (success)
                cachedPeBoxBarGraphics.Add(GraphicUtil.CachedTopGraphic_BoxBarUnit_Get("PE", cachedEntity.PeBoxComp.Props.amountProdueMax));
            else
                cachedNeBoxBarGraphics.Add(GraphicUtil.CachedTopGraphic_BoxBarUnit_Get("NE", cachedEntity.PeBoxComp.Props.amountProdueMax));
        }

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

            var entity = HeldPawn.GetComp<LC_CompEntity>();
            cachedEntity = entity ?? null;

            var studiable = HeldPawn.GetComp<LC_CompStudiable>();
            compStudiable = studiable ?? null;
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

        /// <summary>
        /// 绕开基类平台的绘制方法
        /// </summary>
        /// <param name="drawLoc"></param>
        /// <param name="flip"></param>
        protected void DrawAvoidParent(Vector3 drawLoc, bool flip)
        {
            if (def.drawerType == DrawerType.RealtimeOnly || !Spawned)
            {
                Graphic.Draw(drawLoc, flip ? Rotation.Opposite : Rotation, this);
            }

            SilhouetteUtility.DrawGraphicSilhouette(this, drawLoc);

            Comps_PostDraw();
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
                    command_Action.Disable("LC_NoAbnormalityOnPlatformDesc".Translate());

                yield return command_Action;
            }
        }

        public override string GetInspectString()
        {
            string text = "\n";

            Pawn heldPawn = HeldPawn;
            if (heldPawn != null)
            {
                TaggedString ts = "HoldingThing".Translate() + ": " + heldPawn.NameShortColored.CapitalizeFirst();
                bool flag = this.SafelyContains(heldPawn);
                if (!flag)
                {
                    ts += " (" + "HoldingPlatformRequiresStrength".Translate(StatDefOf.MinimumContainmentStrength.Worker.ValueToString(heldPawn.GetStatValue(StatDefOf.MinimumContainmentStrength), finalized: false)) + ")";
                }

                text += ts.Colorize(flag ? Color.white : ColorLibrary.RedReadable);
            }
            else
            {
                text += "HoldingThing".Translate() + ": " + "Nothing".Translate().CapitalizeFirst();
            }

            //if (heldPawn != null && heldPawn.def.IsStudiable)
            //{
            //    string inspectStringExtraFor = heldPawn.def.label.Translate();
            //    if (!inspectStringExtraFor.NullOrEmpty())
            //    {
            //        text = text + "\n" + inspectStringExtraFor;
            //    }
            //}

            return text;
        }

        #endregion UI
    }
}