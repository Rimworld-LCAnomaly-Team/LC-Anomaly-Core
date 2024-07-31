using LCAnomalyCore.Comp;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 自动研究分配对话框
    /// </summary>
    public class Dialog_LC_AssignEntity : Window
    {
        private CompAssignableToPawn_LC_Entity assignable;

        private Vector2 scrollPosition;

        private static readonly List<Pawn> tmpPawnSorted = new List<Pawn>(12);

        /// <summary>
        /// 窗体的尺寸
        /// </summary>
        public override Vector2 InitialSize => new Vector2(650f, 500f);

        public Dialog_LC_AssignEntity(CompAssignableToPawn_LC_Entity assignable)
        {
            this.assignable = assignable;
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            Rect outRect = new Rect(inRect);
            outRect.yMin += 20f;
            outRect.yMax -= 40f;

            float num = 0f;
            num += (float)assignable.AssignedPawnsForReading.Count * 35f;
            num += (float)assignable.AssigningCandidates.Count() * 35f;
            num += 7f;

            Rect viewRect = new Rect(0f, 0f, outRect.width, num);

            Widgets.AdjustRectsForScrollView(inRect, ref outRect, ref viewRect);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            SortTmpList(assignable.AssignedPawnsForReading);

            float y = 0f;
            for (int i = 0; i < tmpPawnSorted.Count; i++)
            {
                Pawn pawn = tmpPawnSorted[i];
                DrawAssignedRow(pawn, ref y, viewRect, i);
            }

            if (assignable.AssignedPawnsForReading.Count > 0)
            {
                Rect rect = new Rect(0f, y, viewRect.width, 7f);
                y += 7f;
                using (new TextBlock(Widgets.SeparatorLineColor))
                {
                    Widgets.DrawLineHorizontal(rect.x, rect.y + rect.height / 2f, rect.width);
                }
            }

            SortTmpList(assignable.AssigningCandidates);
            for (int j = 0; j < tmpPawnSorted.Count; j++)
            {
                Pawn pawn2 = tmpPawnSorted[j];
                DrawUnassignedRow(pawn2, ref y, viewRect, j);
            }

            tmpPawnSorted.Clear();
            Widgets.EndScrollView();
        }

        private void SortTmpList(IEnumerable<Pawn> collection)
        {
            tmpPawnSorted.Clear();
            tmpPawnSorted.AddRange(collection);
            tmpPawnSorted.SortBy((Pawn x) => x.LabelShort);
        }

        private void DrawAssignedRow(Pawn pawn, ref float y, Rect viewRect, int i)
        {
            Rect rect = new Rect(0f, y, viewRect.width, 35f);
            y += 35f;
            if (i % 2 == 1)
            {
                Widgets.DrawLightHighlight(rect);
            }

            Rect rect2 = rect;
            rect2.width = rect.height;
            Widgets.ThingIcon(rect2, pawn);

            Rect rect3 = rect;
            rect3.xMin = rect.xMax - 165f - 10f;
            rect3 = rect3.ContractedBy(2f);
            if (Widgets.ButtonText(rect3, "LC_AssignableEntity_EntityUnassign".Translate()))
            {
                assignable.TryUnassignPawn(pawn);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            Rect rect4 = rect;
            rect4.xMin = rect2.xMax + 10f;
            rect4.xMax = rect3.xMin - 10f;
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.LabelEllipses(rect4, pawn.LabelCap);
            }

            Rect rect5 = rect;
            rect5.xMin = rect3.xMin - 180f;
            rect5.xMax = rect3.xMin - 10f;
            DrawAssignableWorkRow(pawn, rect5);
            DrawToolTipRow(pawn, rect4);
        }

        private void DrawUnassignedRow(Pawn pawn, ref float y, Rect viewRect, int i)
        {
            if (assignable.AssignedPawnsForReading.Contains(pawn))
            {
                return;
            }

            AcceptanceReport acceptanceReport = assignable.CanAssignTo(pawn);
            bool accepted = acceptanceReport.Accepted;
            Rect rect = new Rect(0f, y, viewRect.width, 35f);
            y += 35f;
            if (i % 2 == 1)
            {
                Widgets.DrawLightHighlight(rect);
            }

            if (!accepted)
            {
                GUI.color = Color.gray;
            }

            Rect rect2 = rect;
            rect2.width = rect.height;
            Widgets.ThingIcon(rect2, pawn);

            Rect rect3 = rect;
            rect3.xMin = rect.xMax - 165f - 10f;
            rect3 = rect3.ContractedBy(2f);
            if (accepted && Widgets.ButtonText(rect3, "LC_AssignableEntity_EntityAssign".Translate()))
            {
                assignable.TryAssignPawn(pawn);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            Rect rect5 = rect;
            rect5.xMin = rect2.xMax + 10f;
            rect5.xMax = rect3.xMin - 10f;
            string label = pawn.LabelCap + (accepted ? "" : (" (" + acceptanceReport.Reason.StripTags() + ")"));
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.LabelEllipses(rect5, label);
            }

            Rect rect6 = rect;
            rect6.xMin = rect3.xMin - 180f;
            rect6.xMax = rect3.xMin - 10f;
            DrawAssignableWorkRow(pawn, rect6);
            DrawToolTipRow(pawn, rect5);
        }

        private void DrawAssignableWorkRow(Pawn pawn, Rect rect)
        {
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = "";

                //检查是否满足最低技能条件
                if (assignable.CheckSkillRequire(pawn))
                {
                    text = "LC_AssignableEntity_SkillRequire".Translate();
                    Widgets.LabelEllipses(rect, ColoredText.Colorize(text, ColorLibrary.Green));
                }
                else
                {
                    text = "LC_AssignableEntity_SkillNotRequire".Translate();
                    Widgets.LabelEllipses(rect, ColoredText.Colorize(text, ColorLibrary.Red));
                }
            }
        }

        private void DrawToolTipRow(Pawn pawn, Rect rect)
        {
            SkillDef intellectual = SkillDefOf.Intellectual;

            string labelCap = pawn.LabelCap;
            labelCap += "\n" + intellectual.LabelCap + ": ";

            bool disabled = pawn.skills.GetSkill(intellectual).TotallyDisabled;
            labelCap += disabled
                ? "LC_AssignableEntity_SkillDisabled".Translate() + SkillDefOf.Intellectual.label.Translate()
                : pawn.skills.GetSkill(intellectual).GetLevel();

            if (Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, labelCap);
        }
    }
}