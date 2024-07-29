using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using LCAnomalyLibrary.Comp;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    /// <summary>
    /// 收容平台Building
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_HoldingPlatform : RimWorld.Building_HoldingPlatform
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

        #endregion

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

        #endregion

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
                    LCAnomalyLibrary.Util.Components.LC.TryGetAnomalyStatusSaved(cachedEntity.parent.def, out var saved);
                    return saved.IndiPeBoxAmount;
                }

                return 0;
            }
        }

        #endregion

        #endregion

        #region 生命周期

        /// <summary>
        /// 生成时方法
        /// </summary>
        /// <param name="map"></param>
        /// <param name="respawningAfterLoad"></param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            //加载存档后进行初始化和事件绑定
            if (respawningAfterLoad)
            {
                Init();
                innerContainer.OnContentsChanged += OnContentChanged;
            }
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
        public override void Tick()
        {
            //if (AllComps != null)
            //{
            //    int i = 0;
            //    for (int count = AllComps.Count; i < count; i++)
            //        AllComps[i].CompTick();
            //}

            //innerContainer.ThingOwnerTick();
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
                #region 左右下角可工作状态

                //如果分配工作者，就显示
                if (CompAssignable.AssignedPawns.Any() && CompWorkable.UIAllowed)
                {
                    CompWorkable?.AutoWorkGraphic.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }

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

                #endregion

                #region 逆卡巴拉计数器

                //显示不可用
                if (!EntityCached)
                {
                    GraphicUtil.CachedTopGraphic_QliphothIndicator_TopNull
                        ?.Draw(drawPosUpperCached, base.Rotation, this, 0f);

                    return;
                }
                //不大于10就显示对应数字
                if (QliphothCounter < 10)
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

                #endregion

                #region Pebox计数器

                //不大于100就显示对应数字
                if (PeBoxCounter < 100)
                {
                    GraphicUtil.IndiPeBoxIndicator_GetCachedTopGraphic()[PeBoxCounter]
                        ?.Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
                }
                //大于100就显示99+
                else
                {
                    GraphicUtil.CachedTopGraphic_IndiPeBoxIndicator_Max
                        ?.Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
                }

                #endregion

                #region 平台上的异常名字UI

                if (EntityCached)
                {
                    CachedEntityNameGraphic?.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }

                #endregion

                #region 可工作UI
                var category = HeldPawn.def.entityCodexEntry.category.defName;
                var graphicLevel = Util.GraphicUtil.LevelIndicator_GetCachedTopGraphic(category);
                graphicLevel.Draw(drawPosCached, base.Rotation, this, 0f);

                #endregion
            }
        }

        #endregion

        #region 事件方法

        /// <summary>
        /// 收容内容改变时触发的方法
        /// </summary>
        protected void OnContentChanged()
        {
            Init();
            EntityNameUpdateForce();
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
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 强制更新名称贴图
        /// </summary>
        protected void EntityNameUpdateForce()
        {
            cachedEntityNameGraphic = Util.GraphicUtil.EntityNamePlatformTopGraphic_Get(cachedEntity.parent.def.defName, true);
            Log.Warning("Building_HoldingPlatform：检测到容器内异想体变化，强制更新名称贴图");
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

        #endregion

        #region UI

        /// <summary>
        /// Gizmo
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
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
            }
        }

        #endregion
    }
}