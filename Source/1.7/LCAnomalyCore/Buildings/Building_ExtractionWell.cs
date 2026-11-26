using System.Collections.Generic;
using System.Text;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.UI;
using RimWorld;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.Buildings
{
    /// <summary>
    /// 早期“井”型异想体提取终端建筑。
    /// 玩家可在前期解锁后建造此终端，通过“三选一”界面提取各危险等级的异想体收容匣。
    /// </summary>
    public class Building_ExtractionWell : Building
    {
        private CompPowerTrader powerComp;
        private CompFlickable flickComp;
        private CompBreakdownable breakdownComp;

        /// <inheritdoc />
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            flickComp = GetComp<CompFlickable>();
            breakdownComp = GetComp<CompBreakdownable>();
        }

        /// <summary>
        /// 获取本井建筑当前是否通电且未损坏。
        /// </summary>
        public bool IsOperational =>
            (powerComp == null || powerComp.PowerOn)
            && (flickComp == null || flickComp.SwitchIsOn)
            && (breakdownComp == null || !breakdownComp.BrokenDown);

        /// <inheritdoc />
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Faction != Faction.OfPlayer || Map == null)
            {
                yield break;
            }

            MapComponent_LCExtraction mgr = MapComponent_LCExtraction.GetFor(Map);
            Command_Action openCmd = new Command_Action
            {
                defaultLabel = "LC_Extract_GizmoLabel".Translate(),
                defaultDesc = "LC_Extract_GizmoDesc".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Extraction/Extract_Unit_Chassis", false)
                       ?? def?.uiIcon,
                action = delegate
                {
                    if (Find.WindowStack.IsOpen<Dialog_LC_ExtractAbnormality>())
                    {
                        return;
                    }

                    (Defs.SoundDefOf.LC_Sound_Extraction_Start ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
                    Find.WindowStack.Add(new Dialog_LC_ExtractAbnormality(Map, this));
                }
            };

            if (!IsOperational && (mgr == null || !mgr.HasPendingDraw))
            {
                openCmd.Disable("LC_Extract_NeedPower".Translate());
            }

            yield return openCmd;

            if (Prefs.DevMode && mgr != null && mgr.CooldownTicksRemaining > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Reset Extraction Cooldown",
                    defaultDesc = "Immediately clear the abnormality extraction cooldown on this map.",
                    action = delegate
                    {
                        mgr.DebugResetCooldown();
                    }
                };
            }
        }

        /// <inheritdoc />
        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            string baseText = base.GetInspectString();
            if (!baseText.NullOrEmpty())
            {
                sb.AppendLine(baseText);
            }

            MapComponent_LCExtraction mgr = MapComponent_LCExtraction.GetFor(Map);
            if (mgr == null)
            {
                return sb.ToString().TrimEndNewlines();
            }

            if (mgr.HasPendingDraw)
            {
                sb.AppendLine("LC_Extract_InspectPending".Translate());
            }
            else if (mgr.CooldownTicksRemaining > 0)
            {
                sb.AppendLine("LC_Extract_InspectCooldown".Translate(mgr.CooldownTicksRemaining.ToStringTicksToPeriod()));
            }
            else if (mgr.IsFreeCharterAvailable)
            {
                sb.AppendLine("LC_Extract_InspectFreeCharter".Translate());
            }
            else
            {
                sb.AppendLine("LC_Extract_InspectReady".Translate(mgr.TotalExtractions));
            }

            return sb.ToString().TrimEndNewlines();
        }
    }
}
