using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Defs;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 设施收容总控台（MainTab）：一屏集中监控收容室状态、熔毁波次、出逃威胁与 LC 员工美德配装。
    /// </summary>
    public class MainTabWindow_LCFacility : MainTabWindow
    {
        private enum FacilityTab
        {
            Rooms,
            Employees
        }

        private enum RoomFilter
        {
            All,
            Melting,
            Working,
            Idle,
            Tools
        }

        private enum EmployeeFilter
        {
            All,
            Assigned,
            Unassigned
        }

        private FacilityTab activeTab = FacilityTab.Rooms;
        private RoomFilter roomFilter = RoomFilter.All;
        private EmployeeFilter employeeFilter = EmployeeFilter.All;
        private Vector2 roomsScrollPos;
        private Vector2 employeesScrollPos;

        public override Vector2 InitialSize => new Vector2(1040f, 680f);

        public override void DoWindowContents(Rect inRect)
        {
            Map map = Find.CurrentMap;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;

            try
            {
                DrawTopHeader(new Rect(inRect.x, inRect.y, inRect.width, 54f), map);

                Rect tabArea = new Rect(inRect.x, inRect.y + 92f, inRect.width, inRect.height - 92f);
                List<TabRecord> tabs = new List<TabRecord>
                {
                    new TabRecord("LC_FacilityTab_Rooms".Translate(), () => activeTab = FacilityTab.Rooms, activeTab == FacilityTab.Rooms),
                    new TabRecord("LC_FacilityTab_Employees".Translate(), () => activeTab = FacilityTab.Employees, activeTab == FacilityTab.Employees)
                };

                Widgets.DrawMenuSection(tabArea);
                TabDrawer.DrawTabs(tabArea, tabs);

                Rect contentRect = tabArea.ContractedBy(10f);
                if (map == null)
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.gray;
                    Widgets.Label(contentRect, "LC_FacilityTab_NoMap".Translate());
                    return;
                }

                if (activeTab == FacilityTab.Rooms)
                {
                    DrawRoomsTab(contentRect, map);
                }
                else
                {
                    DrawEmployeesTab(contentRect, map);
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        private void DrawTopHeader(Rect rect, Map map)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.10f, 0.13f, 0.94f));
            GUI.color = new Color(0.26f, 0.34f, 0.42f, 0.90f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 4f, 250f, 28f), "LC_FacilityTab_Title".Translate());

            if (map == null)
            {
                return;
            }

            List<Building_AbnormalityContainmentTerminal> occupiedRooms = GetOccupiedTerminals(map);
            List<CompAbnormality> escaped = GetEscapedAbnormalities(map);
            MapComponent_LCQliphothMeltdown meltdownMgr = map.GetComponent<MapComponent_LCQliphothMeltdown>();
            int activeMeltdowns = meltdownMgr?.ActiveMeltdownCount ?? 0;

            float pillX = rect.x + 12f;
            float pillY = rect.y + 30f;
            Text.Font = GameFont.Tiny;

            DrawKpiChip(new Rect(pillX, pillY, 130f, 18f),
                "LC_FacilityTab_KpiContained".Translate(occupiedRooms.Count),
                new Color(0.45f, 0.84f, 1.0f));
            pillX += 136f;

            Rect meltdownChip = new Rect(pillX, pillY, 130f, 18f);
            Color meltdownColor = activeMeltdowns > 0 ? new Color(1.0f, 0.36f, 0.28f) : new Color(0.65f, 0.72f, 0.78f);
            DrawKpiChip(meltdownChip, "LC_FacilityTab_KpiMeltdown".Translate(activeMeltdowns), meltdownColor);
            if (activeMeltdowns > 0 && Widgets.ButtonInvisible(meltdownChip))
            {
                Building_AbnormalityContainmentTerminal urgent = meltdownMgr?.GetMostUrgentMeltingTerminal();
                if (urgent != null)
                {
                    JumpAndSelect(urgent);
                }
            }
            pillX += 136f;

            Rect escapedChip = new Rect(pillX, pillY, 130f, 18f);
            Color escapedColor = escaped.Count > 0 ? new Color(1.0f, 0.28f, 0.38f) : new Color(0.55f, 0.85f, 0.62f);
            DrawKpiChip(escapedChip, "LC_FacilityTab_KpiEscaped".Translate(escaped.Count), escapedColor);
            if (escaped.Count > 0 && Widgets.ButtonInvisible(escapedChip) && escaped[0]?.parent != null)
            {
                JumpAndSelect(escaped[0].parent);
            }
            pillX += 136f;

            if (meltdownMgr != null)
            {
                string waveText = "LC_FacilityTab_KpiWave".Translate(
                    meltdownMgr.MeltdownLevel,
                    meltdownMgr.WorksSinceLastWave,
                    meltdownMgr.WorksPerWave).ToString();
                string milestone = MapComponent_LCQliphothMeltdown.NextMilestonePreviewProvider?.Invoke(map, meltdownMgr.MeltdownLevel);
                Rect waveRect = new Rect(pillX, pillY, 210f, 18f);
                DrawKpiChip(waveRect, waveText, new Color(0.95f, 0.80f, 0.34f));
                if (!milestone.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(waveRect, milestone);
                }
                pillX += 216f;
            }

            int maxBullets = ManagerBulletUtility.GetMaxBulletCharges(map);
            if (maxBullets > 0 && GameComponent_LC.Instance != null)
            {
                int curBullets = GameComponent_LC.Instance.GetManagerBulletCharges(map);
                DrawKpiChip(new Rect(pillX, pillY, 128f, 18f),
                    "LC_FacilityTab_KpiBullets".Translate(curBullets, maxBullets),
                    new Color(0.38f, 0.90f, 0.78f));
            }

            Text.Font = GameFont.Small;
            Rect codexBtn = new Rect(rect.xMax - 148f, rect.y + 10f, 136f, 34f);
            if (Widgets.ButtonText(codexBtn, "LC_FacilityTab_OpenCodex".Translate()))
            {
                Find.WindowStack.Add(new Dialog_LC_EntityCodex());
                RimWorld.SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        private void DrawRoomsTab(Rect rect, Map map)
        {
            float y = rect.y;

            List<CompAbnormality> escaped = GetEscapedAbnormalities(map);
            if (escaped.Count > 0)
            {
                Rect alertRect = new Rect(rect.x, y, rect.width, 32f);
                Widgets.DrawBoxSolid(alertRect, new Color(0.28f, 0.06f, 0.08f, 0.92f));
                GUI.color = new Color(0.96f, 0.28f, 0.30f);
                Widgets.DrawBox(alertRect, 1);

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(1.0f, 0.86f, 0.84f);
                string names = string.Join(", ", escaped.Take(4).Select(c =>
                {
                    Pawn p = c.parent as Pawn;
                    float hpPct = p?.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                    return $"{p?.LabelShortCap ?? c.parent.LabelShortCap} ({hpPct * 100f:0}%)";
                }));
                if (escaped.Count > 4)
                {
                    names += $" +{escaped.Count - 4}";
                }
                Widgets.LabelEllipses(new Rect(alertRect.x + 8f, alertRect.y, alertRect.width - 110f, alertRect.height),
                    "LC_FacilityTab_EscapedBanner".Translate(names));

                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                if (Widgets.ButtonText(new Rect(alertRect.xMax - 92f, alertRect.y + 4f, 84f, 24f), "LC_FacilityTab_BtnJump".Translate()))
                {
                    JumpAndSelect(escaped[0].parent);
                }

                y += 38f;
            }

            DrawRoomFilters(new Rect(rect.x, y, rect.width, 28f));
            y += 34f;

            // 表头
            Rect headerRect = new Rect(rect.x, y, rect.width - 16f, 22f);
            DrawRoomHeaderRow(headerRect);
            y += 24f;

            List<Building_AbnormalityContainmentTerminal> rows = GetOccupiedTerminals(map)
                .Where(MatchesRoomFilter)
                .OrderByDescending(t => t.IsInMeltdown)
                .ThenByDescending(t => GetRiskOrder(t))
                .ThenBy(t => t.HeldPawn?.LabelShortCap ?? t.LabelShortCap)
                .ToList();

            Rect listOut = new Rect(rect.x, y, rect.width, rect.yMax - y);
            if (rows.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(listOut, "LC_FacilityTab_NoRooms".Translate());
                return;
            }

            const float rowH = 44f;
            Rect listView = new Rect(0f, 0f, listOut.width - 16f, rows.Count * (rowH + 4f));
            Widgets.BeginScrollView(listOut, ref roomsScrollPos, listView);
            float rowY = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                DrawRoomRow(new Rect(0f, rowY, listView.width, rowH), rows[i], i % 2 == 1);
                rowY += rowH + 4f;
            }
            Widgets.EndScrollView();
        }

        private void DrawRoomFilters(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            RoomFilter[] filters = { RoomFilter.All, RoomFilter.Melting, RoomFilter.Working, RoomFilter.Idle, RoomFilter.Tools };
            float x = rect.x;
            for (int i = 0; i < filters.Length; i++)
            {
                RoomFilter f = filters[i];
                Rect btn = new Rect(x, rect.y, 92f, 26f);
                bool active = roomFilter == f;
                if (active)
                {
                    Widgets.DrawBoxSolid(btn, new Color(0.18f, 0.28f, 0.36f, 0.95f));
                }
                if (Widgets.ButtonText(btn, ("LC_FacilityTab_RoomFilter_" + f).Translate(), drawBackground: !active))
                {
                    roomFilter = f;
                    RimWorld.SoundDefOf.Click.PlayOneShotOnCamera();
                }
                x += 98f;
            }
        }

        private void DrawRoomHeaderRow(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, 260f, rect.height), "LC_FacilityTab_ColEntity".Translate());
            Widgets.Label(new Rect(rect.x + 275f, rect.y, 70f, rect.height), "LC_FacilityTab_ColQliphoth".Translate());
            Widgets.Label(new Rect(rect.x + 350f, rect.y, 175f, rect.height), "LC_FacilityTab_ColStatus".Translate());
            Widgets.Label(new Rect(rect.x + 530f, rect.y, 70f, rect.height), "LC_FacilityTab_ColPeBox".Translate());
            Widgets.Label(new Rect(rect.x + 605f, rect.y, 165f, rect.height), "LC_FacilityTab_ColWorkAssign".Translate());
            Widgets.Label(new Rect(rect.x + 775f, rect.y, rect.width - 775f, rect.height), "LC_FacilityTab_ColActions".Translate());
            GUI.color = Color.white;
        }

        private void DrawRoomRow(Rect rect, Building_AbnormalityContainmentTerminal terminal, bool alt)
        {
            bool melting = terminal.IsInMeltdown;
            Color bg = melting
                ? new Color(0.32f, 0.08f, 0.08f, 0.90f)
                : (alt ? new Color(0.12f, 0.14f, 0.17f, 0.72f) : new Color(0.09f, 0.11f, 0.14f, 0.72f));
            Widgets.DrawBoxSolid(rect, bg);
            GUI.color = melting ? new Color(0.95f, 0.34f, 0.24f) : new Color(0.24f, 0.28f, 0.34f, 0.65f);
            Widgets.DrawBox(rect, 1);

            Pawn held = terminal.HeldPawn;
            CompAbnormality abn = terminal.HeldAbnormality;
            bool isTool = terminal.IsHoldingTool;

            // 1. 图标 + 危险等级 + 名称
            GUI.color = Color.white;
            if (held != null)
            {
                Widgets.ThingIcon(new Rect(rect.x + 6f, rect.y + 4f, 34f, 34f), held);
            }

            string riskTag = abn?.AbnormalityCategory?.defName ?? (isTool ? "TOOL" : "-");
            Color riskColor = GetRiskColor(riskTag);
            Rect riskRect = new Rect(rect.x + 44f, rect.y + 12f, 48f, 18f);
            Widgets.DrawBoxSolid(riskRect, new Color(riskColor.r * 0.22f, riskColor.g * 0.22f, riskColor.b * 0.22f, 0.92f));
            GUI.color = riskColor;
            Widgets.DrawBox(riskRect, 1);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.LabelFit(riskRect, riskTag);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.LabelEllipses(new Rect(rect.x + 98f, rect.y + 6f, 172f, 30f), held?.LabelCap ?? terminal.LabelCap);

            // 2. Q 计数器
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            bool qEnabled = abn != null && abn.QliphothEnabled;
            string qText = qEnabled ? $"{terminal.QliphothCounter}/{terminal.QliphothCounterMax}" : "-";
            GUI.color = qEnabled && terminal.QliphothCounter <= 1 ? new Color(1.0f, 0.42f, 0.38f) : new Color(1.0f, 0.86f, 0.42f);
            Widgets.Label(new Rect(rect.x + 275f, rect.y + 6f, 70f, 30f), qText);

            // 3. 状态
            Text.Font = GameFont.Tiny;
            string statusText;
            Color statusColor;
            if (melting)
            {
                int secs = Mathf.CeilToInt(terminal.MeltdownSecondsRemaining);
                statusText = "LC_FacilityTab_StatusMeltdown".Translate(secs);
                statusColor = new Color(1.0f, 0.42f, 0.34f);
            }
            else if (terminal.IsStudySessionActive)
            {
                string workerName = terminal.ActiveStudier?.LabelShortCap ?? "-";
                statusText = "LC_FacilityTab_StatusWorking".Translate(workerName);
                statusColor = new Color(0.42f, 0.94f, 0.62f);
            }
            else if (isTool)
            {
                statusText = "LC_FacilityTab_StatusTool".Translate();
                statusColor = new Color(0.75f, 0.82f, 0.90f);
            }
            else
            {
                int cdTicks = terminal.CompAbnormalityStudiable?.TicksTilNextStudy ?? 0;
                if (cdTicks > 0)
                {
                    statusText = "LC_FacilityTab_StatusCooldown".Translate(Mathf.CeilToInt(cdTicks / 60f));
                    statusColor = new Color(0.92f, 0.78f, 0.34f);
                }
                else
                {
                    statusText = "LC_FacilityTab_StatusReady".Translate();
                    statusColor = ColorLibrary.Green;
                }
            }
            GUI.color = statusColor;
            Widgets.LabelEllipses(new Rect(rect.x + 350f, rect.y + 8f, 175f, 28f), statusText);

            // 4. PE-Box
            GUI.color = new Color(0.45f, 0.94f, 0.68f);
            Widgets.Label(new Rect(rect.x + 530f, rect.y + 8f, 70f, 28f), terminal.PeBoxCounter.ToString());

            // 5. 工作偏好 & 指派人数
            GUI.color = new Color(0.86f, 0.90f, 0.95f);
            string workText = isTool
                ? "-"
                : $"{GetWorkTypeShortLabel(terminal.CurWorkType)} · {"LC_FacilityTab_AssignedCount".Translate(terminal.CompAssignable?.AssignedPawnsForReading.Count ?? 0)}";
            Widgets.LabelEllipses(new Rect(rect.x + 605f, rect.y + 8f, 165f, 28f), workText);

            // 6. 操作按钮
            GUI.color = Color.white;
            float btnX = rect.x + 775f;
            if (!isTool)
            {
                if (Widgets.ButtonText(new Rect(btnX, rect.y + 8f, 66f, 28f), "LC_FacilityTab_BtnWork".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_LC_AssignWorkType(terminal));
                }
                if (terminal.CompAssignable != null && Widgets.ButtonText(new Rect(btnX + 70f, rect.y + 8f, 66f, 28f), "LC_FacilityTab_BtnAssign".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_LC_AssignEntity(terminal.CompAssignable));
                }
            }
            if (Widgets.ButtonText(new Rect(btnX + 140f, rect.y + 8f, 62f, 28f), "LC_FacilityTab_BtnJump".Translate()))
            {
                JumpAndSelect(terminal);
            }
        }

        private void DrawEmployeesTab(Rect rect, Map map)
        {
            float y = rect.y;
            DrawEmployeeFilters(new Rect(rect.x, y, rect.width, 28f));
            y += 34f;

            Rect headerRect = new Rect(rect.x, y, rect.width - 16f, 22f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(headerRect.x + 8f, headerRect.y, 175f, headerRect.height), "LC_FacilityTab_ColEmployee".Translate());
            Widgets.Label(new Rect(headerRect.x + 188f, headerRect.y, 130f, headerRect.height), "LC_FacilityTab_ColDepartment".Translate());
            Widgets.Label(new Rect(headerRect.x + 322f, headerRect.y, 245f, headerRect.height), "LC_FacilityTab_ColVirtues".Translate());
            Widgets.Label(new Rect(headerRect.x + 572f, headerRect.y, 92f, headerRect.height), "LC_FacilityTab_ColVitals".Translate());
            Widgets.Label(new Rect(headerRect.x + 668f, headerRect.y, 210f, headerRect.height), "LC_FacilityTab_ColEgo".Translate());
            Widgets.Label(new Rect(headerRect.x + 882f, headerRect.y, headerRect.width - 882f, headerRect.height), "LC_FacilityTab_ColActions".Translate());
            GUI.color = Color.white;
            y += 24f;

            List<Pawn> colonists = (map.mapPawns?.FreeColonistsSpawned ?? new List<Pawn>())
                .Where(p => MatchesEmployeeFilter(p))
                .OrderByDescending(p => p.GetComp<CompPawnStatus>()?.Enabled == true)
                .ThenByDescending(p => (int)(p.GetComp<CompPawnStatus>()?.SingleLevel ?? EPawnLevel.I))
                .ThenBy(p => p.LabelShortCap)
                .ToList();

            Rect listOut = new Rect(rect.x, y, rect.width, rect.yMax - y);
            if (colonists.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(listOut, "LC_FacilityTab_NoEmployees".Translate());
                return;
            }

            const float rowH = 44f;
            Rect listView = new Rect(0f, 0f, listOut.width - 16f, colonists.Count * (rowH + 4f));
            Widgets.BeginScrollView(listOut, ref employeesScrollPos, listView);
            float rowY = 0f;
            for (int i = 0; i < colonists.Count; i++)
            {
                DrawEmployeeRow(new Rect(0f, rowY, listView.width, rowH), colonists[i], map, i % 2 == 1);
                rowY += rowH + 4f;
            }
            Widgets.EndScrollView();
        }

        private void DrawEmployeeFilters(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            EmployeeFilter[] filters = { EmployeeFilter.All, EmployeeFilter.Assigned, EmployeeFilter.Unassigned };
            float x = rect.x;
            for (int i = 0; i < filters.Length; i++)
            {
                EmployeeFilter f = filters[i];
                Rect btn = new Rect(x, rect.y, 108f, 26f);
                bool active = employeeFilter == f;
                if (active)
                {
                    Widgets.DrawBoxSolid(btn, new Color(0.18f, 0.28f, 0.36f, 0.95f));
                }
                if (Widgets.ButtonText(btn, ("LC_FacilityTab_EmpFilter_" + f).Translate(), drawBackground: !active))
                {
                    employeeFilter = f;
                    RimWorld.SoundDefOf.Click.PlayOneShotOnCamera();
                }
                x += 114f;
            }
        }

        private void DrawEmployeeRow(Rect rect, Pawn pawn, Map map, bool alt)
        {
            Widgets.DrawBoxSolid(rect, alt ? new Color(0.12f, 0.14f, 0.17f, 0.72f) : new Color(0.09f, 0.11f, 0.14f, 0.72f));
            GUI.color = new Color(0.24f, 0.28f, 0.34f, 0.65f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            CompPawnStatus status = pawn.GetComp<CompPawnStatus>();
            bool enabled = status != null && status.Enabled;

            // 1. 头像 + 名字 + 等级
            Widgets.ThingIcon(new Rect(rect.x + 6f, rect.y + 4f, 34f, 34f), pawn);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.LabelEllipses(new Rect(rect.x + 44f, rect.y + 4f, 134f, 22f), pawn.LabelShortCap);

            Text.Font = GameFont.Tiny;
            GUI.color = enabled ? new Color(0.95f, 0.84f, 0.38f) : Color.gray;
            string rankStr = enabled ? "LC_FacilityTab_GradePrefix".Translate(status.SingleLevel.ToString()).ToString() : "LC_FacilityTab_UnassignedBadge".Translate().ToString();
            Widgets.Label(new Rect(rect.x + 44f, rect.y + 23f, 134f, 18f), rankStr);

            // 2. 所属部门
            GUI.color = enabled ? new Color(0.66f, 0.88f, 1.0f) : Color.gray;
            string deptName = status?.AssignedCore?.LabelCap ?? "-";
            Widgets.LabelEllipses(new Rect(rect.x + 188f, rect.y + 8f, 130f, 28f), deptName);

            // 3. 四维美德
            if (status != null)
            {
                float vx = rect.x + 322f;
                DrawVirtueMini(new Rect(vx, rect.y + 8f, 58f, 28f), "F", status, EPawnStatus.Fortitude, new Color(0.96f, 0.36f, 0.34f));
                DrawVirtueMini(new Rect(vx + 60f, rect.y + 8f, 58f, 28f), "P", status, EPawnStatus.Prudence, Color.white);
                DrawVirtueMini(new Rect(vx + 120f, rect.y + 8f, 58f, 28f), "T", status, EPawnStatus.Temperance, new Color(0.78f, 0.50f, 0.98f));
                DrawVirtueMini(new Rect(vx + 180f, rect.y + 8f, 58f, 28f), "J", status, EPawnStatus.Justice, new Color(0.42f, 0.88f, 0.98f));
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + 322f, rect.y + 8f, 240f, 28f), "-");
            }

            // 4. HP / SP
            float hpPct = (pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f) * 100f;
            float spDmg = pawn.health?.hediffSet?.GetFirstHediffOfDef(Defs.HediffDefOf.LC_MentalDamage)?.Severity ?? 0f;
            float spPct = Mathf.Clamp01(1f - spDmg) * 100f;
            GUI.color = new Color(0.45f, 0.94f, 0.62f);
            Widgets.Label(new Rect(rect.x + 572f, rect.y + 4f, 92f, 18f), $"HP {hpPct:0}%");
            GUI.color = new Color(0.46f, 0.84f, 0.98f);
            Widgets.Label(new Rect(rect.x + 572f, rect.y + 22f, 92f, 18f), $"SP {spPct:0}%");

            // 5. E.G.O 装备与四色抗性
            string weaponName = pawn.equipment?.Primary?.LabelCap ?? "-";
            GetBestFourColorResistances(pawn, out string suitName, out float r, out float w, out float b, out float p);
            GUI.color = new Color(0.92f, 0.92f, 0.95f);
            Widgets.LabelEllipses(new Rect(rect.x + 668f, rect.y + 4f, 210f, 18f), $"{weaponName} / {suitName}");
            GUI.color = new Color(0.75f, 0.80f, 0.86f);
            Widgets.Label(new Rect(rect.x + 668f, rect.y + 22f, 210f, 18f), $"R:{r:0.0}  W:{w:0.0}  B:{b:0.0}  P:{p:0.0}");

            // 6. 操作按钮
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(rect.x + 882f, rect.y + 8f, 64f, 28f), "LC_FacilityTab_BtnDept".Translate()))
            {
                OpenDepartmentAssignmentFor(pawn, status, map);
            }
            if (Widgets.ButtonText(new Rect(rect.x + 950f, rect.y + 8f, 52f, 28f), "LC_FacilityTab_BtnJump".Translate()))
            {
                JumpAndSelect(pawn);
            }
        }

        private static void DrawVirtueMini(Rect rect, string tag, CompPawnStatus status, EPawnStatus type, Color color)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = color;
            EPawnLevel lvl = status.GetPawnStatusELevel(type);
            int val = status.GetPawnStatusLevel(type).Status;
            Widgets.LabelFit(rect, $"{tag}:{lvl}({val})");
        }

        private static void OpenDepartmentAssignmentFor(Pawn pawn, CompPawnStatus status, Map map)
        {
            if (status?.AssignedCore?.CompDepartmentCore != null && !status.AssignedCore.Destroyed)
            {
                Find.WindowStack.Add(new Dialog_LC_DepartmentCoreAssign(status.AssignedCore.CompDepartmentCore));
                return;
            }

            List<Building_DepartmentCore> cores = map.listerBuildings
                .AllBuildingsColonistOfClass<Building_DepartmentCore>()
                .Where(c => c != null && c.Spawned && !c.Destroyed && c.CompDepartmentCore != null)
                .ToList();

            if (cores.Count == 0)
            {
                Messages.Message("LC_FacilityTab_NoDepartmentCore".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (cores.Count == 1)
            {
                Find.WindowStack.Add(new Dialog_LC_DepartmentCoreAssign(cores[0].CompDepartmentCore));
                return;
            }

            List<FloatMenuOption> opts = new List<FloatMenuOption>(cores.Count);
            foreach (Building_DepartmentCore core in cores)
            {
                Building_DepartmentCore captured = core;
                opts.Add(new FloatMenuOption(captured.LabelCap, () =>
                {
                    Find.WindowStack.Add(new Dialog_LC_DepartmentCoreAssign(captured.CompDepartmentCore));
                }));
            }
            Find.WindowStack.Add(new Verse.FloatMenu(opts));
        }

        private static void GetBestFourColorResistances(Pawn pawn, out string suitLabel, out float r, out float w, out float b, out float p)
        {
            r = 1f;
            w = 1f;
            b = 1f;
            p = 1f;
            suitLabel = "-";
            float bestAuthoredWeight = float.MaxValue;

            if (pawn?.apparel?.WornApparel == null)
            {
                return;
            }

            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (apparel == null || !FourColorOverrides.TryGetApparelProfile(apparel.def, out FourColorOverrides.ResolvedApparelProfile prof))
                {
                    continue;
                }

                float er = Mathf.Max(0f, FourColorOverrides.ApplyApparelQuality(prof.red, apparel, prof.isAuthoredEgo));
                float ew = Mathf.Max(0f, FourColorOverrides.ApplyApparelQuality(prof.white, apparel, prof.isAuthoredEgo));
                float eb = Mathf.Max(0f, FourColorOverrides.ApplyApparelQuality(prof.black, apparel, prof.isAuthoredEgo));
                float ep = Mathf.Max(0f, FourColorOverrides.ApplyApparelQuality(prof.pale, apparel, prof.isAuthoredEgo));

                r = Mathf.Min(r, er);
                w = Mathf.Min(w, ew);
                b = Mathf.Min(b, eb);
                p = Mathf.Min(p, ep);

                float sum = er + ew + eb + ep;
                if (prof.isAuthoredEgo || (suitLabel == "-" && sum < 3.99f) || sum < bestAuthoredWeight)
                {
                    bestAuthoredWeight = sum;
                    suitLabel = apparel.LabelCap;
                }
            }
        }

        private bool MatchesRoomFilter(Building_AbnormalityContainmentTerminal terminal)
        {
            switch (roomFilter)
            {
                case RoomFilter.Melting: return terminal.IsInMeltdown;
                case RoomFilter.Working: return terminal.IsStudySessionActive;
                case RoomFilter.Idle: return !terminal.IsHoldingTool && !terminal.IsInMeltdown && !terminal.IsStudySessionActive;
                case RoomFilter.Tools: return terminal.IsHoldingTool;
                default: return true;
            }
        }

        private bool MatchesEmployeeFilter(Pawn pawn)
        {
            bool enabled = pawn.GetComp<CompPawnStatus>()?.Enabled == true;
            switch (employeeFilter)
            {
                case EmployeeFilter.Assigned: return enabled;
                case EmployeeFilter.Unassigned: return !enabled;
                default: return true;
            }
        }

        private static List<Building_AbnormalityContainmentTerminal> GetOccupiedTerminals(Map map)
        {
            return map.listerBuildings
                .AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>()
                .Where(t => t != null && t.Spawned && !t.Destroyed && t.Occupied)
                .ToList();
        }

        private static List<CompAbnormality> GetEscapedAbnormalities(Map map)
        {
            List<CompAbnormality> result = new List<CompAbnormality>();
            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn pawn = allPawns[i];
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    continue;
                }

                CompAbnormality comp = pawn.GetComp<CompAbnormality>();
                if (comp != null && comp.Escaped)
                {
                    result.Add(comp);
                }
            }
            return result;
        }

        private static void DrawKpiChip(Rect rect, string label, Color color)
        {
            Widgets.DrawBoxSolid(rect, new Color(color.r * 0.14f, color.g * 0.14f, color.b * 0.14f, 0.88f));
            GUI.color = color;
            Widgets.DrawBox(rect, 1);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.LabelFit(rect, label);
            GUI.color = Color.white;
        }

        private static void JumpAndSelect(Thing target)
        {
            if (target == null || target.Destroyed)
            {
                return;
            }

            CameraJumper.TryJumpAndSelect(target);
        }

        private static int GetRiskOrder(Building_AbnormalityContainmentTerminal t)
        {
            switch (t.HeldAbnormality?.AbnormalityCategory?.defName)
            {
                case "ALEPH": return 5;
                case "WAW": return 4;
                case "HE": return 3;
                case "TETH": return 2;
                case "ZAYIN": return 1;
                default: return 0;
            }
        }

        private static Color GetRiskColor(string tag)
        {
            switch (tag)
            {
                case "ZAYIN": return new Color(0.25f, 0.85f, 0.25f);
                case "TETH": return new Color(0.28f, 0.65f, 1.0f);
                case "HE": return new Color(0.95f, 0.82f, 0.20f);
                case "WAW": return new Color(0.72f, 0.35f, 0.96f);
                case "ALEPH": return new Color(0.95f, 0.22f, 0.25f);
                default: return new Color(0.78f, 0.82f, 0.88f);
            }
        }

        private static string GetWorkTypeShortLabel(EAnomalyWorkType wt)
        {
            string tipKey = "TipDialogWorkType_" + wt;
            if (tipKey.CanTranslate())
            {
                return tipKey.Translate().ToString();
            }
            return wt.ToString();
        }
    }
}
