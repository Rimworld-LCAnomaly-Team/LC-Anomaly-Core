﻿using LCAnomalyCore.Defs;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
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

        public override Vector2 InitialSize => new Vector2(980f, 724f);

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

            this.selectedEntry = selectedEntry ?? DefDatabase<AbnormalityCodexEntryDef>.AllDefs.OrderBy((AbnormalityCodexEntryDef x) => x.label).FirstOrDefault((Defs.AbnormalityCodexEntryDef x) => x.Discovered);
        }

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
                    Widgets.Label(new Rect(0f, num, viewRect.width, 30f), flag ? selectedEntry.LabelCap : "UndiscoveredEntity".Translate());
                    num += 40f;
                }

                using (new TextBlock(newWordWrap: true))
                {
                    string text = (flag ? selectedEntry.description : ((string)"UndiscoveredEntityDesc".Translate()));
                    float num2 = Text.CalcHeight(text, viewRect.width);
                    Widgets.Label(new Rect(0f, num, viewRect.width, num2), text);
                    num += num2 + 10f;
                }

                //if (flag)
                //{
                //    //if (selectedEntry.linkedThings.Count > 0)
                //    //{
                //    //    foreach (ThingDef linkedThing in selectedEntry.linkedThings)
                //    //    {
                //    //        Rect rect = new Rect(0f, num, viewRect.width, Text.LineHeight);
                //    //        if (devShowAll || Find.EntityCodex.Discovered(linkedThing))
                //    //        {
                //    //            Widgets.HyperlinkWithIcon(rect, new Dialog_InfoCard.Hyperlink(linkedThing));
                //    //        }
                //    //        else
                //    //        {
                //    //            rect.xMin += rect.height;
                //    //            using (new TextBlock(ColoredText.SubtleGrayColor))
                //    //            {
                //    //                Widgets.Label(rect, "Undiscovered".Translate());
                //    //            }
                //    //        }

                //    //        num += rect.height;
                //    //    }

                //    //    num += 10f;
                //    //}

                //    //if (selectedEntry.discoveredResearchProjects.Count > 0)
                //    //{
                //    //    Widgets.Label(new Rect(0f, num, viewRect.width, Text.LineHeight), "ResearchUnlocks".Translate() + ":");
                //    //    num += Text.LineHeight;
                //    //    foreach (ResearchProjectDef discoveredResearchProject in selectedEntry.discoveredResearchProjects)
                //    //    {
                //    //        Rect rect2 = new Rect(0f, num, viewRect.width, Text.LineHeight);
                //    //        if (Widgets.ButtonText(rect2, "ViewHyperlink".Translate(discoveredResearchProject.LabelCap), drawBackground: false))
                //    //        {
                //    //            Close();
                //    //            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
                //    //            ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).Select(discoveredResearchProject);
                //    //        }

                //    //        num += rect2.height;
                //    //    }
                //    //}
                //}
                //else if (Prefs.DevMode && DebugSettings.godMode)
                //{
                //    if (Widgets.ButtonText(new Rect(0f, num, viewRect.width, ButSize.y), "DEV: Discover"))
                //    {
                //        if (!selectedEntry.linkedThings.NullOrEmpty())
                //        {
                //            for (int i = 0; i < selectedEntry.linkedThings.Count; i++)
                //            {
                //                Find.EntityCodex.SetDiscovered((AbnormalityCodexEntryDef)selectedEntry, selectedEntry.linkedThings[i]);
                //            }
                //        }
                //        else
                //        {
                //            Find.EntityCodex.SetDiscovered((AbnormalityCodexEntryDef)selectedEntry);
                //        }
                //    }

                //    num += ButSize.y;
                //}

                leftScrollHeight = num;
            }

            Widgets.EndScrollView();
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