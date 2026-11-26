using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Interface;
using LCAnomalyCore.UI;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 收容工作指派与成功率预览台：展示异想体PE规则、员工四维属性、四种工作方式预估成功率与一键派工按钮。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Dialog_LC_AssignWorkType : Window
    {
        private static readonly EAnomalyWorkType[] OrderedWorkTypes =
        {
            EAnomalyWorkType.Instinct,
            EAnomalyWorkType.Insight,
            EAnomalyWorkType.Attachment,
            EAnomalyWorkType.Repression
        };

        private static readonly Texture2D TexIconInstinct = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Instinct", false);
        private static readonly Texture2D TexIconInsight = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Insight", false);
        private static readonly Texture2D TexIconAttachment = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Attachment", false);
        private static readonly Texture2D TexIconRepression = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Repression", false);

        private readonly IHoldingPlatformWorkTypeSelectable platform;
        private readonly IAbnormalityContainmentUnit unit;
        private readonly Pawn heldPawn;
        private readonly CompAbnormality abnormality;
        private readonly CompPeBoxProduce peComp;
        private readonly List<Pawn> candidateEmployees = new List<Pawn>();
        private Pawn previewEmployee;

        /// <summary>窗体尺寸。</summary>
        public override Vector2 InitialSize => new Vector2(540f, 440f);

        /// <summary>初始化 <c>Dialog_LC_AssignWorkType</c> 类的新实例。</summary>
        public Dialog_LC_AssignWorkType(IHoldingPlatformWorkTypeSelectable platform)
        {
            this.platform = platform;
            unit = platform as IAbnormalityContainmentUnit;
            heldPawn = unit?.HeldPawn;
            abnormality = heldPawn?.GetComp<CompAbnormality>();
            peComp = heldPawn?.GetComp<CompPeBoxProduce>();

            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;

            RefreshCandidates();
        }

        /// <summary>计算指定员工以特定工作类型对目标异想体作业的预估单次产盒成功率（0~1）。</summary>
        public static float ComputeSuccessRatePreview(CompAbnormality abnormality, CompPawnStatus studier, EAnomalyWorkType workType)
        {
            if (abnormality == null || studier == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(abnormality.CheckStudierSkillRequire(studier, workType));
        }

        /// <summary>按脑叶原版五档标准返回成功率对应的翻译键。</summary>
        public static string GetSuccessTierKey(float rate)
        {
            if (rate >= 0.80f) return "LC_WorkPreview_TierVeryHigh";
            if (rate >= 0.60f) return "LC_WorkPreview_TierHigh";
            if (rate >= 0.40f) return "LC_WorkPreview_TierCommon";
            if (rate >= 0.20f) return "LC_WorkPreview_TierLow";
            return "LC_WorkPreview_TierVeryLow";
        }

        /// <summary>按五档成功率返回高亮颜色。</summary>
        public static Color GetSuccessTierColor(float rate)
        {
            if (rate >= 0.80f) return new Color(0.28f, 0.96f, 0.60f);
            if (rate >= 0.60f) return new Color(0.55f, 0.90f, 0.35f);
            if (rate >= 0.40f) return new Color(0.96f, 0.84f, 0.30f);
            if (rate >= 0.20f) return new Color(0.98f, 0.56f, 0.24f);
            return new Color(0.95f, 0.28f, 0.28f);
        }

        private void RefreshCandidates()
        {
            candidateEmployees.Clear();
            Map map = unit?.UnitThing?.Map;
            if (map?.mapPawns?.FreeColonistsSpawned == null)
            {
                return;
            }

            HashSet<Pawn> assignedSet = new HashSet<Pawn>();
            if (unit.CompAssignable?.AssignedPawns != null)
            {
                foreach (Pawn assigned in unit.CompAssignable.AssignedPawns)
                {
                    if (assigned != null)
                    {
                        assignedSet.Add(assigned);
                    }
                }
            }

            foreach (Pawn col in map.mapPawns.FreeColonistsSpawned)
            {
                if (col == null || col.Dead)
                {
                    continue;
                }

                CompPawnStatus status = col.GetComp<CompPawnStatus>();
                if (status != null && status.Enabled)
                {
                    candidateEmployees.Add(col);
                }
            }

            candidateEmployees.Sort((a, b) =>
            {
                int assignCmp = assignedSet.Contains(b).CompareTo(assignedSet.Contains(a));
                if (assignCmp != 0) return assignCmp;
                int lvA = (int)(a.GetComp<CompPawnStatus>()?.SingleLevel ?? EPawnLevel.I);
                int lvB = (int)(b.GetComp<CompPawnStatus>()?.SingleLevel ?? EPawnLevel.I);
                int lvCmp = lvB.CompareTo(lvA);
                if (lvCmp != 0) return lvCmp;
                return string.Compare(a.LabelShortCap, b.LabelShortCap, StringComparison.Ordinal);
            });

            if (candidateEmployees.Count > 0)
            {
                previewEmployee = candidateEmployees[0];
            }
        }

        /// <inheritdoc />
        public override void DoWindowContents(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                float y = inRect.y;

                // 1) Header (44px): Risk badge + Abnormality Name + Qliphoth/PE thresholds
                Rect headerRect = new Rect(inRect.x, y, inRect.width, 44f);
                DrawHeader(headerRect);
                y += 50f;

                // 2) Optional Meltdown Emergency Banner (24px)
                if (platform is Building_AbnormalityContainmentTerminal term && term.IsInMeltdown)
                {
                    Rect meltdownRect = new Rect(inRect.x, y, inRect.width, 24f);
                    DrawMeltdownBanner(meltdownRect, term);
                    y += 28f;
                }

                // 3) Employee Selector & Four-Virtue Summary (46px)
                Rect empRect = new Rect(inRect.x, y, inRect.width, 46f);
                DrawEmployeeSelector(empRect);
                y += 52f;

                // 4) Footer reservation + 2x2 Work Type Cards Grid
                float footerH = 34f;
                float gridH = Mathf.Max(140f, inRect.yMax - footerH - 8f - y);
                Rect gridRect = new Rect(inRect.x, y, inRect.width, gridH);
                DrawWorkGrid(gridRect);

                // 5) Footer: Dispatch Now + Confirm
                Rect footerRect = new Rect(inRect.x, inRect.yMax - footerH, inRect.width, footerH);
                DrawFooter(footerRect);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        private void DrawHeader(Rect rect)
        {
            string riskTag = heldPawn != null ? EscapedAbnormalityOverlayDrawer.GetRiskTag(heldPawn) : null;
            Color riskColor = EscapedAbnormalityOverlayDrawer.GetRiskColor(riskTag);

            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.10f, 0.13f, 0.94f));
            GUI.color = new Color(0.28f, 0.34f, 0.40f);
            Widgets.DrawBox(rect, 1);
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 2f, 4f, rect.height - 4f), riskColor);

            float leftX = rect.x + 10f;
            if (!riskTag.NullOrEmpty())
            {
                Rect riskRect = new Rect(leftX, rect.y + 6f, 52f, 20f);
                Widgets.DrawBoxSolid(riskRect, new Color(riskColor.r, riskColor.g, riskColor.b, 0.92f));
                bool darkText = riskTag == "ZAYIN" || riskTag == "TETH" || riskTag == "HE";
                GUI.color = darkText ? new Color(0.06f, 0.07f, 0.09f) : Color.white;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.LabelFit(riskRect, riskTag);
                leftX = riskRect.xMax + 8f;
            }

            string abnoName = heldPawn?.LabelShortCap ?? unit?.UnitThing?.LabelCap ?? "---";
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.Lerp(Color.white, riskColor, 0.15f);
            Widgets.LabelFit(new Rect(leftX, rect.y + 2f, rect.width * 0.50f, 26f), abnoName);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.75f, 0.80f, 0.86f);
            Widgets.LabelFit(new Rect(rect.x + 10f, rect.y + 26f, rect.width - 20f, 16f), "LC_WorkPreview_TitleHint".Translate());

            // Right side: Qliphoth, PE-Box, Result Ranges
            if (abnormality != null)
            {
                string qText = abnormality.QliphothEnabled
                    ? $"{abnormality.QliphothCountCurrent}/{abnormality.QliphothCountMax}"
                    : "-";
                int indiPe = 0;
                if (Util.Components.LC != null && heldPawn != null)
                {
                    Util.Components.LC.TryGetAnomalyStatusSaved(heldPawn.def, out var saved);
                    indiPe = saved.IndiPeBoxAmount;
                }

                string thresholdText = peComp != null
                    ? "LC_WorkPreview_HeaderStats".Translate(
                        qText,
                        indiPe,
                        peComp.Props.amountProdueRangeNormal.min,
                        peComp.Props.amountProdueRangeNormal.max,
                        peComp.Props.amountProdueMax)
                    : string.Empty;

                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = new Color(0.65f, 0.92f, 0.78f);
                Widgets.LabelFit(new Rect(rect.x + rect.width * 0.42f, rect.y + 3f, rect.width * 0.56f - 8f, 22f), thresholdText);
            }
        }

        private void DrawMeltdownBanner(Rect rect, Building_AbnormalityContainmentTerminal term)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 7.5f);
            Widgets.DrawBoxSolid(rect, Color.Lerp(new Color(0.32f, 0.06f, 0.06f, 0.94f), new Color(0.55f, 0.10f, 0.08f, 0.96f), pulse));
            GUI.color = Color.Lerp(new Color(0.95f, 0.30f, 0.22f), new Color(1.0f, 0.72f, 0.28f), pulse);
            Widgets.DrawBox(rect, 1);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.LabelFit(rect, "LC_WorkPreview_MeltdownAlert".Translate(Mathf.CeilToInt(term.MeltdownSecondsRemaining)));
        }

        private void DrawEmployeeSelector(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.10f, 0.12f, 0.15f, 0.85f));
            GUI.color = new Color(0.24f, 0.28f, 0.33f);
            Widgets.DrawBox(rect, 1);

            if (candidateEmployees.Count == 0 || previewEmployee == null)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(rect, "LC_WorkPreview_NoEmployees".Translate());
                return;
            }

            Rect btnRect = new Rect(rect.x + 6f, rect.y + 8f, 168f, 30f);
            GUI.color = Color.white;
            string btnLabel = "LC_WorkPreview_EmployeeBtn".Translate(previewEmployee.LabelShortCap);
            if (Widgets.ButtonText(btnRect, btnLabel))
            {
                OpenEmployeeDropdown();
            }

            CompPawnStatus st = previewEmployee.GetComp<CompPawnStatus>();
            if (st != null)
            {
                Rect virtueRect = new Rect(btnRect.xMax + 10f, rect.y + 4f, rect.xMax - btnRect.xMax - 16f, rect.height - 8f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                string vLine1 = string.Format(
                    "{0}: {1} ({2})   |   {3}: {4} ({5})",
                    "LC_WorkPreview_Virtue_Instinct".Translate(), st.GetPawnStatusELevel(EPawnStatus.Fortitude), st.GetPawnStatusLevel(EPawnStatus.Fortitude).Status,
                    "LC_WorkPreview_Virtue_Insight".Translate(), st.GetPawnStatusELevel(EPawnStatus.Prudence), st.GetPawnStatusLevel(EPawnStatus.Prudence).Status);
                string vLine2 = string.Format(
                    "{0}: {1} ({2})   |   {3}: {4} ({5})",
                    "LC_WorkPreview_Virtue_Attachment".Translate(), st.GetPawnStatusELevel(EPawnStatus.Temperance), st.GetPawnStatusLevel(EPawnStatus.Temperance).Status,
                    "LC_WorkPreview_Virtue_Repression".Translate(), st.GetPawnStatusELevel(EPawnStatus.Justice), st.GetPawnStatusLevel(EPawnStatus.Justice).Status);

                GUI.color = new Color(0.88f, 0.91f, 0.95f);
                Widgets.LabelFit(new Rect(virtueRect.x, virtueRect.y, virtueRect.width, virtueRect.height * 0.5f), vLine1);
                Widgets.LabelFit(new Rect(virtueRect.x, virtueRect.y + virtueRect.height * 0.5f, virtueRect.width, virtueRect.height * 0.5f), vLine2);
            }
        }

        private void OpenEmployeeDropdown()
        {
            HashSet<Pawn> assignedSet = new HashSet<Pawn>();
            if (unit?.CompAssignable?.AssignedPawns != null)
            {
                foreach (Pawn p in unit.CompAssignable.AssignedPawns)
                {
                    if (p != null) assignedSet.Add(p);
                }
            }

            List<FloatMenuOption> opts = new List<FloatMenuOption>(candidateEmployees.Count);
            for (int i = 0; i < candidateEmployees.Count; i++)
            {
                Pawn emp = candidateEmployees[i];
                CompPawnStatus st = emp.GetComp<CompPawnStatus>();
                string tag = assignedSet.Contains(emp) ? "★ " : string.Empty;
                string lv = st != null ? st.SingleLevel.ToString() : "?";
                string label = $"{tag}{emp.LabelShortCap} (Lv.{lv})";
                opts.Add(new FloatMenuOption(label, () => previewEmployee = emp));
            }
            Find.WindowStack.Add(new Verse.FloatMenu(opts));
        }

        private void DrawWorkGrid(Rect gridRect)
        {
            float gap = 10f;
            float cardW = (gridRect.width - gap) * 0.5f;
            float cardH = (gridRect.height - gap) * 0.5f;

            for (int i = 0; i < OrderedWorkTypes.Length; i++)
            {
                EAnomalyWorkType workType = OrderedWorkTypes[i];
                int col = i % 2;
                int row = i / 2;
                Rect cardRect = new Rect(
                    gridRect.x + col * (cardW + gap),
                    gridRect.y + row * (cardH + gap),
                    cardW,
                    cardH);
                DrawWorkCard(cardRect, workType);
            }
        }

        private void DrawWorkCard(Rect rect, EAnomalyWorkType workType)
        {
            bool selected = platform != null && platform.CurWorkType == workType;
            bool hovered = Mouse.IsOver(rect);
            Color workAccent = WorkNarrationUtility.GetWorkTypeAccentColor(workType);

            Color bgColor = selected
                ? new Color(0.12f, 0.17f, 0.22f, 0.96f)
                : (hovered ? new Color(0.11f, 0.14f, 0.18f, 0.94f) : new Color(0.08f, 0.10f, 0.13f, 0.90f));
            Widgets.DrawBoxSolid(rect, bgColor);

            GUI.color = selected
                ? workAccent
                : (hovered ? Color.Lerp(new Color(0.32f, 0.38f, 0.44f), workAccent, 0.55f) : new Color(0.24f, 0.28f, 0.34f));
            Widgets.DrawBox(rect, selected ? 2 : 1);
            Widgets.DrawBoxSolid(new Rect(rect.x + 2f, rect.y + 4f, 4f, rect.height - 8f), workAccent);

            // Icon + Title + Selected Pill
            Texture2D iconTex = GetWorkTypeIcon(workType) ?? GraphicUtil.DialogAssignWorkTypeNormalTexture_Get(workType);
            float iconSize = 36f;
            Rect iconRect = new Rect(rect.x + 12f, rect.y + 10f, iconSize, iconSize);
            if (iconTex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit);
            }

            string workLabel = ("TipDialogWorkType_" + workType).Translate().ToString();
            string virtueLabel = GetVirtueDisplay(workType);

            float textX = iconRect.xMax + 10f;
            float textW = rect.xMax - textX - 10f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.Lerp(Color.white, workAccent, 0.20f);
            Widgets.LabelFit(new Rect(textX, rect.y + 8f, textW - (selected ? 52f : 0f), 22f), workLabel);

            if (selected)
            {
                Rect selPill = new Rect(rect.xMax - 58f, rect.y + 10f, 48f, 18f);
                Widgets.DrawBoxSolid(selPill, new Color(workAccent.r * 0.25f, workAccent.g * 0.25f, workAccent.b * 0.25f, 0.95f));
                GUI.color = workAccent;
                Widgets.DrawBox(selPill, 1);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.LabelFit(selPill, "LC_WorkPreview_SelectedTag".Translate());
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.76f, 0.80f, 0.86f);
            Widgets.LabelFit(new Rect(textX, rect.y + 30f, textW, 18f), virtueLabel);

            // Bottom area: Success Rate Progress Bar + Numeric/Tier label
            CompPawnStatus previewStatus = previewEmployee?.GetComp<CompPawnStatus>();
            if (abnormality != null && previewStatus != null)
            {
                float rate = ComputeSuccessRatePreview(abnormality, previewStatus, workType);
                Color tierColor = GetSuccessTierColor(rate);
                string tierText = GetSuccessTierKey(rate).Translate().ToString();
                int pct = Mathf.RoundToInt(rate * 100f);

                Rect labelRect = new Rect(rect.x + 12f, rect.yMax - 42f, rect.width - 24f, 18f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.72f, 0.76f, 0.82f);
                Widgets.LabelFit(new Rect(labelRect.x, labelRect.y, 88f, 18f), "LC_WorkPreview_SuccessRateLabel".Translate());

                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = tierColor;
                Widgets.LabelFit(new Rect(labelRect.x + 84f, labelRect.y, labelRect.width - 84f, 18f), $"{tierText} · {pct}%");

                Rect barOuter = new Rect(rect.x + 12f, rect.yMax - 22f, rect.width - 24f, 12f);
                Widgets.DrawBoxSolid(barOuter, new Color(0.05f, 0.06f, 0.08f, 0.95f));
                GUI.color = new Color(0.24f, 0.28f, 0.34f);
                Widgets.DrawBox(barOuter, 1);
                Rect barInner = barOuter.ContractedBy(2f);
                if (rate > 0f)
                {
                    float fillW = Mathf.Max(2f, barInner.width * rate);
                    Widgets.DrawBoxSolid(new Rect(barInner.x, barInner.y, Mathf.Min(barInner.width, fillW), barInner.height), tierColor);
                }
            }
            else
            {
                Rect emptyRect = new Rect(rect.x + 12f, rect.yMax - 30f, rect.width - 24f, 20f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.LabelFit(emptyRect, "--");
            }

            if (Mouse.IsOver(rect))
            {
                string tip = ("CommandAssignmentWorkTypeOnHoverDesc_" + workType).Translate().ToString();
                if (abnormality?.StudiableComp != null)
                {
                    float unlockOffset = abnormality.StudiableComp.GetWorkSuccessRateOffset();
                    if (Mathf.Abs(unlockOffset) > 0.0001f)
                    {
                        tip += "\n" + "LC_WorkPreview_UnlockBonusTip".Translate(Mathf.RoundToInt(unlockOffset * 100f));
                    }
                }
                TooltipHandler.TipRegion(rect, tip);
            }

            if (Widgets.ButtonInvisible(rect) && platform != null)
            {
                platform.CurWorkType = workType;
                RimWorld.SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        private void DrawFooter(Rect rect)
        {
            float btnW = 148f;
            float btnH = rect.height;

            Rect dispatchRect = new Rect(rect.x, rect.y, 200f, btnH);
            string disableReason = null;
            if (previewEmployee == null || unit == null || heldPawn == null)
            {
                disableReason = "LC_WorkPreview_NoEmployees".Translate();
            }
            else if (unit.CompAbnormalityStudiable == null || !unit.CompAbnormalityStudiable.CurrentlyStudiable())
            {
                disableReason = (unit.CompAbnormalityStudiable != null && unit.CompAbnormalityStudiable.TicksTilNextStudy > 0)
                    ? "LC_CanBeStudiedInDuration".Translate(unit.CompAbnormalityStudiable.TicksTilNextStudy.ToStringTicksToPeriod()).CapitalizeFirst()
                    : "LC_WorkPreview_NotStudiableNow".Translate();
            }
            else if (!previewEmployee.CanReserveAndReach(heldPawn, PathEndMode.Touch, Danger.Deadly, 1, -1, null, ignoreOtherReservations: true))
            {
                disableReason = "NoPath".Translate().CapitalizeFirst();
            }

            GUI.color = Color.white;
            if (disableReason == null)
            {
                if (Widgets.ButtonText(dispatchRect, "LC_WorkPreview_DispatchBtn".Translate(previewEmployee.LabelShortCap)))
                {
                    Job studyJob = JobMaker.MakeJob(Defs.JobDefOf.LC_AbnormalityStudyInteract, unit.UnitThing, null, heldPawn);
                    previewEmployee.jobs.TryTakeOrderedJob(studyJob, JobTag.Misc);
                    Close();
                }
            }
            else
            {
                GUI.color = new Color(0.55f, 0.55f, 0.55f);
                Widgets.ButtonText(dispatchRect, "LC_WorkPreview_DispatchDisabled".Translate(), active: false);
                TooltipHandler.TipRegion(dispatchRect, disableReason);
            }

            GUI.color = Color.white;
            Rect confirmRect = new Rect(rect.xMax - btnW, rect.y, btnW, btnH);
            if (Widgets.ButtonText(confirmRect, "Confirm".Translate().CapitalizeFirst()))
            {
                Close();
            }
        }

        private string GetVirtueDisplay(EAnomalyWorkType workType)
        {
            EPawnStatus stat;
            switch (workType)
            {
                case EAnomalyWorkType.Instinct: stat = EPawnStatus.Fortitude; break;
                case EAnomalyWorkType.Insight: stat = EPawnStatus.Prudence; break;
                case EAnomalyWorkType.Attachment: stat = EPawnStatus.Temperance; break;
                case EAnomalyWorkType.Repression: stat = EPawnStatus.Justice; break;
                default: stat = EPawnStatus.Fortitude; break;
            }

            string virtueName = ("LC_WorkPreview_Virtue_" + workType).Translate().ToString();
            CompPawnStatus st = previewEmployee?.GetComp<CompPawnStatus>();
            if (st == null)
            {
                return virtueName;
            }

            return $"{virtueName} · Lv.{st.GetPawnStatusELevel(stat)} ({st.GetPawnStatusLevel(stat).Status})";
        }

        private static Texture2D GetWorkTypeIcon(EAnomalyWorkType workType)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct: return TexIconInstinct;
                case EAnomalyWorkType.Insight: return TexIconInsight;
                case EAnomalyWorkType.Attachment: return TexIconAttachment;
                case EAnomalyWorkType.Repression: return TexIconRepression;
                default: return null;
            }
        }
    }
}
