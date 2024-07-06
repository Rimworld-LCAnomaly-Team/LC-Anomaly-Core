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

        public override IEnumerable<Verse.Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            //TODO 测试
            yield return new Command_Action
            {
                defaultLabel = "EntityCodex".Translate() + "...",
                defaultDesc = "EntityCodexGizmoTip".Translate(),
                icon = new CachedTexture("UI/Icons/OpenCodex").Texture,
                action = delegate ()
                {
                    Find.WindowStack.Add(new Dialog_LC_EntityCodex(null));
                }
            };
        }

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