using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.UI
{
    /// <summary>表示 <c>ITab_AbnormalityStudyNotes</c> 类型。</summary>
    public class ITab_AbnormalityStudyNotes : ITab
    {
        private Vector2 leftScroll;

        private Vector2 rightScroll;

        private ChoiceLetter selectedLetter;

        private Thing previous;

        private const float TopPadding = 20f;

        private const float InitialHeight = 350f;

        private const float TitleHeight = 30f;

        private const float InitialWidth = 610f;

        private const float DateSize = 90f;

        private const float RowHeight = 30f;

        /// <summary>获取 <c>StudiableThing</c>。</summary>
        protected Thing StudiableThing => (base.SelThing as Building_AbnormalityHoldingPlatform)?.HeldPawn ?? base.SelThing;

        /// <inheritdoc />
        public override bool IsVisible
        {
            get
            {
                return StudyUnlocks != null;
            }
        }

        private CompAbnormalityStudyUnlocks StudyUnlocks => StudiableThing?.TryGetComp<CompAbnormalityStudyUnlocks>();

        /// <summary>获取 <c>Letters</c>。</summary>
        protected virtual IReadOnlyList<ChoiceLetter> Letters => StudyUnlocks?.Letters;

        /// <summary>获取 <c>StudyCompleted</c>。</summary>
        protected virtual bool StudyCompleted => StudyUnlocks?.Completed ?? false;

        /// <summary>初始化 <c>ITab_AbnormalityStudyNotes</c> 类的新实例。</summary>
        public ITab_AbnormalityStudyNotes()
        {
            size = new Vector2(Mathf.Min(610f, Verse.UI.screenWidth), 350f);
            labelKey = "TabStudyNotesContents";
        }

        /// <inheritdoc />
        public override void OnOpen()
        {
            selectedLetter = (Letters?.EnumerableNullOrEmpty() ?? true) ? null : Letters[Letters.Count - 1];
        }

        /// <inheritdoc />
        protected override void FillTab()
        {
            if (previous != StudiableThing)
            {
                var letters = Letters;
                selectedLetter = (letters == null || letters.Count == 0) ? null : letters[letters.Count - 1];
                previous = StudiableThing;
            }
            Rect rect = new Rect(0f, 20f, size.x, size.y - 20f);
            rect = rect.ContractedBy(10f);
            Rect rect2 = rect;
            rect2.y = 10f;
            rect2.height = 30f;
            Rect rect3 = rect;
            rect3.yMin = rect2.yMax + 17f;
            rect3.SplitVerticallyWithMargin(out var left, out var right, 17f);
            right.yMin += 17f;
            Rect rect4 = right;
            rect4.xMin -= 17f;
            rect4.yMin = rect.yMin;
            DrawTitle(rect2);
            DrawLetters(left);
            if (selectedLetter != null)
            {
                Widgets.LabelScrollable(right, selectedLetter.Text, ref rightScroll);
                return;
            }
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleCenter, Color.gray))
            {
                Widgets.Label(rect4, "StudyNotesTab_NoDiscoveries".Translate());
            }
        }

        private void DrawTitle(Rect rect)
        {
            float num = 0f;
            if (StudiableThing != null)
            {
                Rect position = rect;
                position.width = position.height;
                num = rect.height + 10f;
                GUI.DrawTexture(position, StudiableThing.def.uiIcon);
            }
            Rect rect2 = rect;
            rect2.xMin += num;
            rect2.xMax = rect.x + rect.width / 2f;
            Rect rect3 = rect;
            rect3.xMin = rect2.xMax;
            Rect rect4 = rect3;
            rect4.y = rect2.yMax - 4f;
            using (new TextBlock(GameFont.Medium, TextAnchor.MiddleLeft))
            {
                if (StudiableThing != null)
                    Widgets.LabelFit(rect2, StudiableThing.LabelCap);
            }
            CompAbnormalityStudiable compStudiable = StudiableThing?.TryGetComp<CompAbnormalityStudiable>();
            if (compStudiable == null)
            {
                return;
            }
            Widgets.CheckboxLabeled(rect3, "StudyNotesTab_ToggleStudy".Translate(), ref compStudiable.studyEnabled, disabled: false, null, null, placeCheckboxNearText: true);
        }

        private void DrawLetters(Rect rect)
        {
            Rect rect2 = rect;
            rect2.height = Text.LineHeight;
            TaggedString taggedString = (StudyCompleted ? "StudyNotesTab_StudyProgressCompleted".Translate() : "StudyNotesTab_StudyProgressOngoing".Translate());
            TaggedString taggedString2 = "StudyNotesTab_StudyProgress".Translate();
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(rect2, $"{taggedString2}: {taggedString}");
            }
            Widgets.DrawLineHorizontal(rect.x, rect2.yMax + 4f, rect.width, Color.gray);
            var letters = Letters;
            int num = (letters != null && !letters.EnumerableNullOrEmpty()) ? letters.Count : 0;
            Rect outRect = rect;
            outRect.yMin = rect2.yMax + 10f;
            Rect rect3 = new Rect(0f, 0f, rect.width, 30f * (float)num);
            float y = 0f;
            Widgets.BeginScrollView(outRect, ref leftScroll, rect3);
            for (int num2 = num - 1; num2 >= 0; num2--)
            {
                DoLetterRow(rect3, ref y, letters[num2], num2);
            }
            Widgets.EndScrollView();
        }

        private void DoLetterRow(Rect rect, ref float y, ChoiceLetter letter, int index)
        {
            rect.y = y;
            rect.height = 30f;
            y += 30f;
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlightSelected(rect);
                selectedLetter = letter;
            }
            else if (selectedLetter == letter)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(rect);
            }
            Rect rect2 = rect;
            rect2.width = 90f;
            Vector2 location = ((Find.CurrentMap != null) ? Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile) : default(Vector2));
            string str = GenDate.DateShortStringAt(GenDate.TickGameToAbs(letter.arrivalTick), location);
            Rect rect3 = rect;
            rect3.xMin = rect2.xMax + 4f;
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft, false))
            {
                Widgets.Label(rect2, str.Truncate(rect2.width));
                using (new TextBlock(new Color(0.75f, 0.75f, 0.75f)))
                {
                    Widgets.LabelEllipses(rect3, letter.Label);
                }
            }
        }
    }
}
