using LCAnomalyLibrary.UI;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Building
{
    /// <summary>
    /// 脑啡肽发电机（建筑）（WIP）
    /// </summary>
    public class Building_EnkephalinGenerator : Verse.Building
    {
        [Unsaved(false)]
        private CompHeatPusherPowered heatPusher;

        private CompHeatPusherPowered HeatPusher => heatPusher ?? (heatPusher = GetComp<CompHeatPusherPowered>());

        /// <summary>
        /// 是否正在工作
        /// </summary>
        /// <returns></returns>
        public override bool IsWorking()
        {
            return HeatPusher.ShouldPushHeatNow;
        }
    }
}