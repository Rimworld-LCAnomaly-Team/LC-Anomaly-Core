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
    /// 独立PeBox指示器建筑
    /// </summary>
    public class Building_IndiPeBoxIndicator : Verse.Building
    {

        #region 字段

        private bool initalized;

        /// <summary>
        /// 独立PeBox数量
        /// </summary>
        public int PeBoxCounter
        {
            get
            {
                if (allowed)
                {
                    LCAnomalyLibrary.Util.Components.LC.TryGetAnomalyStatusSaved(cachedEntityDef, out var saved);
                    return saved.IndiPeBoxAmount;
                }

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

        private ThingDef cachedEntityDef;
        private bool allowed => cachedEntityDef != null;

        /// <summary>
        /// 已连接的平台（理论上只能有一个）
        /// </summary>
        public List<Thing> Platforms => FacilityComp.LinkedBuildings;

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
                UpdatePeBoxCounter();
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

            //不可用显示
            if (!allowed)
            {
                GraphicUtil.CachedTopGraphic_IndiPeBoxIndicator_NotAllowed
                    .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
                return;
            }

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
                    building_HoldingPlatform.holdingOwner.OnContentsChanged += UpdatePeBoxCounter;
                }
            }

            UpdatePeBoxCounter();
        }

        /// <summary>
        /// 更新PeBox计数器
        /// </summary>
        private void UpdatePeBoxCounter()
        {
            foreach (Thing thing in Platforms)
            {
                var platform = thing as Building_HoldingPlatform;
                if (platform != null)
                {
                    var compEntity = platform.HeldPawn.TryGetComp<LC_CompEntity>();
                    if (compEntity != null)
                    {
                        if (cachedEntityDef != null && cachedEntityDef == compEntity.parent.def)
                            return;

                        var peBoxComp = compEntity.PeBoxComp;
                        if (peBoxComp != null)
                        {
                            cachedEntityDef = compEntity.parent.def;
                        }
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

                stringBuilder.Append("IndiPeBoxIndicatorInspectText".Translate());
                stringBuilder.Append($"：{PeBoxCounter}");
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region 事件

        /// <summary>
        /// 热重载通知
        /// </summary>
        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();
            UpdatePeBoxCounter();
        }

        #endregion 事件
    }
}
