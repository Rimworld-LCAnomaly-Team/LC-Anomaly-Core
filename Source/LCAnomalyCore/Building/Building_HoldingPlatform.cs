using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using LCAnomalyLibrary.Comp;
using RimWorld;
using System.Collections.Generic;
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


        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                QliphothCounterInit();
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

            initalized = true;
            
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
                        graphic.Draw(drawPosCached, base.Rotation, this, 0f);
                    }
                }

                #region 逆卡巴拉计数器

                //底部贴图
                GraphicUtil.CachedTopGraphic_QliphothIndicator_Bottom
                    .Draw(drawPosCached, base.Rotation, this, 0f);

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