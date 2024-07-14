using LCAnomalyCore.Util;
using LCAnomalyLibrary.Comp;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    /// <summary>
    /// 逆卡巴拉计数器（建筑）
    /// </summary>
    public class Building_QliphothIndicator : Verse.Building
    {
        #region 字段

        private bool initalized;

        /// <summary>
        /// 逆卡巴拉计数器值
        /// </summary>
        public int QliphothCounter
        {
            get
            {
                if(allowed)
                    return cachedEntity.QliphothCountCurrent;

                return 0;
            }
        }

        /// <summary>
        /// 设施Comp
        /// </summary>
        public CompFacility FacilityComp => facilityComp ?? (facilityComp = GetComp<CompFacility>());

        private CompFacility facilityComp;

        /// <summary>
        /// 能源Comp
        /// </summary>
        public CompPowerTrader Power => powerComp ?? (powerComp = GetComp<CompPowerTrader>());

        private CompPowerTrader powerComp;

        /// <summary>
        /// 已连接的平台（理论上只能有一个）
        /// </summary>
        public List<Thing> Platforms => FacilityComp.LinkedBuildings;

        private LC_CompEntity cachedEntity;
        private bool allowed => cachedEntity != null;

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
        /// 每Tick调用
        /// </summary>
        public override void Tick()
        {
            base.Tick();

            //每 250Tick 更新一次计数器
            if (this.IsHashIntervalTick(250))
            {
                UpdateQliphothCounter();
            }
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

            //显示不可用
            if (!allowed)
            {
                GraphicUtil.CachedTopGraphic_QliphothIndicator_NotAllowed
                    .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);

                return;
            }

            //不大于10就显示对应数字
            if (QliphothCounter < 10)
            {
                GraphicUtil.QliphothIndicator_GetCachedTopGraphic()[QliphothCounter]
                    .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
            }
            //大于10就显示9+
            else
            {
                GraphicUtil.CachedTopGraphic_QliphothIndicator_Max
                    .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
            }
        }

        #endregion 生命周期

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
                    building_HoldingPlatform.innerContainer.OnContentsChanged += UpdateQliphothCounter;
                }
            }

            UpdateQliphothCounter();
        }

        /// <summary>
        /// 更新逆卡巴拉计数器
        /// </summary>
        private void UpdateQliphothCounter()
        {
            foreach (Thing thing in Platforms)
            {
                if (thing is Building_HoldingPlatform platform)
                {
                    var entity = platform.HeldPawn.TryGetComp<LC_CompEntity>();

                    if (entity == null)
                    {
                        cachedEntity = null;
                        return;
                    }

                    if (cachedEntity == entity)
                        return;

                    cachedEntity = entity;
                }
                else
                {
                    cachedEntity = null;
                }
            }
        }

        #endregion 工具方法

        #region UI

        /// <summary>
        /// Inspect面板信息
        /// </summary>
        /// <returns></returns>
        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());

            if (allowed)
            {
                if (stringBuilder.Length != 0)
                    stringBuilder.AppendLine();

                stringBuilder.Append("QliphothCounterInspect".Translate());
                stringBuilder.Append($"：{cachedEntity.QliphothCountCurrent}");
            }

            return stringBuilder.ToString();
        }

        #endregion UI

        #region 事件

        /// <summary>
        /// 热重载通知
        /// </summary>
        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();
            UpdateQliphothCounter();
        }

        #endregion 事件
    }
}