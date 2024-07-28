using LCAnomalyCore.Comp;
using LCAnomalyCore.UI;
using LCAnomalyCore.Util;
using LCAnomalyLibrary.Comp;
using LCAnomalyLibrary.Comp.Abstract;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    [StaticConstructorOnStartup]
    public class Building_HoldingPlatform : RimWorld.Building_HoldingPlatform
    {
        protected CompWorkableUI CompWorkable => compWorkable ?? (compWorkable = GetComp<CompWorkableUI>());
        private CompWorkableUI compWorkable;

        private bool initalized;
        private LC_CompEntity cachedEntity;
        private bool entityCached => cachedEntity != null;

        private Vector3 altitudesCached = Altitudes.AltIncVect * 2f;

        protected Graphic CachedEntityNameGraphic => cachedEntityNameGraphic 
            ?? (cachedEntityNameGraphic = Util.GraphicUtil.EntityNamePlatformTopGraphic_Get(cachedEntity.parent.def.defName, true));
        private Graphic cachedEntityNameGraphic;

        public CompAssignableToPawn_LC_Entity CompAssignable => compAssignable
            ?? (compAssignable = GetComp<CompAssignableToPawn_LC_Entity>());
        private CompAssignableToPawn_LC_Entity compAssignable;

        /// <summary>
        /// 逆卡巴拉计数器值
        /// </summary>
        public int QliphothCounter
        {
            get
            {
                if (entityCached)
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
                if (entityCached)
                {
                    LCAnomalyLibrary.Util.Components.LC.TryGetAnomalyStatusSaved(cachedEntity.parent.def, out var saved);
                    return saved.IndiPeBoxAmount;
                }

                return 0;
            }
        }


        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                QliphothCounterInit();
                PeBoxCounterInit();

                initalized = true;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void QliphothCounterInit()
        {
            if (initalized)
            {
                return;
            }
            
            innerContainer.OnContentsChanged += QliphothCounterUpdate;

            QliphothCounterUpdate();
        }

        public override void Tick()
        {
            base.Tick();

            //每 250Tick 更新一次计数器
            if (this.IsHashIntervalTick(250))
            {
                QliphothCounterUpdate();
                PeBoxCounterUpdate();
            }
        }

        /// <summary>
        /// 更新逆卡巴拉计数器
        /// </summary>
        private void QliphothCounterUpdate()
        {

            var entity = HeldPawn?.TryGetComp<LC_CompEntity>();

            if (entity == null)
            {
                cachedEntity = null;
                return;
            }

            if (cachedEntity == entity)
                return;

            cachedEntity = entity;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void PeBoxCounterInit()
        {
            if (initalized)
            {
                return;
            }

            innerContainer.OnContentsChanged += PeBoxCounterUpdate;

            PeBoxCounterUpdate();
        }

        /// <summary>
        /// 更新PeBox计数器
        /// </summary>
        private void PeBoxCounterUpdate()
        {
            var entity = HeldPawn?.TryGetComp<LC_CompEntity>();

            if (entity != null)
            {
                if (cachedEntity != null && cachedEntity.parent.def == entity.parent.def)
                    return;

                var peBoxComp = entity.PeBoxComp;
                if (peBoxComp != null)
                {
                    cachedEntity = entity;
                    EntityNameUpdateForce();
                }
            }
            else
            {
                cachedEntity = null;
            }
        }

        private void EntityNameUpdateForce()
        {
            cachedEntityNameGraphic = Util.GraphicUtil.EntityNamePlatformTopGraphic_Get(cachedEntity.parent.def.defName, true);
            Log.Warning("Building_HoldingPlatform：检测到容器内异想体变化，强制更新名称贴图");
        }

        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();

            QliphothCounterUpdate();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            Vector3 drawPosCached = this.DrawPos + altitudesCached;
            Vector3 drawPosUpperCached = drawPosCached + Vector3.up;

            if (HeldPawn != null)
            {
                
                if (CompWorkable != null && CompWorkable.UIAllowed)
                {
                    var comp = HeldPawn.GetComp<CompStudiable>();
                    if (comp != null)
                    {
                        var studiable = !(comp.EverStudiable() && comp.TicksTilNextStudy > 0);
                        var graphic = studiable ? CompWorkable.AllowWorkGraphic : CompWorkable.NotAllowWorkGraphic;
                        graphic.Draw(drawPosUpperCached, base.Rotation, this, 0f);
                    }
                }

                #region 逆卡巴拉计数器

                //显示不可用
                if (!entityCached)
                {
                    GraphicUtil.CachedTopGraphic_QliphothIndicator_TopNull
                        .Draw(drawPosUpperCached, base.Rotation, this, 0f);

                    return;
                }
                //不大于10就显示对应数字
                if (QliphothCounter < 10)
                {
                    GraphicUtil.QliphothIndicator_GetCachedTopGraphic()[QliphothCounter]
                        .Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }
                //大于10就显示9+
                else
                {
                    GraphicUtil.CachedTopGraphic_QliphothIndicator_TopMax
                        .Draw(drawPosUpperCached, base.Rotation, this, 0f);
                }

                #endregion

                #region Pebox计数器

                //不大于100就显示对应数字
                if (PeBoxCounter < 100)
                {
                    GraphicUtil.IndiPeBoxIndicator_GetCachedTopGraphic()[PeBoxCounter]
                        .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
                }
                //大于100就显示99+
                else
                {
                    GraphicUtil.CachedTopGraphic_IndiPeBoxIndicator_Max
                        .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
                }

                #endregion

                #region 平台上的异常名字UI

                if (entityCached)
                {
                    //var graphic = Util.GraphicUtil.EntityNamePlatformTopGraphic_Get(cachedEntity.parent.def.defName);
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

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if(CompWorkable != null)
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
    }
}