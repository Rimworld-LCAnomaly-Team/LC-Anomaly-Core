using LCAnomalyCore.Comp;
using LCAnomalyCore.Defs;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
    /// <summary>表示 <c>Dialog_LC_EntityCodex</c> 类型。</summary>
    public class Dialog_LC_EntityCodex : Window
    {
        private Vector2 leftScrollPos;

        private Vector2 rightScrollPos;

        private float leftScrollHeight;

        private float rightScrollHeight;

        private Defs.AbnormalityCodexEntryDef selectedEntry;

        private List<AbnormalityCategoryDef> categoriesInOrder;

        private Dictionary<AbnormalityCategoryDef, List<Defs.AbnormalityCodexEntryDef>> entriesByCategory = new Dictionary<AbnormalityCategoryDef, List<Defs.AbnormalityCodexEntryDef>>();

        private Dictionary<AbnormalityCategoryDef, float> categoryRectSizes = new Dictionary<AbnormalityCategoryDef, float>();

        private bool devShowAll;

        private static readonly Vector2 ButSize = new Vector2(150f, 38f);

        private const float HeaderHeight = 30f;

        private const float LeftRectWidthPct = 0.35f;

        private const float EntrySize = 74f;

        private const float EntryGap = 10f;

        private const int MaxEntriesPerRow = 7;

        /// <inheritdoc />
        public override Vector2 InitialSize => new Vector2(980f, 724f);

        /// <summary>初始化 <c>Dialog_LC_EntityCodex</c> 类的新实例。</summary>
        public Dialog_LC_EntityCodex(AbnormalityCodexEntryDef selectedEntry = null)
        {
            doCloseX = true;
            doCloseButton = true;
            forcePause = true;
            categoriesInOrder = (from x in DefDatabase<AbnormalityCategoryDef>.AllDefsListForReading
                                 where DefDatabase<AbnormalityCodexEntryDef>.AllDefs.Any((AbnormalityCodexEntryDef y) => y.category == x && y.Visible)
                                 orderby x.listOrder
                                 select x).ToList();
            foreach (AbnormalityCategoryDef item in categoriesInOrder)
            {
                entriesByCategory.Add(item, new List<AbnormalityCodexEntryDef>());
                categoryRectSizes.Add(item, 0f);
            }

            foreach (AbnormalityCodexEntryDef item2 in DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading)
            {
                if (item2.Visible)
                {
                    entriesByCategory[item2.category].Add(item2);
                }
            }

            foreach (KeyValuePair<AbnormalityCategoryDef, List<AbnormalityCodexEntryDef>> item3 in entriesByCategory)
            {
                item3.Deconstruct(out var _, out var value);
                value.SortBy((AbnormalityCodexEntryDef e) => e.orderInCategory, (AbnormalityCodexEntryDef e) => e.label);
            }

            this.selectedEntry = selectedEntry
                ?? DefDatabase<AbnormalityCodexEntryDef>.AllDefs
                    .Where(x => x.Visible)
                    .OrderBy(x => x.category?.listOrder ?? int.MaxValue)
                    .ThenBy(x => x.orderInCategory)
                    .ThenBy(x => x.label)
                    .FirstOrDefault(x => x.Discovered)
                ?? DefDatabase<AbnormalityCodexEntryDef>.AllDefs
                    .Where(x => x.Visible)
                    .OrderBy(x => x.category?.listOrder ?? int.MaxValue)
                    .ThenBy(x => x.orderInCategory)
                    .ThenBy(x => x.label)
                    .FirstOrDefault();
        }

        /// <inheritdoc />
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Rect rect = inRect;
            rect.height -= ButSize.y + 10f;
            using (new TextBlock(GameFont.Medium))
            {
                Widgets.Label(new Rect(0f, 0f, rect.width, 30f), "LC_EntityCodexLabel".Translate());
            }

            if (Prefs.DevMode && DebugSettings.godMode)
            {
                Widgets.CheckboxLabeled(new Rect(rect.xMax - 150f, 0f, 150f, 30f), "DEV: Show all", ref devShowAll, disabled: false, null, null, placeCheckboxNearText: true);
            }

            rect.yMin += 40f;
            TaggedString taggedString = "LC_EntityCodexDesc".Translate();
            float num = Text.CalcHeight(taggedString, rect.width);
            Widgets.Label(new Rect(0f, rect.y, rect.width, num), taggedString);
            rect.yMin += num + 10f;
            Rect inRect2 = rect.LeftPart(0.35f);
            Rect rect2 = rect.RightPart(0.65f);
            LeftRect(inRect2);
            RightRect(rect2);
        }

        private void LeftRect(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, leftScrollHeight);
            Widgets.BeginScrollView(inRect, ref leftScrollPos, viewRect);
            if (selectedEntry != null)
            {
                float num = 0f;
                bool flag = devShowAll || selectedEntry.Discovered;
                using (new TextBlock(GameFont.Medium))
                {
                    Widgets.Label(new Rect(0f, num, viewRect.width, 30f), flag ? selectedEntry.LabelCap : "LC_UndiscoveredEntity".Translate());
                    num += 36f;
                }

                if (flag && selectedEntry.category != null)
                {
                    using (new TextBlock(GameFont.Tiny, ColoredText.SubtleGrayColor))
                    {
                        Widgets.Label(new Rect(0f, num, viewRect.width, 20f), selectedEntry.category.LabelCap);
                        num += 22f;
                    }
                }

                using (new TextBlock(newWordWrap: true))
                {
                    string text = flag ? selectedEntry.description : ((string)"LC_UndiscoveredEntityDesc".Translate());
                    float num2 = Text.CalcHeight(text, viewRect.width);
                    Widgets.Label(new Rect(0f, num, viewRect.width, num2), text);
                    num += num2 + 12f;
                }

                if (flag)
                {
                    ThingDef primaryDef = AbnormalityUtility.ResolvePrimaryAbnormalityDef(selectedEntry);
                    AnomalyStatusSaved saved = default;
                    bool hasSaved = primaryDef != null && Components.LC != null;
                    if (hasSaved)
                    {
                        Components.LC.TryGetAnomalyStatusSaved(primaryDef, out saved);
                    }

                    num = DrawManagementSummary(viewRect.width, num, primaryDef, hasSaved, saved);
                    num = DrawLinkedEntries(viewRect.width, num, primaryDef);
                    num = DrawResearchUnlocks(viewRect.width, num);
                    num = DrawStudyNotes(viewRect.width, num, primaryDef, hasSaved ? saved.StudyProgress : 0);
                }
                else if (Prefs.DevMode && DebugSettings.godMode)
                {
                    if (Widgets.ButtonText(new Rect(0f, num, viewRect.width, ButSize.y), "DEV: Discover"))
                    {
                        selectedEntry.Discovered = true;
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }

                    num += ButSize.y + 8f;
                }

                leftScrollHeight = num;
            }

            Widgets.EndScrollView();
        }

        private float DrawManagementSummary(float width, float y, ThingDef primaryDef, bool hasSaved, AnomalyStatusSaved saved)
        {
            var studyProps = primaryDef?.GetCompProperties<CompProperties_AbnormalityStudyUnlocks>();
            var egoProps = primaryDef?.GetCompProperties<CompProperties_EgoExtractable>();
            bool hasPeBox = hasSaved && primaryDef != null && primaryDef.HasComp(typeof(CompPeBoxProduce));
            if (!hasPeBox && studyProps == null && egoProps == null)
            {
                return y;
            }

            DrawSectionHeader(width, ref y, "LC_Codex_ManagementSummary".Translate());

            if (studyProps != null)
            {
                int maxNotes = studyProps.studyNotes?.Count ?? 0;
                int progress = hasSaved ? Mathf.Min(saved.StudyProgress, maxNotes) : 0;
                DrawSummaryLine(width, ref y, "LC_Codex_ObservationLevel".Translate($"{progress} / {maxNotes}"));
            }

            if (hasPeBox)
            {
                DrawSummaryLine(width, ref y, "LC_Codex_IndiPeBox".Translate(saved.IndiPeBoxAmount));
            }

            if (egoProps != null)
            {
                if (egoProps.weaponExtracted != null)
                {
                    int extracted = hasSaved ? saved.ExtractedEgoWeaponAmount : 0;
                    DrawSummaryLine(width, ref y, "LC_Codex_ExtractedWeapon".Translate($"{extracted} / {egoProps.amountMaxWeapon}"));
                }
                if (egoProps.armorExtracted != null)
                {
                    int extracted = hasSaved ? saved.ExtractedEgoArmorAmount : 0;
                    DrawSummaryLine(width, ref y, "LC_Codex_ExtractedArmor".Translate($"{extracted} / {egoProps.amountMaxArmor}"));
                }
            }

            return y + 10f;
        }

        private float DrawLinkedEntries(float width, float y, ThingDef primaryDef)
        {
            List<ThingDef> linked = CollectLinkedThings(primaryDef);
            if (linked.Count == 0)
            {
                return y;
            }

            DrawSectionHeader(width, ref y, "LC_Codex_LinkedEntries".Translate());
            foreach (ThingDef thingDef in linked)
            {
                Rect row = new Rect(0f, y, width, Text.LineHeight);
                Widgets.HyperlinkWithIcon(row, new Dialog_InfoCard.Hyperlink(thingDef));
                y += row.height + 2f;
            }

            return y + 10f;
        }

        private List<ThingDef> CollectLinkedThings(ThingDef primaryDef)
        {
            List<ThingDef> results = new List<ThingDef>();
            if (selectedEntry.linkedThings != null)
            {
                foreach (ThingDef t in selectedEntry.linkedThings)
                {
                    if (t != null && !results.Contains(t))
                    {
                        results.Add(t);
                    }
                }
            }

            if (primaryDef != null)
            {
                var egoProps = primaryDef.GetCompProperties<CompProperties_EgoExtractable>();
                if (egoProps != null)
                {
                    if (egoProps.weaponExtracted != null && !results.Contains(egoProps.weaponExtracted))
                    {
                        results.Add(egoProps.weaponExtracted);
                    }
                    if (egoProps.armorExtracted != null && !results.Contains(egoProps.armorExtracted))
                    {
                        results.Add(egoProps.armorExtracted);
                    }
                }

                var accProps = primaryDef.GetCompProperties<CompProperties_Accessoryable>();
                if (accProps?.accessoryDef != null && !results.Contains(accProps.accessoryDef))
                {
                    results.Add(accProps.accessoryDef);
                }
            }

            return results;
        }

        private float DrawResearchUnlocks(float width, float y)
        {
            if (selectedEntry.discoveredResearchProjects.NullOrEmpty())
            {
                return y;
            }

            bool anyValid = selectedEntry.discoveredResearchProjects.Any(p => p != null);
            if (!anyValid)
            {
                return y;
            }

            DrawSectionHeader(width, ref y, "LC_ResearchUnlocks".Translate());
            foreach (ResearchProjectDef project in selectedEntry.discoveredResearchProjects)
            {
                if (project == null)
                {
                    continue;
                }

                Rect row = new Rect(0f, y, width, Text.LineHeight);
                if (Widgets.ButtonText(row, "ViewHyperlink".Translate(project.LabelCap), drawBackground: false))
                {
                    Close();
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
                    if (MainButtonDefOf.Research.TabWindow is MainTabWindow_Research researchWindow)
                    {
                        researchWindow.Select(project);
                    }
                }
                y += row.height + 2f;
            }

            return y + 10f;
        }

        private float DrawStudyNotes(float width, float y, ThingDef primaryDef, int savedProgress)
        {
            var studyProps = primaryDef?.GetCompProperties<CompProperties_AbnormalityStudyUnlocks>();
            if (studyProps == null || studyProps.studyNotes.NullOrEmpty())
            {
                return y;
            }

            DrawSectionHeader(width, ref y, "LC_Codex_StudyNotes".Translate());
            int visibleCount = devShowAll
                ? studyProps.studyNotes.Count
                : Mathf.Clamp(savedProgress, 0, studyProps.studyNotes.Count);

            if (visibleCount <= 0)
            {
                using (new TextBlock(ColoredText.SubtleGrayColor))
                {
                    string emptyText = "LC_Codex_NoStudyNotes".Translate();
                    float h = Text.CalcHeight(emptyText, width);
                    Widgets.Label(new Rect(0f, y, width, h), emptyText);
                    y += h + 6f;
                }
                return y;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                StudyNote note = studyProps.studyNotes[i];
                if (note == null)
                {
                    continue;
                }

                string noteLabel = note.label ?? string.Empty;
                using (new TextBlock(GameFont.Small, ColoredText.TipSectionTitleColor))
                {
                    float lh = Text.CalcHeight(noteLabel, width);
                    Widgets.Label(new Rect(0f, y, width, lh), noteLabel);
                    y += lh + 2f;
                }

                string noteText = note.text ?? string.Empty;
                using (new TextBlock(newWordWrap: true))
                {
                    float th = Text.CalcHeight(noteText, width);
                    Widgets.Label(new Rect(0f, y, width, th), noteText);
                    y += th + 10f;
                }
            }

            return y;
        }

        private static void DrawSectionHeader(float width, ref float y, string label)
        {
            using (new TextBlock(GameFont.Small, ColoredText.TipSectionTitleColor))
            {
                Widgets.Label(new Rect(0f, y, width, Text.LineHeight), label);
            }
            y += Text.LineHeight;
            Widgets.DrawLineHorizontal(0f, y, width);
            y += 6f;
        }

        private static void DrawSummaryLine(float width, ref float y, string text)
        {
            float h = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, h), text);
            y += h + 2f;
        }

        private void RightRect(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, rightScrollHeight);
            Widgets.BeginScrollView(rect, ref rightScrollPos, viewRect);
            float num = 0f;
            foreach (AbnormalityCategoryDef item in categoriesInOrder)
            {
                float num2 = num;
                float height = categoryRectSizes[item];
                GUI.color = new Color(1f, 1f, 1f, 0.5f);

                //绘制每行的灰色长条和Tip
                var rectBg = new Rect(0f, num, rect.width, height);
                Widgets.DrawHighlight(rectBg);
                GUI.color = Color.white;
                DrawToolTipRow(item, rectBg);

                Widgets.Label(new Rect(10f, num, rect.width, Text.LineHeight), item.LabelCap);
                num += Text.LineHeight + 4f;
                List<Defs.AbnormalityCodexEntryDef> list = entriesByCategory[item];
                int num3 = Mathf.Min(Mathf.CeilToInt(Mathf.Sqrt(list.Count)) + 1, 7);
                int num4 = Mathf.CeilToInt((float)list.Count / (float)num3);
                for (int i = 0; i < list.Count; i++)
                {
                    Defs.AbnormalityCodexEntryDef AbnormalityCodexEntryDef = list[i];
                    int num5 = i / num3;
                    int num6 = i % num3;
                    int num7 = ((i >= list.Count - list.Count % num3) ? (list.Count % num3) : num3);
                    float num8 = (viewRect.width - (float)num7 * 74f - (float)(num7 - 1) * 10f) / 2f;
                    Rect rect2 = new Rect(num8 + (float)num6 * 74f + (float)num6 * 10f, num + (float)num5 * 74f + (float)num5 * 10f, 74f, 74f);
                    bool flag = devShowAll || AbnormalityCodexEntryDef.Discovered;
                    DrawEntry(rect2, AbnormalityCodexEntryDef, flag);
                    if (flag)
                    {
                        Text.Font = GameFont.Tiny;
                        float num9 = Text.CalcHeight(AbnormalityCodexEntryDef.LabelCap, rect2.width);
                        Rect rect3 = new Rect(rect2.x, rect2.yMax - num9, rect2.width, num9);
                        Widgets.DrawBoxSolid(rect3, new Color(0f, 0f, 0f, 0.3f));
                        using (new TextBlock(TextAnchor.MiddleCenter))
                        {
                            Widgets.Label(rect3, AbnormalityCodexEntryDef.LabelCap);
                        }

                        Text.Font = GameFont.Small;
                    }
                }

                num += 10f + (float)num4 * 74f + (float)(num4 - 1) * 10f;
                categoryRectSizes[item] = num - num2;
                num += 10f;
            }

            rightScrollHeight = num;
            Widgets.EndScrollView();
        }

        private void DrawEntry(Rect rect, Defs.AbnormalityCodexEntryDef entry, bool discovered)
        {
            Widgets.DrawOptionBackground(rect, entry == selectedEntry);
            GUI.DrawTexture(rect.ContractedBy(2f), discovered ? entry.icon : entry.silhouette);
            if (Widgets.ButtonInvisible(rect))
            {
                selectedEntry = entry;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
        }

        /// <summary>
        /// 绘制Tip
        /// </summary>
        /// <param name="cat">类型</param>
        /// <param name="rect">rect</param>
        private void DrawToolTipRow(AbnormalityCategoryDef cat, Rect rect)
        {
            //Log.Message("鼠标进入区域");

            string labelCap = cat.label.Translate();
            labelCap += "\n\n" + ("LC_EntityCodexTip." + cat.defName).Translate();

            if (Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, labelCap);
        }
    }
}