using LCAnomalyCore.Util;
using LCAnomalyLibrary.Comp;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    public class Building_IndiPeBoxIndicator : Verse.Building
    {
        #region 字段

        private bool initalized;

        public int PeBoxCounter
        {
            get => peBoxCounter;
            set
            {
                if (value == peBoxCounter)
                    return;

                if (value < -1)
                {
                    peBoxCounter = -1;
                    return;
                }

                peBoxCounter = value;
                Log.Message($"独立PeBox计数器：设备值变更为：{peBoxCounter}");
            }
        }

        private int peBoxCounter = -1;

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
            if(PeBoxCounter >= 0)
            {
                GraphicUtil.IndiPeBoxIndicator_GetCachedTopGraphic()[PeBoxCounter]
                    .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
            }
            else
            {
                GraphicUtil.CachedTopGraphic_IndiPeBoxIndicator_NotAllowed
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
                    //如果收容平台不为空
                    var pawn = building_HoldingPlatform.HeldPawn;
                    LC_CompEntity compEntity;
                    if (pawn != null)
                    {
                        //如果是脑叶实体
                        compEntity = pawn.GetComp<LC_CompEntity>();
                        if (compEntity != null )
                        {
                            //绑定事件触发
                            var peBoxComp = compEntity.PeBoxComp;
                            if (peBoxComp != null)
                                compEntity.PeBoxComp.OnContentsChanged += UpdatePeBoxCounter;
                            else
                                peBoxCounter = -1;
                        }
                        else
                        {
                            peBoxCounter = -1;
                        }
                    }
                    else
                    {
                        peBoxCounter = -1;
                    }
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
                        var peBoxComp = compEntity.PeBoxComp;
                        if (peBoxComp != null)
                        {
                            PeBoxCounter = peBoxComp.CurAmountIndiPebox;
                        }
                        else
                        {
                            Log.Message($"独立PeBox计数器：该异想体不提供独立PeBox，不显示技术器");
                            PeBoxCounter = -1;
                        }
                    }
                    else
                    {
                        if (PeBoxCounter != -1)
                        {
                            Log.Message($"独立PeBox计数器：未找到实体组件，重置计数器");
                            PeBoxCounter = -1;
                        }
                    }
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
            if (stringBuilder.Length != 0)
            {
                stringBuilder.AppendLine();
            }

            stringBuilder.Append("IndiPeBoxIndicatorInspect".Translate());
            stringBuilder.Append($"：{PeBoxCounter}");
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

        #region 存储

        /// <summary>
        /// 和游戏内数据存储相关的方法
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref peBoxCounter, "peBoxCounter", -1);
        }

        #endregion
    }
}
