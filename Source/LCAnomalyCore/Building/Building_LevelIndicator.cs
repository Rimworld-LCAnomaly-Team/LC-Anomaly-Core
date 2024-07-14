using LCAnomalyCore.Util;
using LCAnomalyLibrary.Comp;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    public class Building_LevelIndicator : Verse.Building
    {
        #region 字段

        private bool initalized;

        /// <summary>
        /// 设施Comp
        /// </summary>
        public CompFacility FacilityComp => facilityComp ?? (facilityComp = GetComp<CompFacility>());

        private CompFacility facilityComp;

        /// <summary>
        /// 已连接的平台（理论上只能有一个）
        /// </summary>
        public List<Thing> Platforms => FacilityComp.LinkedBuildings;

        private ThingDef cachedEntityDef;
        private bool Allowed => cachedEntityDef != null;
        private int LevelIndex
        {
            get
            {
                var typeS = cachedEntityDef.entityCodexEntry.category.defName;

                switch (typeS) 
                {
                    case "ZAYIN":
                        return 0;
                    case "TETH":
                        return 1;
                    case "HE":
                        return 2;
                    case "WAW":
                        return 3;
                    case "ALEPH":
                        return 4;
                    default:
                        return 0;
                }
            }
        }

        #endregion 字段

        #region 生命周期

        /// <summary>
        /// 开始生成时的方法
        /// </summary>
        /// <param name="map">地图</param>
        /// <param name="respawningAfterLoad">加载后重新生成</param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 移除时的方法
        /// </summary>
        /// <param name="mode">销毁模式</param>
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);

            initalized = false;
        }

        /// <summary>
        /// 绘制方法
        /// </summary>
        /// <param name="drawLoc">位置</param>
        /// <param name="flip">是否翻转</param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (!initalized)
            {
                Initialize();
            }
            
            if (Allowed)
                GraphicUtil.LevelIndicator_GetCachedTopGraphic()[LevelIndex]
                    .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 初始化
        /// </summary>
        private void Initialize()
        {
            if (initalized)
            {
                return;
            }

            initalized = true;

            foreach (Thing platform in Platforms)
            {
                if (platform is Building_HoldingPlatform building_HoldingPlatform)
                {
                    building_HoldingPlatform.holdingOwner.OnContentsChanged += UpdateLevelIndicator;
                }
            }

            UpdateLevelIndicator();
        }

        /// <summary>
        /// 更新等级显示
        /// </summary>
        private void UpdateLevelIndicator()
        {
            foreach (Thing thing in Platforms)
            {
                var platform = thing as Building_HoldingPlatform;
                if (platform != null)
                {
                    var compEntity = platform.HeldPawn.TryGetComp<LC_CompEntity>();
                    if (compEntity != null)
                    {
                        if(cachedEntityDef != null && compEntity.parent.def == cachedEntityDef)
                        {
                            return;
                        }

                        cachedEntityDef = compEntity.parent.def;
                    }
                    else
                    {
                        cachedEntityDef = null;
                    }
                }
                else
                {
                    cachedEntityDef = null;
                }
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 热重载通知
        /// </summary>
        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();
            UpdateLevelIndicator();
        }

        #endregion 事件
    }
}
