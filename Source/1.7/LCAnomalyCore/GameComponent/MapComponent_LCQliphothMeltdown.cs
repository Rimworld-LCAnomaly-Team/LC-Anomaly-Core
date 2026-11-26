using System;
using System.Collections.Generic;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Settings;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.GameComponent
{
    /// <summary>
    /// 地图级逆卡巴拉熔毁（Qliphoth Meltdown）过载波次管理器：
    /// 追踪当日工作累计进度并在达到阈值时触发波次警报，同时绘制屏幕顶部实时熔毁监控条。
    /// </summary>
    public class MapComponent_LCQliphothMeltdown : MapComponent
    {
        /// <summary>触发熔毁波次时派发（参数：地图、当前熔毁等级、本波次受影响终端列表）。</summary>
        public static event Action<Map, int, List<Building_AbnormalityContainmentTerminal>> OnMeltdownWaveTriggered;

        /// <summary>由外围模组（如 Ordeals）注入下一阶段考验里程碑预报文案（参数：地图、当前熔毁等级）。</summary>
        public static Func<Map, int, string> NextMilestonePreviewProvider;

        private int meltdownLevel;
        private int worksSinceLastWave;
        private int lastShiftDay = -1;

        public MapComponent_LCQliphothMeltdown(Map map) : base(map)
        {
        }

        /// <summary>当前地图今日已推进的熔毁等级（每波 +1，跨日自动重置）。</summary>
        public int MeltdownLevel => meltdownLevel;

        /// <summary>自上次熔毁波次以来已完成的工作次数。</summary>
        public int WorksSinceLastWave => worksSinceLastWave;

        /// <summary>每次触发熔毁波次所需的完成工作次数。</summary>
        public int WorksPerWave => Mathf.Clamp(Setting_LCAnomalyCore_Main.Settings?.QliphothMeltdown_WorksPerWave ?? 6, 2, 20);

        /// <summary>当前地图上是否存在任意已收容活体异想体的终端。</summary>
        public bool HasOccupiedContainmentRooms
        {
            get
            {
                List<Thing> terminals = GetTerminals();
                for (int i = 0; i < terminals.Count; i++)
                {
                    if (terminals[i] is Building_AbnormalityContainmentTerminal term
                        && term.Spawned
                        && term.Occupied
                        && !term.IsHoldingTool)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>当前正在熔毁倒计时中的收容室数量。</summary>
        public int ActiveMeltdownCount
        {
            get
            {
                int count = 0;
                List<Thing> terminals = GetTerminals();
                for (int i = 0; i < terminals.Count; i++)
                {
                    if (terminals[i] is Building_AbnormalityContainmentTerminal term && term.IsInMeltdown)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>返回当前剩余倒计时最短的熔毁收容室终端（无熔毁时返回 null）。</summary>
        public Building_AbnormalityContainmentTerminal GetMostUrgentMeltingTerminal()
        {
            Building_AbnormalityContainmentTerminal urgent = null;
            int minTicks = int.MaxValue;
            List<Thing> terminals = GetTerminals();
            for (int i = 0; i < terminals.Count; i++)
            {
                if (terminals[i] is Building_AbnormalityContainmentTerminal term
                    && term.IsInMeltdown
                    && term.MeltdownTicksRemaining < minTicks)
                {
                    minTicks = term.MeltdownTicksRemaining;
                    urgent = term;
                }
            }
            return urgent;
        }

        /// <summary>收容工作完成结算后记录一次设施负荷，达到阈值即触发新一波逆卡巴拉熔毁。</summary>
        public void Notify_ContainmentWorkCompleted(Building_AbnormalityContainmentTerminal completedTerminal)
        {
            if (Setting_LCAnomalyCore_Main.Settings?.If_EnableQliphothMeltdown != true)
            {
                return;
            }

            if (map == null || completedTerminal == null || !completedTerminal.Spawned)
            {
                return;
            }

            SyncDailyShiftIfNeeded();

            worksSinceLastWave++;
            if (worksSinceLastWave >= WorksPerWave)
            {
                worksSinceLastWave = 0;
                TriggerMeltdownWave();
            }
        }

        /// <summary>立即在当前地图随机选取符合条件的收容室触发一次逆卡巴拉熔毁波次，返回实际进入熔毁的房间数。</summary>
        public int TriggerMeltdownWave(int? overrideRoomCount = null, int? overrideDurationTicks = null)
        {
            if (map == null)
            {
                return 0;
            }

            List<Building_AbnormalityContainmentTerminal> eligible = new List<Building_AbnormalityContainmentTerminal>();
            List<Thing> terminals = GetTerminals();
            for (int i = 0; i < terminals.Count; i++)
            {
                if (terminals[i] is Building_AbnormalityContainmentTerminal term && term.CanTriggerMeltdownNow())
                {
                    eligible.Add(term);
                }
            }

            if (eligible.Count == 0)
            {
                return 0;
            }

            SyncDailyShiftIfNeeded();
            meltdownLevel++;

            int desiredCount = overrideRoomCount
                ?? Mathf.Clamp(1 + (meltdownLevel - 1) / 2, 1, Mathf.Max(1, Mathf.CeilToInt(eligible.Count * 0.6f)));
            int count = Mathf.Clamp(desiredCount, 1, eligible.Count);
            int durationTicks = overrideDurationTicks
                ?? (Mathf.Clamp(Setting_LCAnomalyCore_Main.Settings?.QliphothMeltdown_CountdownSeconds ?? 60, 20, 180) * 60);

            eligible.Shuffle();
            List<Building_AbnormalityContainmentTerminal> triggered = new List<Building_AbnormalityContainmentTerminal>(count);
            for (int i = 0; i < count; i++)
            {
                if (eligible[i].TriggerMeltdown(durationTicks))
                {
                    triggered.Add(eligible[i]);
                }
            }

            if (triggered.Count == 0)
            {
                meltdownLevel = Mathf.Max(0, meltdownLevel - 1);
                return 0;
            }

            Defs.SoundDefOf.LC_Sound_QliphothMeltdown_Alert?.PlayOneShotOnCamera(map);
            Messages.Message(
                "LC_Meltdown_WaveTriggeredMessage".Translate(ToRoman(meltdownLevel), triggered.Count),
                new LookTargets(triggered),
                MessageTypeDefOf.ThreatSmall);

            OnMeltdownWaveTriggered?.Invoke(map, meltdownLevel, triggered);
            return triggered.Count;
        }

        /// <summary>清空当前地图所有正在进行的熔毁并可选重置波次计数。</summary>
        public void ResetAllMeltdowns(bool resetWaveProgress = false)
        {
            List<Thing> terminals = GetTerminals();
            for (int i = 0; i < terminals.Count; i++)
            {
                if (terminals[i] is Building_AbnormalityContainmentTerminal term && term.IsInMeltdown)
                {
                    term.ClearMeltdown(resolvedByWork: false);
                }
            }

            if (resetWaveProgress)
            {
                meltdownLevel = 0;
                worksSinceLastWave = 0;
            }
        }

        /// <summary>当前地图是否正在屏幕顶部绘制熔毁监控条（供下方快捷栏避让排版）。</summary>
        public bool IsHudVisible
        {
            get
            {
                Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
                return Current.ProgramState == ProgramState.Playing
                    && Find.CurrentMap == map
                    && Find.ScreenshotModeHandler != null
                    && !Find.ScreenshotModeHandler.Active
                    && settings != null
                    && settings.If_EnableQliphothMeltdown
                    && settings.If_ShowQliphothMeltdownHud
                    && (ActiveMeltdownCount > 0 || HasOccupiedContainmentRooms);
            }
        }

        /// <inheritdoc />
        public override void MapComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap != map
                || Find.ScreenshotModeHandler.Active)
            {
                return;
            }

            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            if (settings == null || !settings.If_EnableQliphothMeltdown || !settings.If_ShowQliphothMeltdownHud)
            {
                return;
            }

            Building_AbnormalityContainmentTerminal urgent = GetMostUrgentMeltingTerminal();
            int activeCount = urgent != null ? ActiveMeltdownCount : 0;
            if (activeCount == 0 && !HasOccupiedContainmentRooms)
            {
                return;
            }

            string milestonePreview = NextMilestonePreviewProvider?.Invoke(map, meltdownLevel);

            float panelW = 216f;
            float panelH = 36f;
            Rect panelRect = new Rect(166f, 8f, panelW, panelH);

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 7.5f);
                Color bgColor = activeCount > 0
                    ? Color.Lerp(new Color(0.16f, 0.05f, 0.06f, 0.90f), new Color(0.28f, 0.07f, 0.07f, 0.94f), pulse)
                    : new Color(0.06f, 0.08f, 0.11f, 0.86f);
                Color borderColor = activeCount > 0
                    ? Color.Lerp(new Color(0.95f, 0.30f, 0.22f), new Color(1.0f, 0.72f, 0.25f), pulse)
                    : new Color(0.26f, 0.32f, 0.38f, 0.92f);
                Color accentColor = activeCount > 0
                    ? new Color(1.0f, 0.34f, 0.24f)
                    : new Color(0.94f, 0.76f, 0.28f);

                Widgets.DrawBoxSolid(panelRect, bgColor);
                GUI.color = borderColor;
                Widgets.DrawBox(panelRect, 1);
                Widgets.DrawBoxSolid(new Rect(panelRect.x + 1f, panelRect.y + 2f, 4f, panelRect.height - 4f), accentColor);

                Rect topRect = new Rect(panelRect.x + 9f, panelRect.y + 2f, panelRect.width - 14f, 17f);
                Text.Font = GameFont.Tiny;
                if (activeCount > 0)
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = Color.Lerp(new Color(1.0f, 0.86f, 0.78f), Color.white, pulse * 0.4f);
                    string alertText = "LC_Meltdown_TopHudAlert".Translate(
                        activeCount,
                        Mathf.CeilToInt(urgent.MeltdownSecondsRemaining));
                    Widgets.LabelFit(topRect, alertText);
                }
                else
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = new Color(0.95f, 0.82f, 0.38f);
                    Widgets.LabelFit(
                        new Rect(topRect.x, topRect.y, topRect.width - 54f, topRect.height),
                        "LC_Meltdown_TopHudLevel".Translate(ToRoman(meltdownLevel)));

                    Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = new Color(0.75f, 0.86f, 0.95f);
                    Widgets.LabelFit(
                        new Rect(topRect.xMax - 52f, topRect.y, 52f, topRect.height),
                        $"{worksSinceLastWave}/{WorksPerWave}");
                }

                // Bottom segmented progress track
                Rect trackRect = new Rect(panelRect.x + 9f, panelRect.y + 22f, panelRect.width - 15f, 9f);
                if (activeCount > 0)
                {
                    Widgets.DrawBoxSolid(trackRect, new Color(0.12f, 0.04f, 0.05f, 0.95f));
                    float ratio = urgent.MeltdownRemainingRatio;
                    if (ratio > 0f)
                    {
                        Widgets.DrawBoxSolid(
                            new Rect(trackRect.x + 1f, trackRect.y + 1f, Mathf.Max(2f, (trackRect.width - 2f) * ratio), trackRect.height - 2f),
                            new Color(0.96f, 0.32f, 0.22f, 0.96f));
                    }

                    if (Widgets.ButtonInvisible(panelRect))
                    {
                        CameraJumper.TryJumpAndSelect(urgent);
                    }
                }
                else
                {
                    int segs = Mathf.Max(1, WorksPerWave);
                    float gap = segs > 10 ? 1.5f : 2.5f;
                    float segW = (trackRect.width - gap * (segs - 1)) / segs;
                    for (int s = 0; s < segs; s++)
                    {
                        Rect segRect = new Rect(trackRect.x + s * (segW + gap), trackRect.y, segW, trackRect.height);
                        Color segColor = s < worksSinceLastWave
                            ? new Color(0.96f, 0.74f, 0.24f, 0.95f)
                            : new Color(0.15f, 0.18f, 0.22f, 0.90f);
                        Widgets.DrawBoxSolid(segRect, segColor);
                    }
                }

                string baseTip = activeCount > 0
                    ? "LC_Meltdown_TopHudAlertTip".Translate().ToString()
                    : "LC_Meltdown_TopHudTip".Translate(WorksPerWave).ToString();
                if (!milestonePreview.NullOrEmpty())
                {
                    baseTip += "\n" + milestonePreview;
                }

                TooltipHandler.TipRegion(panelRect, baseTip);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        /// <inheritdoc />
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref meltdownLevel, "lcMeltdownLevel", 0);
            Scribe_Values.Look(ref worksSinceLastWave, "lcWorksSinceLastWave", 0);
            Scribe_Values.Look(ref lastShiftDay, "lcMeltdownLastShiftDay", -1);
        }

        private List<Thing> GetTerminals()
        {
            ThingDef def = Defs.ThingDefOf.LC_ContainmentTerminal;
            if (map?.listerThings == null || def == null)
            {
                return EmptyThingList;
            }
            return map.listerThings.ThingsOfDef(def);
        }

        private void SyncDailyShiftIfNeeded()
        {
            if (Find.TickManager == null)
            {
                return;
            }

            int today = GenDate.DaysPassed;
            if (lastShiftDay != today)
            {
                lastShiftDay = today;
                meltdownLevel = 0;
                worksSinceLastWave = 0;
            }
        }

        private static string ToRoman(int number)
        {
            if (number <= 0) return "0";
            switch (number)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                case 7: return "VII";
                case 8: return "VIII";
                case 9: return "IX";
                case 10: return "X";
                default: return number.ToString();
            }
        }

        private static readonly List<Thing> EmptyThingList = new List<Thing>(0);
    }
}
