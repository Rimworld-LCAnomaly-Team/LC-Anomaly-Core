using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Defs;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Misc;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Patch;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 脑叶公司独立异想体与考验管理档案终端：
    /// 采用脑叶公司黑曜金红视觉终端皮肤，支持异想体/工具/考验三页签、PE-Box 渐进情报解密、工作偏好矩阵、四色抗性档案与全图鉴里程碑。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Dialog_LC_EntityCodex : Window
    {
        private static readonly Texture2D TexWindowBg = ContentFinder<Texture2D>.Get("UI/Codex/Codex_WindowBg", false);
        private static readonly Texture2D TexHeaderBanner = ContentFinder<Texture2D>.Get("UI/Codex/Codex_HeaderBanner", false);
        private static readonly Texture2D TexDossierBanner = ContentFinder<Texture2D>.Get("UI/Codex/Codex_DossierBanner", false);
        private static readonly Texture2D TexPortraitFrame = ContentFinder<Texture2D>.Get("UI/Codex/Codex_PortraitFrame", false);
        private static readonly Texture2D TexPanelFrame = ContentFinder<Texture2D>.Get("UI/Codex/Codex_PanelFrame", false);
        private static readonly Texture2D TexSectionBar = ContentFinder<Texture2D>.Get("UI/Codex/Codex_SectionBar", false);
        private static readonly Texture2D TexTabNormal = ContentFinder<Texture2D>.Get("UI/Codex/Codex_TabNormal", false);
        private static readonly Texture2D TexTabActive = ContentFinder<Texture2D>.Get("UI/Codex/Codex_TabActive", false);
        private static readonly Texture2D TexTileFrame = ContentFinder<Texture2D>.Get("UI/Codex/Codex_TileFrame", false);
        private static readonly Texture2D TexTileSelected = ContentFinder<Texture2D>.Get("UI/Codex/Codex_TileSelected", false);
        private static readonly Texture2D TexLockedCard = ContentFinder<Texture2D>.Get("UI/Codex/Codex_LockedCard", false);
        private static readonly Texture2D TexLockIcon = ContentFinder<Texture2D>.Get("UI/Codex/Codex_LockIcon", false);
        private static readonly Texture2D TexPeBoxIcon = ContentFinder<Texture2D>.Get("UI/Codex/Codex_PeBoxIcon", false);
        private static readonly Texture2D TexHazardDivider = ContentFinder<Texture2D>.Get("UI/Codex/Codex_HazardDivider", false);

        private static readonly Texture2D TexWorkInstinct = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Instinct", false);
        private static readonly Texture2D TexWorkInsight = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Insight", false);
        private static readonly Texture2D TexWorkAttachment = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Attachment", false);
        private static readonly Texture2D TexWorkRepression = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Repression", false);

        private Vector2 leftScrollPos;
        private Vector2 rightScrollPos;
        private float leftScrollHeight;
        private float rightScrollHeight;

        private AbnormalityCodexEntryDef selectedEntry;
        private CodexEntryKind activeTab = CodexEntryKind.Abnormality;
        private bool devShowAll;

        private readonly List<AbnormalityCategoryDef> categoriesInOrder;
        private readonly Dictionary<CodexEntryKind, Dictionary<AbnormalityCategoryDef, List<AbnormalityCodexEntryDef>>> entriesByTab
            = new Dictionary<CodexEntryKind, Dictionary<AbnormalityCategoryDef, List<AbnormalityCodexEntryDef>>>();
        private readonly Dictionary<AbnormalityCategoryDef, float> categoryRectSizes = new Dictionary<AbnormalityCategoryDef, float>();

        private static readonly CodexEntryKind[] OrderedTabs =
        {
            CodexEntryKind.Abnormality,
            CodexEntryKind.Tool,
            CodexEntryKind.Ordeal
        };

        private static readonly FourColorDamageType[] FourColors =
        {
            FourColorDamageType.Red,
            FourColorDamageType.White,
            FourColorDamageType.Black,
            FourColorDamageType.Pale
        };

        private static readonly Vector2 ButSize = new Vector2(150f, 34f);
        private const float EntryTileSize = 84f;
        private const float EntryTileLabelHeight = 26f;
        private const float EntryPipHeight = 5f;
        private const float EntryTileGap = 8f;
        private const int MaxEntriesPerRow = 6;

        private static readonly Color PanelBgFallback = new Color(0.06f, 0.07f, 0.09f, 0.94f);
        private static readonly Color CardBgColor = new Color(0.09f, 0.11f, 0.14f, 0.90f);
        private static readonly Color LockedCardBgFallback = new Color(0.11f, 0.06f, 0.08f, 0.90f);
        private static readonly Color BorderSubtleColor = new Color(0.35f, 0.32f, 0.24f, 0.72f);
        private static readonly Color GoldAccentColor = new Color(0.95f, 0.84f, 0.44f, 1f);
        private static readonly Color CrimsonAccentColor = new Color(0.90f, 0.22f, 0.22f, 1f);

        /// <inheritdoc />
        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1120f, Verse.UI.screenWidth > 100 ? Verse.UI.screenWidth - 40f : 1120f),
            Mathf.Min(760f, Verse.UI.screenHeight > 100 ? Verse.UI.screenHeight - 40f : 760f));

        /// <inheritdoc />
        protected override float Margin => 0f;

        /// <summary>初始化 <c>Dialog_LC_EntityCodex</c> 类的新实例。</summary>
        public Dialog_LC_EntityCodex(AbnormalityCodexEntryDef selectedEntry = null)
        {
            doWindowBackground = false;
            drawShadow = true;
            doCloseX = false;
            doCloseButton = false;
            forcePause = true;

            categoriesInOrder = DefDatabase<AbnormalityCategoryDef>.AllDefsListForReading
                .OrderBy(c => c.listOrder)
                .ToList();

            foreach (CodexEntryKind kind in OrderedTabs)
            {
                var catDict = new Dictionary<AbnormalityCategoryDef, List<AbnormalityCodexEntryDef>>();
                foreach (AbnormalityCategoryDef cat in categoriesInOrder)
                {
                    catDict[cat] = new List<AbnormalityCodexEntryDef>();
                }
                entriesByTab[kind] = catDict;
            }

            foreach (AbnormalityCategoryDef cat in categoriesInOrder)
            {
                categoryRectSizes[cat] = 0f;
            }

            foreach (AbnormalityCodexEntryDef entry in DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading)
            {
                if (entry == null || !entry.Visible || entry.category == null)
                {
                    continue;
                }

                CodexEntryKind kind = entry.ResolvedEntryKind;
                if (!entriesByTab.TryGetValue(kind, out var catDict))
                {
                    kind = CodexEntryKind.Abnormality;
                    catDict = entriesByTab[kind];
                }

                if (!catDict.ContainsKey(entry.category))
                {
                    catDict[entry.category] = new List<AbnormalityCodexEntryDef>();
                    if (!categoriesInOrder.Contains(entry.category))
                    {
                        categoriesInOrder.Add(entry.category);
                        categoryRectSizes[entry.category] = 0f;
                    }
                }

                catDict[entry.category].Add(entry);
            }

            foreach (var tabPair in entriesByTab)
            {
                foreach (var catPair in tabPair.Value)
                {
                    catPair.Value.SortBy(e => e.orderInCategory, e => e.label);
                }
            }

            if (selectedEntry != null)
            {
                this.selectedEntry = selectedEntry;
                activeTab = selectedEntry.ResolvedEntryKind;
                if (activeTab == CodexEntryKind.Auto)
                {
                    activeTab = CodexEntryKind.Abnormality;
                }
            }
            else
            {
                activeTab = CodexEntryKind.Abnormality;
                this.selectedEntry = FindPreferredEntryForTab(activeTab)
                    ?? DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading.FirstOrDefault(e => e != null && e.Visible);
                if (this.selectedEntry != null)
                {
                    activeTab = this.selectedEntry.ResolvedEntryKind;
                }
            }
        }

        private AbnormalityCodexEntryDef FindPreferredEntryForTab(CodexEntryKind kind)
        {
            if (!entriesByTab.TryGetValue(kind, out var catDict))
            {
                return null;
            }

            AbnormalityCodexEntryDef firstVisible = null;
            for (int c = 0; c < categoriesInOrder.Count; c++)
            {
                if (!catDict.TryGetValue(categoriesInOrder[c], out var list))
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    AbnormalityCodexEntryDef entry = list[i];
                    if (firstVisible == null)
                    {
                        firstVisible = entry;
                    }

                    if (entry.Discovered)
                    {
                        return entry;
                    }
                }
            }

            return firstVisible;
        }

        private int CountEntriesInTab(CodexEntryKind kind, out int discoveredCount)
        {
            discoveredCount = 0;
            int total = 0;
            if (!entriesByTab.TryGetValue(kind, out var catDict))
            {
                return 0;
            }

            foreach (var list in catDict.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    total++;
                    if (devShowAll || list[i].Discovered)
                    {
                        discoveredCount++;
                    }
                }
            }

            return total;
        }

        /// <inheritdoc />
        public override void DoWindowContents(Rect inRect)
        {
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWrap = Text.WordWrap;
            Color prevColor = GUI.color;

            try
            {
                // 全屏脑叶黑曜控制台底图
                if (TexWindowBg != null)
                {
                    GUI.DrawTexture(inRect, TexWindowBg, ScaleMode.StretchToFill);
                }
                else
                {
                    Widgets.DrawBoxSolidWithOutline(inRect, new Color(0.05f, 0.06f, 0.08f, 0.98f), GoldAccentColor);
                }

                Rect workRect = inRect.ContractedBy(14f);

                // 顶部横幅与里程碑条
                DrawHeaderBar(new Rect(workRect.x, workRect.y, workRect.width, 76f));
                workRect.yMin += 82f;

                // 页签栏 + DEV 按钮
                DrawTabStrip(new Rect(workRect.x, workRect.y, workRect.width, 34f));
                workRect.yMin += 40f;

                // 底部关闭栏
                Rect footerRect = new Rect(workRect.x, workRect.yMax - ButSize.y, workRect.width, ButSize.y);
                workRect.height -= ButSize.y + 8f;

                // 左右主分栏（脑叶斜切面板框架）
                float leftWidth = Mathf.Floor(workRect.width * 0.46f);
                float gap = 12f;
                Rect leftPanelRect = new Rect(workRect.x, workRect.y, leftWidth, workRect.height);
                Rect rightPanelRect = new Rect(leftPanelRect.xMax + gap, workRect.y, workRect.width - leftWidth - gap, workRect.height);

                DrawLobCorpPanel(leftPanelRect);
                DrawLobCorpPanel(rightPanelRect);

                DrawLeftDossier(leftPanelRect.ContractedBy(10f));
                DrawRightCatalog(rightPanelRect.ContractedBy(10f));

                DrawFooterBar(footerRect);
            }
            finally
            {
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                Text.WordWrap = prevWrap;
                GUI.color = prevColor;
            }
        }

        private static void DrawLobCorpPanel(Rect rect)
        {
            if (TexPanelFrame != null)
            {
                GUI.DrawTexture(rect, TexPanelFrame, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(rect, PanelBgFallback, BorderSubtleColor);
            }
        }

        private void DrawHeaderBar(Rect rect)
        {
            if (TexHeaderBanner != null)
            {
                GUI.DrawTexture(rect, TexHeaderBanner, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(rect, CardBgColor, CrimsonAccentColor);
            }

            float closeBtnSize = 26f;
            float milestoneWidth = Mathf.Min(350f, rect.width * 0.36f);
            Rect closeRect = new Rect(rect.xMax - closeBtnSize - 6f, rect.y + 6f, closeBtnSize, closeBtnSize);
            Rect milestoneRect = new Rect(closeRect.x - milestoneWidth - 10f, rect.y + 6f, milestoneWidth, rect.height - 14f);
            Rect titleArea = new Rect(rect.x + 18f, rect.y + 6f, milestoneRect.x - rect.x - 28f, rect.height - 14f);

            using (new TextBlock(GameFont.Medium, Color.white))
            {
                Widgets.Label(new Rect(titleArea.x, titleArea.y, titleArea.width, 30f), "LC_EntityCodexLabel".Translate());
            }

            using (new TextBlock(GameFont.Tiny, new Color(0.84f, 0.86f, 0.90f)))
            using (new TextBlock(newWordWrap: true))
            {
                Widgets.Label(new Rect(titleArea.x, titleArea.y + 30f, titleArea.width, 34f), "LC_EntityCodexDesc".Translate());
            }

            DrawMilestoneWidget(milestoneRect);

            bool hoverClose = Mouse.IsOver(closeRect);
            Widgets.DrawBoxSolidWithOutline(closeRect,
                hoverClose ? new Color(0.45f, 0.10f, 0.10f, 0.95f) : new Color(0.14f, 0.08f, 0.09f, 0.90f),
                hoverClose ? GoldAccentColor : CrimsonAccentColor);
            using (new TextBlock(GameFont.Small, hoverClose ? GoldAccentColor : Color.white))
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(closeRect, "×");
            }
            if (Widgets.ButtonInvisible(closeRect))
            {
                Close();
            }
        }

        private void DrawFooterBar(Rect rect)
        {
            Rect closeBtnRect = new Rect(rect.center.x - ButSize.x * 0.5f, rect.y, ButSize.x, ButSize.y);
            DrawStyledLobCorpButton(closeBtnRect, "CloseButton".Translate(), activeLook: true);
            if (Widgets.ButtonInvisible(closeBtnRect))
            {
                Close();
            }
        }

        private static void DrawStyledLobCorpButton(Rect rect, string label, bool activeLook, bool disabled = false, Texture2D leftIcon = null)
        {
            bool hovered = !disabled && Mouse.IsOver(rect);
            Texture2D tex = (activeLook || hovered) ? TexTabActive : TexTabNormal;

            Color prev = GUI.color;
            if (disabled)
            {
                GUI.color = new Color(0.55f, 0.55f, 0.58f, 0.72f);
            }
            else if (hovered)
            {
                GUI.color = new Color(1.12f, 1.12f, 1.05f, 1f);
            }

            if (tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(rect, activeLook ? new Color(0.20f, 0.09f, 0.11f) : CardBgColor, GoldAccentColor);
            }

            GUI.color = prev;

            float textLeft = rect.x + 8f;
            if (leftIcon != null)
            {
                float iconS = Mathf.Min(18f, rect.height - 8f);
                GUI.DrawTexture(new Rect(rect.x + 12f, rect.center.y - iconS * 0.5f, iconS, iconS), leftIcon, ScaleMode.ScaleToFit);
                textLeft = rect.x + 16f + iconS;
            }

            Color textCol = disabled
                ? ColoredText.SubtleGrayColor
                : ((activeLook || hovered) ? GoldAccentColor : Color.white);
            using (new TextBlock(GameFont.Small, textCol))
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(new Rect(textLeft, rect.y, rect.xMax - textLeft - 6f, rect.height), label);
            }
        }

        private void DrawMilestoneWidget(Rect rect)
        {
            DrawLobCorpPanel(rect);
            CodexCompletionSnapshot snap = CodexIntelUtility.GetCompletionSnapshot();
            Rect inner = rect.ContractedBy(6f);

            float textLeft = inner.x;
            if (TexPeBoxIcon != null)
            {
                GUI.DrawTexture(new Rect(inner.x, inner.y, 16f, 16f), TexPeBoxIcon, ScaleMode.ScaleToFit);
                textLeft += 20f;
            }

            using (new TextBlock(GameFont.Tiny))
            {
                string headerLeft = "LC_Codex_ArchiveProgress".Translate($"{snap.completionPercent * 100f:0.#}%", snap.unlockedSlots, snap.totalSlots);
                string tierText = "LC_Codex_MilestoneTierLabel".Translate(snap.milestoneTier);
                Widgets.Label(new Rect(textLeft, inner.y, inner.xMax - textLeft - 85f, 18f), headerLeft);
                using (new TextBlock(GameFont.Tiny, snap.milestoneTier > 0 ? GoldAccentColor : ColoredText.SubtleGrayColor))
                using (new TextBlock(TextAnchor.UpperRight))
                {
                    Widgets.Label(new Rect(inner.x, inner.y, inner.width, 18f), tierText);
                }
            }

            Rect barRect = new Rect(inner.x, inner.y + 20f, inner.width, 10f);
            Widgets.DrawBoxSolidWithOutline(barRect, new Color(0.06f, 0.07f, 0.09f, 1f), BorderSubtleColor);
            if (snap.completionPercent > 0.001f)
            {
                Rect fillRect = new Rect(barRect.x + 1f, barRect.y + 1f, Mathf.Max(2f, (barRect.width - 2f) * snap.completionPercent), barRect.height - 2f);
                Color barColor = snap.milestoneTier >= 4 ? GoldAccentColor : new Color(0.90f, 0.28f, 0.28f, 1f);
                Widgets.DrawBoxSolid(fillRect, barColor);
            }

            float[] thresholds = { 0.25f, 0.50f, 0.75f, 1.00f };
            float pipY = barRect.yMax + 4f;
            float pipWidth = (inner.width - 12f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                bool active = snap.milestoneTier >= (i + 1);
                Rect badgeRect = new Rect(inner.x + i * (pipWidth + 4f), pipY, pipWidth, 16f);
                Widgets.DrawBoxSolidWithOutline(badgeRect,
                    active ? new Color(0.35f, 0.22f, 0.08f, 0.95f) : new Color(0.08f, 0.09f, 0.11f, 0.92f),
                    active ? GoldAccentColor : BorderSubtleColor);
                using (new TextBlock(GameFont.Tiny, active ? GoldAccentColor : ColoredText.SubtleGrayColor))
                using (new TextBlock(TextAnchor.MiddleCenter))
                {
                    Widgets.Label(badgeRect, $"{Mathf.RoundToInt(thresholds[i] * 100f)}%");
                }
            }

            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, BuildMilestoneTooltip(snap));
            }
        }

        private static string BuildMilestoneTooltip(CodexCompletionSnapshot snap)
        {
            string s1 = snap.milestoneTier >= 1 ? "LC_Codex_MilestoneActive".Translate().ToString() : "LC_Codex_MilestoneLocked".Translate().ToString();
            string s2 = snap.milestoneTier >= 2 ? "LC_Codex_MilestoneActive".Translate().ToString() : "LC_Codex_MilestoneLocked".Translate().ToString();
            string s3 = snap.milestoneTier >= 3 ? "LC_Codex_MilestoneActive".Translate().ToString() : "LC_Codex_MilestoneLocked".Translate().ToString();
            string s4 = snap.milestoneTier >= 4 ? "LC_Codex_MilestoneActive".Translate().ToString() : "LC_Codex_MilestoneLocked".Translate().ToString();

            return "LC_Codex_MilestoneTooltip".Translate(
                snap.discoveredEntries,
                snap.totalEntries,
                snap.completedEntries,
                s1, s2, s3, s4).ToString();
        }

        private void DrawTabStrip(Rect rect)
        {
            float x = rect.x;
            for (int i = 0; i < OrderedTabs.Length; i++)
            {
                CodexEntryKind kind = OrderedTabs[i];
                int total = CountEntriesInTab(kind, out int discovered);
                if (total == 0 && kind != CodexEntryKind.Abnormality)
                {
                    continue;
                }

                string key;
                switch (kind)
                {
                    case CodexEntryKind.Tool: key = "LC_Codex_Tab_Tools"; break;
                    case CodexEntryKind.Ordeal: key = "LC_Codex_Tab_Ordeals"; break;
                    default: key = "LC_Codex_Tab_Abnormalities"; break;
                }

                string label = $"{key.Translate()} ({discovered}/{total})";
                float btnWidth = Mathf.Max(154f, Text.CalcSize(label).x + 34f);
                Rect tabRect = new Rect(x, rect.y, btnWidth, rect.height);
                bool selected = activeTab == kind;
                DrawStyledLobCorpButton(tabRect, label, activeLook: selected);

                if (Widgets.ButtonInvisible(tabRect))
                {
                    SwitchTab(kind);
                }

                x += btnWidth + 8f;
            }

            if (Prefs.DevMode && DebugSettings.godMode)
            {
                float rightX = rect.xMax;

                Rect peBtnRect = new Rect(rightX - 108f, rect.y + 3f, 108f, rect.height - 6f);
                if (selectedEntry != null && selectedEntry.PrimaryEntityDef != null && Components.LC != null)
                {
                    if (Widgets.ButtonText(peBtnRect, "DEV: +50 PE"))
                    {
                        ThingDef holder = CodexIntelUtility.GetIndiPeBoxHolderKey(selectedEntry, Components.LC) ?? selectedEntry.PrimaryEntityDef;
                        Components.LC.TryGetAnomalyStatusSaved(holder, out AnomalyStatusSaved saved);
                        saved.IndiPeBoxAmount += 50;
                        Components.LC.AnomalyStatusSavedDict[holder] = saved;
                        RimWorld.SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }
                    rightX -= 114f;
                }

                Rect unlockAllRect = new Rect(rightX - 122f, rect.y + 3f, 122f, rect.height - 6f);
                if (Widgets.ButtonText(unlockAllRect, "DEV: Unlock all"))
                {
                    Components.LC?.UnlockAllCodexIntel();
                    RimWorld.SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
                rightX -= 128f;

                Rect showAllRect = new Rect(rightX - 118f, rect.y + 3f, 118f, rect.height - 6f);
                Widgets.CheckboxLabeled(showAllRect, "DEV: Show all", ref devShowAll, placeCheckboxNearText: true);
            }
        }

        private void SwitchTab(CodexEntryKind kind)
        {
            if (activeTab == kind)
            {
                return;
            }

            activeTab = kind;
            rightScrollPos = Vector2.zero;
            leftScrollPos = Vector2.zero;
            if (selectedEntry == null || selectedEntry.ResolvedEntryKind != kind)
            {
                selectedEntry = FindPreferredEntryForTab(kind);
            }

            RimWorld.SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        private void DrawLeftDossier(Rect inRect)
        {
            if (selectedEntry == null)
            {
                using (new TextBlock(GameFont.Small, ColoredText.SubtleGrayColor))
                using (new TextBlock(TextAnchor.MiddleCenter))
                {
                    Widgets.Label(inRect, "LC_Codex_NoEntrySelected".Translate());
                }
                return;
            }

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(inRect.height, leftScrollHeight));
            Widgets.BeginScrollView(inRect, ref leftScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;
            GameComponent_LC lc = Components.LC;
            bool discovered = devShowAll || selectedEntry.Discovered;
            CodexEntryKind kind = selectedEntry.ResolvedEntryKind;
            ThingDef primaryDef = selectedEntry.PrimaryEntityDef;

            if (!discovered)
            {
                DrawUndiscoveredDossier(width, ref y);
            }
            else
            {
                bool hasBasic = devShowAll || (lc?.HasCodexIntel(selectedEntry, CodexIntelSection.BasicInfo) ?? false);
                bool hasWork = devShowAll || (lc?.HasCodexIntel(selectedEntry, CodexIntelSection.WorkPreferences) ?? false);
                bool hasTips = devShowAll || (lc?.HasCodexIntel(selectedEntry, CodexIntelSection.ManagementTips) ?? false);
                bool hasCombat = devShowAll || (lc?.HasCodexIntel(selectedEntry, CodexIntelSection.EscapeCombat) ?? false);

                AnomalyStatusSaved saved = default;
                bool hasSaved = primaryDef != null && lc != null;
                if (hasSaved)
                {
                    lc.TryGetAnomalyStatusSaved(primaryDef, out saved);
                }

                DrawDossierHeaderCard(width, ref y, kind, hasBasic, lc);

                y = DrawBasicInfoSection(width, y, kind, primaryDef, hasBasic, lc);

                if (kind == CodexEntryKind.Abnormality)
                {
                    y = DrawWorkPreferenceSection(width, y, primaryDef, hasWork, lc);
                }

                if (kind == CodexEntryKind.Abnormality || kind == CodexEntryKind.Ordeal)
                {
                    y = DrawCombatAndEscapeSection(width, y, kind, primaryDef, hasCombat, lc);
                }

                if (kind == CodexEntryKind.Abnormality)
                {
                    y = DrawEgoSection(width, y, primaryDef, hasBasic, hasSaved, saved);
                }

                if (kind == CodexEntryKind.Abnormality || kind == CodexEntryKind.Tool)
                {
                    y = DrawManagementTipsSection(width, y, kind, primaryDef, hasTips, lc);
                }

                if (hasBasic)
                {
                    y = DrawResearchUnlocks(width, y);
                    y = DrawLinkedEntries(width, y, primaryDef);
                }
            }

            leftScrollHeight = y + 12f;
            Widgets.EndScrollView();
        }

        private void DrawUndiscoveredDossier(float width, ref float y)
        {
            Rect cardRect = new Rect(0f, y, width, 114f);
            if (TexDossierBanner != null)
            {
                GUI.DrawTexture(cardRect, TexDossierBanner, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(cardRect, CardBgColor, BorderSubtleColor);
            }

            Rect portraitSocket = new Rect(cardRect.x + 12f, cardRect.y + 12f, 88f, 88f);
            Texture2D iconTex = selectedEntry.silhouette ?? BaseContent.BadTex;
            GUI.DrawTexture(portraitSocket.ContractedBy(8f), iconTex, ScaleMode.ScaleToFit);
            if (TexPortraitFrame != null)
            {
                GUI.DrawTexture(portraitSocket, TexPortraitFrame, ScaleMode.StretchToFill);
            }

            float textX = portraitSocket.xMax + 14f;
            float textW = cardRect.xMax - textX - 12f;
            using (new TextBlock(GameFont.Medium))
            {
                Widgets.Label(new Rect(textX, cardRect.y + 14f, textW, 30f), "LC_UndiscoveredEntity".Translate());
            }

            if (selectedEntry.category != null)
            {
                DrawRiskPill(new Rect(textX, cardRect.y + 50f, 84f, 22f), selectedEntry.category.defName, "???");
            }

            y = cardRect.yMax + 12f;

            using (new TextBlock(GameFont.Small, ColoredText.SubtleGrayColor))
            using (new TextBlock(newWordWrap: true))
            {
                string desc = "LC_UndiscoveredEntityDesc".Translate();
                float h = Text.CalcHeight(desc, width);
                Widgets.Label(new Rect(0f, y, width, h), desc);
                y += h + 12f;
            }

            if (Prefs.DevMode && DebugSettings.godMode)
            {
                if (Widgets.ButtonText(new Rect(0f, y, width, ButSize.y), "DEV: Discover"))
                {
                    selectedEntry.Discovered = true;
                    RimWorld.SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }

                y += ButSize.y + 8f;
            }
        }

        private void DrawDossierHeaderCard(float width, ref float y, CodexEntryKind kind, bool hasBasic, GameComponent_LC lc)
        {
            Rect cardRect = new Rect(0f, y, width, 114f);
            if (TexDossierBanner != null)
            {
                GUI.DrawTexture(cardRect, TexDossierBanner, ScaleMode.StretchToFill);
            }
            else
            {
                Color catColor = GetCategoryColor(selectedEntry.category?.defName);
                Widgets.DrawBoxSolidWithOutline(cardRect, CardBgColor, new Color(catColor.r, catColor.g, catColor.b, 0.55f));
            }

            Rect portraitSocket = new Rect(cardRect.x + 12f, cardRect.y + 12f, 88f, 88f);
            Texture2D tex = selectedEntry.icon ?? selectedEntry.silhouette ?? BaseContent.BadTex;
            Color prevColor = GUI.color;
            if (!hasBasic && !devShowAll)
            {
                GUI.color = new Color(0.76f, 0.80f, 0.88f, 1f);
            }
            GUI.DrawTexture(portraitSocket.ContractedBy(8f), tex, ScaleMode.ScaleToFit);
            GUI.color = prevColor;

            if (TexPortraitFrame != null)
            {
                GUI.DrawTexture(portraitSocket, TexPortraitFrame, ScaleMode.StretchToFill);
            }

            float textX = portraitSocket.xMax + 14f;
            float textW = cardRect.xMax - textX - 12f;

            string code = selectedEntry.ResolvedContainmentCode;
            using (new TextBlock(GameFont.Tiny, GoldAccentColor))
            {
                Widgets.Label(new Rect(textX, cardRect.y + 11f, textW - 88f, 18f), code);
            }

            bool showRiskGrade = hasBasic || kind == CodexEntryKind.Ordeal;
            DrawRiskPill(new Rect(cardRect.xMax - 92f, cardRect.y + 9f, 80f, 20f), selectedEntry.category?.defName, showRiskGrade ? selectedEntry.category?.LabelCap.ToString() : "???");

            string title = hasBasic ? selectedEntry.LabelCap.ToString() : "LC_Codex_MaskedTitle".Translate(code).ToString();
            using (new TextBlock(GameFont.Medium, Color.white))
            {
                Widgets.Label(new Rect(textX, cardRect.y + 32f, textW, 32f), title);
            }

            Rect statsRect = new Rect(textX, cardRect.y + 72f, textW, 28f);
            if (kind == CodexEntryKind.Ordeal)
            {
                int kills = lc?.GetOrdealKillCount(selectedEntry) ?? 0;
                string stage = selectedEntry.ordealStage.NullOrEmpty() ? "-" : selectedEntry.ordealStage;
                using (new TextBlock(GameFont.Tiny, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(statsRect, "LC_Codex_OrdealHeaderStats".Translate(kills, stage));
                }
            }
            else
            {
                int maxNotes = CodexIntelUtility.GetTotalStudyNotesCount(selectedEntry);
                int obsLevel = CodexIntelUtility.GetMaxStudyProgress(selectedEntry, lc);
                int indiPeBox = CodexIntelUtility.GetTotalIndiPeBox(selectedEntry, lc);

                string obsStr = "LC_Codex_ObservationLevel".Translate($"{obsLevel} / {maxNotes}").ToString();
                if (kind == CodexEntryKind.Tool)
                {
                    using (new TextBlock(GameFont.Tiny, ColoredText.SubtleGrayColor))
                    {
                        Widgets.Label(statsRect, obsStr);
                    }
                }
                else
                {
                    float obsW = Text.CalcSize(obsStr).x + 12f;
                    using (new TextBlock(GameFont.Tiny, ColoredText.SubtleGrayColor))
                    {
                        Widgets.Label(new Rect(statsRect.x, statsRect.y, obsW, statsRect.height), obsStr);
                    }

                    float peX = statsRect.x + obsW;
                    if (TexPeBoxIcon != null)
                    {
                        GUI.DrawTexture(new Rect(peX, statsRect.y, 16f, 16f), TexPeBoxIcon, ScaleMode.ScaleToFit);
                        peX += 20f;
                    }

                    using (new TextBlock(GameFont.Tiny, GoldAccentColor))
                    {
                        Widgets.Label(new Rect(peX, statsRect.y, statsRect.xMax - peX, statsRect.height), "LC_Codex_IndiPeBox".Translate(indiPeBox));
                    }
                }
            }

            y = cardRect.yMax + 10f;
        }

        private float DrawBasicInfoSection(float width, float y, CodexEntryKind kind, ThingDef primaryDef, bool unlocked, GameComponent_LC lc)
        {
            DrawSectionHeader(width, ref y, "LC_Codex_Sec_BasicInfo".Translate());
            if (!unlocked)
            {
                return DrawLockedModuleCard(width, y, CodexIntelSection.BasicInfo,
                    "LC_Codex_LockedBasicHint".Translate(),
                    allowPeBoxUnlock: kind != CodexEntryKind.Ordeal,
                    lc: lc);
            }

            string desc = !selectedEntry.description.NullOrEmpty()
                ? selectedEntry.description
                : (primaryDef?.description ?? string.Empty);
            if (!desc.NullOrEmpty())
            {
                using (new TextBlock(newWordWrap: true))
                {
                    float h = Text.CalcHeight(desc, width);
                    Widgets.Label(new Rect(0f, y, width, h), desc);
                    y += h + 8f;
                }
            }

            if (kind == CodexEntryKind.Abnormality && primaryDef != null)
            {
                var abnProps = primaryDef.GetCompProperties<CompProperties_Abnormality>();
                if (abnProps != null)
                {
                    string qText = abnProps.qliphothCountMax > 0
                        ? abnProps.qliphothCountMax.ToString()
                        : "LC_Codex_NoneSymbol".Translate().ToString();
                    DrawSummaryLine(width, ref y, "LC_Codex_QliphothMaxLine".Translate(qText));

                    if (abnProps.TryGetWorkDamageInfo(out DamageDef workDmgDef, out FloatRange workRange))
                    {
                        FourColorDamageType fcType = ResolveFourColorTypeFromDamageDef(workDmgDef);
                        DrawFourColorIconLine(width, ref y, fcType,
                            "LC_Codex_WorkDamageLine".Translate(
                                FourColorDisplayHelper.FormatColoredDamageType(fcType),
                                FourColorDisplayHelper.FormatDamageRange(workRange.min, workRange.max, isPercent: false)));
                    }
                }
            }
            else if (kind == CodexEntryKind.Tool && primaryDef != null)
            {
                var toolProps = primaryDef.comps?.OfType<CompProperties_AbnormalityTool>().FirstOrDefault();
                if (toolProps != null)
                {
                    string modeKey = "LC_Codex_ToolMode_" + toolProps.toolMode;
                    string modeLabel = modeKey.CanTranslate() ? modeKey.Translate().ToString() : toolProps.toolMode.ToString();
                    DrawSummaryLine(width, ref y, "LC_Codex_ToolModeLine".Translate(modeLabel));
                }
            }

            return y + 8f;
        }

        private float DrawWorkPreferenceSection(float width, float y, ThingDef primaryDef, bool unlocked, GameComponent_LC lc)
        {
            DrawSectionHeader(width, ref y, "LC_Codex_Sec_WorkPreferences".Translate());
            if (!unlocked)
            {
                return DrawLockedModuleCard(width, y, CodexIntelSection.WorkPreferences,
                    "LC_Codex_LockedWorkHint".Translate(),
                    allowPeBoxUnlock: true,
                    lc: lc);
            }

            if (!CodexIntelUtility.TryGetWorkMatrix(primaryDef, out var matrix))
            {
                using (new TextBlock(ColoredText.SubtleGrayColor))
                {
                    string unavailable = "LC_Codex_WorkMatrixUnavailable".Translate();
                    float h = Text.CalcHeight(unavailable, width);
                    Widgets.Label(new Rect(0f, y, width, h), unavailable);
                    return y + h + 10f;
                }
            }

            float colWorkW = Mathf.Min(122f, width * 0.32f);
            float colLevelW = (width - colWorkW) / 5f;
            float rowH = 24f;

            Rect headerRect = new Rect(0f, y, width, rowH);
            Widgets.DrawBoxSolidWithOutline(headerRect, new Color(0.11f, 0.13f, 0.17f, 0.96f), BorderSubtleColor);
            using (new TextBlock(GameFont.Tiny, GoldAccentColor))
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(new Rect(0f, y, colWorkW, rowH), "LC_Codex_WorkTypeHeader".Translate());
                string[] numerals = { "I", "II", "III", "IV", "V" };
                for (int lv = 0; lv < 5; lv++)
                {
                    Widgets.Label(new Rect(colWorkW + lv * colLevelW, y, colLevelW, rowH), numerals[lv]);
                }
            }
            y += rowH + 2f;

            for (int r = 0; r < CodexIntelUtility.OrderedWorkTypes.Length; r++)
            {
                EAnomalyWorkType wt = CodexIntelUtility.OrderedWorkTypes[r];
                if (!matrix.TryGetValue(wt, out float[] rates))
                {
                    continue;
                }

                Rect rowRect = new Rect(0f, y, width, rowH);
                Widgets.DrawBoxSolid(rowRect, r % 2 == 0 ? new Color(0.08f, 0.10f, 0.13f, 0.92f) : new Color(0.07f, 0.08f, 0.11f, 0.92f));

                Texture2D wtIcon = GetWorkTypeIcon(wt);
                float labelLeft = 6f;
                if (wtIcon != null)
                {
                    GUI.DrawTexture(new Rect(6f, y + (rowH - 16f) * 0.5f, 16f, 16f), wtIcon, ScaleMode.ScaleToFit);
                    labelLeft = 26f;
                }

                string wtLabel = GetWorkTypeLabel(wt);
                using (new TextBlock(GameFont.Tiny, Color.white))
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    Widgets.Label(new Rect(labelLeft, y, colWorkW - labelLeft - 2f, rowH), wtLabel);
                }

                for (int lv = 0; lv < 5; lv++)
                {
                    float rate = rates[lv];
                    Rect cellRect = new Rect(colWorkW + lv * colLevelW + 1f, y + 1f, colLevelW - 2f, rowH - 2f);
                    Color textColor = GetRateColor(rate, out Color cellBg);
                    Widgets.DrawBoxSolid(cellRect, cellBg);
                    using (new TextBlock(GameFont.Tiny, textColor))
                    using (new TextBlock(TextAnchor.MiddleCenter))
                    {
                        Widgets.Label(cellRect, $"{Mathf.RoundToInt(rate * 100f)}%");
                    }
                }

                y += rowH + 2f;
            }

            return y + 8f;
        }

        private float DrawCombatAndEscapeSection(float width, float y, CodexEntryKind kind, ThingDef primaryDef, bool unlocked, GameComponent_LC lc)
        {
            DrawSectionHeader(width, ref y, "LC_Codex_Sec_EscapeCombat".Translate());
            if (!unlocked)
            {
                string hint = kind == CodexEntryKind.Ordeal
                    ? "LC_Codex_LockedOrdealCombatHint".Translate()
                    : "LC_Codex_LockedCombatHint".Translate();
                return DrawLockedModuleCard(width, y, CodexIntelSection.EscapeCombat, hint, allowPeBoxUnlock: kind != CodexEntryKind.Ordeal, lc: lc);
            }

            if (primaryDef == null)
            {
                return y;
            }

            LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
            DamageUtils.TryGetEntityRiskLevel(primaryDef, out riskLevel);
            FourColorDamageType attackType = ResolveEntityAttackDamageType(primaryDef, ref riskLevel);

            if (attackType != FourColorDamageType.None)
            {
                DrawFourColorIconLine(width, ref y, attackType,
                    "LC_Codex_AttackTypeLine".Translate(
                        FourColorDisplayHelper.FormatColoredDamageType(attackType),
                        FourColorDisplayHelper.FormatColoredRiskLevel(riskLevel)));
            }
            else
            {
                DrawSummaryLine(width, ref y, "LC_Codex_AttackTypeLine".Translate(
                    FourColorDisplayHelper.FormatDamageType(FourColorDamageType.None),
                    FourColorDisplayHelper.FormatColoredRiskLevel(riskLevel)));
            }

            if (primaryDef.statBases != null)
            {
                float maxHpScale = primaryDef.race?.baseHealthScale ?? 1f;
                int baseHp = Mathf.RoundToInt(maxHpScale * 100f);
                float moveSpeed = primaryDef.GetStatValueAbstract(RimWorld.StatDefOf.MoveSpeed);
                DrawSummaryLine(width, ref y, "LC_Codex_CombatVitalsLine".Translate(baseHp, moveSpeed.ToString("0.##")));
            }

            y += 4f;

            FourColorOverrides.ResolvedRaceProfile raceProfile = FourColorOverrides.GetRaceProfile(primaryDef);
            float cardW = (width - 6f) * 0.5f;
            float cardH = 28f;
            for (int i = 0; i < FourColors.Length; i++)
            {
                FourColorDamageType colorType = FourColors[i];
                int col = i % 2;
                int row = i / 2;
                Rect resRect = new Rect(col * (cardW + 6f), y + row * (cardH + 4f), cardW, cardH);
                Widgets.DrawBoxSolidWithOutline(resRect, CardBgColor, BorderSubtleColor);

                Texture2D dmgIcon = FourColorPopupTextures.GetIcon(colorType);
                if (dmgIcon != null)
                {
                    GUI.DrawTexture(new Rect(resRect.x + 6f, resRect.y + 6f, 16f, 16f), dmgIcon, ScaleMode.ScaleToFit);
                }

                float resVal = raceProfile.ResistanceFor(colorType);
                string resLabel = $"{FourColorDisplayHelper.FormatColoredDamageType(colorType)}  {FourColorDisplayHelper.FormatResistanceValue(resVal, colorType)}";
                using (new TextBlock(GameFont.Tiny))
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    Widgets.Label(new Rect(resRect.x + 26f, resRect.y, resRect.width - 30f, resRect.height), resLabel);
                }
            }

            y += (cardH + 4f) * 2f + 8f;
            return y;
        }

        private float DrawEgoSection(float width, float y, ThingDef primaryDef, bool hasBasic, bool hasSaved, AnomalyStatusSaved saved)
        {
            var egoProps = primaryDef?.GetCompProperties<CompProperties_EgoExtractable>();
            var accProps = primaryDef?.GetCompProperties<CompProperties_Accessoryable>();
            if (egoProps == null && accProps == null)
            {
                return y;
            }

            DrawSectionHeader(width, ref y, "LC_Codex_Sec_EgoGear".Translate());

            if (egoProps?.weaponExtracted != null)
            {
                int extracted = hasSaved ? saved.ExtractedEgoWeaponAmount : 0;
                DrawEgoGearRow(width, ref y, egoProps.weaponExtracted,
                    hasBasic || extracted > 0,
                    "LC_Codex_EgoWeaponSummary".Translate($"{extracted}/{egoProps.amountMaxWeapon}", egoProps.weaponExtractedNeed, egoProps.unlockLevel));
            }

            if (egoProps?.armorExtracted != null)
            {
                int extracted = hasSaved ? saved.ExtractedEgoArmorAmount : 0;
                DrawEgoGearRow(width, ref y, egoProps.armorExtracted,
                    hasBasic || extracted > 0,
                    "LC_Codex_EgoArmorSummary".Translate($"{extracted}/{egoProps.amountMaxArmor}", egoProps.armorExtractedNeed, egoProps.unlockLevel));
            }

            if (accProps?.accessoryDef != null)
            {
                DrawEgoGearRow(width, ref y, accProps.accessoryDef,
                    hasBasic,
                    "LC_Codex_EgoGiftSummary".Translate($"{accProps.accessoryChance * 100f:0.#}%", accProps.unlockLevel));
            }

            return y + 8f;
        }

        private static void DrawEgoGearRow(float width, ref float y, ThingDef gearDef, bool revealIdentity, string detailLine)
        {
            Rect row = new Rect(0f, y, width, 44f);
            Widgets.DrawBoxSolidWithOutline(row, CardBgColor, BorderSubtleColor);

            Rect iconBox = new Rect(row.x + 6f, row.y + 6f, 32f, 32f);
            if (revealIdentity)
            {
                Widgets.DefIcon(iconBox, gearDef);
                if (TexPortraitFrame != null)
                {
                    GUI.DrawTexture(iconBox, TexPortraitFrame, ScaleMode.StretchToFill);
                }
                Widgets.InfoCardButton(row.xMax - 28f, row.y + 10f, gearDef);
            }
            else
            {
                if (TexLockIcon != null)
                {
                    GUI.DrawTexture(iconBox.ContractedBy(3f), TexLockIcon, ScaleMode.ScaleToFit);
                }
                else
                {
                    using (new TextBlock(GameFont.Medium, ColoredText.SubtleGrayColor))
                    using (new TextBlock(TextAnchor.MiddleCenter))
                    {
                        Widgets.Label(iconBox, "?");
                    }
                }
            }

            string title = revealIdentity ? gearDef.LabelCap.ToString() : "???";
            using (new TextBlock(GameFont.Small, revealIdentity ? Color.white : GoldAccentColor))
            {
                Widgets.Label(new Rect(row.x + 44f, row.y + 3f, row.width - 76f, 20f), title);
            }

            using (new TextBlock(GameFont.Tiny, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(new Rect(row.x + 44f, row.y + 22f, row.width - 76f, 18f), detailLine);
            }

            y += row.height + 4f;
        }

        private float DrawManagementTipsSection(float width, float y, CodexEntryKind kind, ThingDef primaryDef, bool unlocked, GameComponent_LC lc)
        {
            var studyProps = primaryDef?.GetCompProperties<CompProperties_AbnormalityStudyUnlocks>();
            if (studyProps == null || studyProps.studyNotes.NullOrEmpty())
            {
                return y;
            }

            DrawSectionHeader(width, ref y, "LC_Codex_StudyNotes".Translate());
            if (!unlocked)
            {
                string hint = kind == CodexEntryKind.Tool
                    ? "LC_Codex_LockedToolLogsHint".Translate()
                    : "LC_Codex_LockedTipsHint".Translate();
                return DrawLockedModuleCard(width, y, CodexIntelSection.ManagementTips, hint, allowPeBoxUnlock: kind == CodexEntryKind.Abnormality, lc: lc);
            }

            int savedProgress = CodexIntelUtility.GetMaxStudyProgress(selectedEntry, lc);
            int visibleCount = (devShowAll || unlocked)
                ? studyProps.studyNotes.Count
                : Mathf.Clamp(savedProgress, 0, studyProps.studyNotes.Count);

            if (visibleCount <= 0)
            {
                using (new TextBlock(ColoredText.SubtleGrayColor))
                {
                    string emptyText = "LC_Codex_NoStudyNotes".Translate();
                    float h = Text.CalcHeight(emptyText, width);
                    Widgets.Label(new Rect(0f, y, width, h), emptyText);
                    return y + h + 6f;
                }
            }

            for (int i = 0; i < visibleCount; i++)
            {
                StudyNote note = studyProps.studyNotes[i];
                if (note == null)
                {
                    continue;
                }

                string noteLabel = note.label ?? string.Empty;
                using (new TextBlock(GameFont.Small, GoldAccentColor))
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

        private float DrawLockedModuleCard(
            float width,
            float y,
            CodexIntelSection section,
            string hintText,
            bool allowPeBoxUnlock,
            GameComponent_LC lc)
        {
            float iconSlotW = TexLockIcon != null ? 54f : 0f;
            float textWidth = Mathf.Max(120f, width - iconSlotW - 24f);
            float hintH = Text.CalcHeight(hintText, textWidth);
            float bodyH = hintH + (allowPeBoxUnlock ? 38f : 8f);
            float cardH = Mathf.Max(allowPeBoxUnlock ? 82f : 62f, bodyH + 16f);

            Rect cardRect = new Rect(0f, y, width, cardH);
            if (TexLockedCard != null)
            {
                GUI.DrawTexture(cardRect, TexLockedCard, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(cardRect, LockedCardBgFallback, CrimsonAccentColor);
            }

            if (TexLockIcon != null)
            {
                Rect lockRect = new Rect(cardRect.x + 10f, cardRect.center.y - 22f, 44f, 44f);
                GUI.DrawTexture(lockRect, TexLockIcon, ScaleMode.ScaleToFit);
            }

            float contentX = cardRect.x + 12f + iconSlotW;
            using (new TextBlock(GameFont.Tiny, new Color(0.86f, 0.84f, 0.82f)))
            using (new TextBlock(newWordWrap: true))
            {
                Widgets.Label(new Rect(contentX, cardRect.y + 8f, textWidth, hintH), hintText);
            }

            if (allowPeBoxUnlock)
            {
                int cost = selectedEntry.IntelUnlockCost;
                int available = CodexIntelUtility.GetTotalIndiPeBox(selectedEntry, lc);
                bool canAfford = DebugSettings.godMode || available >= cost;
                string btnLabel = "LC_Codex_UnlockModuleBtn".Translate(cost, available);
                Rect btnRect = new Rect(contentX, cardRect.yMax - 34f, textWidth, 26f);

                DrawStyledLobCorpButton(btnRect, btnLabel, activeLook: canAfford, disabled: !canAfford, leftIcon: TexPeBoxIcon);
                if (Widgets.ButtonInvisible(btnRect))
                {
                    if (canAfford && lc != null && lc.TryUnlockCodexIntelWithPeBox(selectedEntry, section, out string failReason))
                    {
                        RimWorld.SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                    else
                    {
                        Messages.Message("LC_Codex_InsufficientPeBox".Translate(cost, available), MessageTypeDefOf.RejectInput, false);
                    }
                }
            }

            return cardRect.yMax + 10f;
        }

        private float DrawResearchUnlocks(float width, float y)
        {
            if (selectedEntry.discoveredResearchProjects.NullOrEmpty()
                || !selectedEntry.discoveredResearchProjects.Any(p => p != null))
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

            return y + 8f;
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

            return y + 8f;
        }

        private List<ThingDef> CollectLinkedThings(ThingDef primaryDef)
        {
            var results = new List<ThingDef>();
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

        private void DrawRightCatalog(Rect rect)
        {
            if (!entriesByTab.TryGetValue(activeTab, out var catDict))
            {
                return;
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, rightScrollHeight));
            Widgets.BeginScrollView(rect, ref rightScrollPos, viewRect);

            float y = 0f;
            GameComponent_LC lc = Components.LC;
            foreach (AbnormalityCategoryDef cat in categoriesInOrder)
            {
                if (!catDict.TryGetValue(cat, out List<AbnormalityCodexEntryDef> list) || list.Count == 0)
                {
                    continue;
                }

                float startY = y;
                float catHeight = categoryRectSizes.TryGetValue(cat, out float cachedH) ? cachedH : 120f;
                Color catColor = GetCategoryColor(cat.defName);

                Rect bgRect = new Rect(0f, y, viewRect.width, catHeight);
                Widgets.DrawBoxSolid(bgRect, new Color(0.08f, 0.09f, 0.12f, 0.62f));
                Widgets.DrawBoxSolid(new Rect(bgRect.x, bgRect.y, 4f, Text.LineHeight + 8f), catColor);
                DrawToolTipRow(cat, new Rect(bgRect.x, bgRect.y, bgRect.width, Text.LineHeight + 8f));

                string catLabel = cat.LabelCap.ToString();
                float catTitleW = Mathf.Max(64f, Text.CalcSize(catLabel).x + 12f);
                using (new TextBlock(GameFont.Small, catColor))
                {
                    Widgets.Label(new Rect(10f, y + 2f, catTitleW, Text.LineHeight), catLabel);
                }

                if (TexHazardDivider != null)
                {
                    float divX = 14f + catTitleW;
                    float divW = Mathf.Max(0f, viewRect.width - divX - 64f);
                    if (divW > 24f)
                    {
                        Color old = GUI.color;
                        GUI.color = new Color(catColor.r, catColor.g, catColor.b, 0.72f);
                        GUI.DrawTexture(new Rect(divX, y + 5f, divW, 12f), TexHazardDivider, ScaleMode.StretchToFill);
                        GUI.color = old;
                    }
                }

                int catDiscovered = list.Count(e => devShowAll || e.Discovered);
                using (new TextBlock(GameFont.Tiny, GoldAccentColor))
                using (new TextBlock(TextAnchor.MiddleRight))
                {
                    Widgets.Label(new Rect(0f, y + 2f, viewRect.width - 8f, Text.LineHeight), $"{catDiscovered}/{list.Count}");
                }

                y += Text.LineHeight + 10f;

                int cols = Mathf.Clamp(Mathf.FloorToInt((viewRect.width - 12f + EntryTileGap) / (EntryTileSize + EntryTileGap)), 1, MaxEntriesPerRow);
                int rows = Mathf.CeilToInt((float)list.Count / cols);
                float tileTotalH = EntryTileSize + EntryPipHeight;

                for (int i = 0; i < list.Count; i++)
                {
                    AbnormalityCodexEntryDef entry = list[i];
                    int row = i / cols;
                    int col = i % cols;
                    int itemsInThisRow = (row == rows - 1 && list.Count % cols != 0) ? (list.Count % cols) : cols;
                    float rowWidth = itemsInThisRow * EntryTileSize + (itemsInThisRow - 1) * EntryTileGap;
                    float startX = (viewRect.width - rowWidth) * 0.5f;

                    Rect tileRect = new Rect(
                        startX + col * (EntryTileSize + EntryTileGap),
                        y + row * (tileTotalH + EntryTileGap),
                        EntryTileSize,
                        EntryTileSize);

                    DrawEntryTile(tileRect, entry, lc);
                }

                y += rows * tileTotalH + Mathf.Max(0, rows - 1) * EntryTileGap + 10f;
                categoryRectSizes[cat] = y - startY;
                y += 8f;
            }

            rightScrollHeight = y;
            Widgets.EndScrollView();
        }

        private void DrawEntryTile(Rect rect, AbnormalityCodexEntryDef entry, GameComponent_LC lc)
        {
            bool discovered = devShowAll || entry.Discovered;
            bool hasBasic = devShowAll || (lc?.HasCodexIntel(entry, CodexIntelSection.BasicInfo) ?? false);
            bool isSelected = entry == selectedEntry;
            bool isHovered = Mouse.IsOver(rect);

            Widgets.DrawBoxSolid(rect,
                isSelected ? new Color(0.18f, 0.11f, 0.12f, 0.96f) : (isHovered ? new Color(0.14f, 0.16f, 0.20f, 0.94f) : CardBgColor));

            Texture2D tex = discovered
                ? (entry.icon ?? BaseContent.BadTex)
                : (entry.silhouette ?? BaseContent.BadTex);
            Color prev = GUI.color;
            if (discovered && !hasBasic)
            {
                GUI.color = new Color(0.70f, 0.74f, 0.80f, 1f);
            }
            GUI.DrawTexture(rect.ContractedBy(7f), tex, ScaleMode.ScaleToFit);
            GUI.color = prev;

            Texture2D frameTex = isSelected ? TexTileSelected : TexTileFrame;
            if (frameTex != null)
            {
                GUI.DrawTexture(rect, frameTex, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(rect, Color.clear, isSelected ? GoldAccentColor : BorderSubtleColor);
            }

            string tileLabel = !discovered
                ? "???"
                : (hasBasic ? entry.LabelCap.ToString() : entry.ResolvedContainmentCode);
            using (new TextBlock(GameFont.Tiny, hasBasic ? Color.white : GoldAccentColor))
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Rect labelRect = new Rect(rect.x + 2f, rect.yMax - EntryTileLabelHeight - 2f, rect.width - 4f, EntryTileLabelHeight);
                Widgets.DrawBoxSolid(labelRect, new Color(0.03f, 0.04f, 0.06f, 0.78f));
                Widgets.Label(labelRect, tileLabel);
            }

            CodexIntelSection applicable = entry.ApplicableIntelSections;
            CodexIntelSection unlocked = devShowAll
                ? applicable
                : (discovered && lc != null ? lc.GetUnlockedCodexIntel(entry) : CodexIntelSection.None);
            DrawIntelPips(new Rect(rect.x + 2f, rect.yMax + 1f, rect.width - 4f, EntryPipHeight - 1f), applicable, unlocked);

            if (Widgets.ButtonInvisible(new Rect(rect.x, rect.y, rect.width, rect.height + EntryPipHeight)))
            {
                selectedEntry = entry;
                leftScrollPos = Vector2.zero;
                RimWorld.SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
        }

        private static void DrawIntelPips(Rect rect, CodexIntelSection applicable, CodexIntelSection unlocked)
        {
            var segments = new List<bool>(4);
            for (int i = 0; i < CodexIntelUtility.OrderedSections.Length; i++)
            {
                CodexIntelSection sec = CodexIntelUtility.OrderedSections[i];
                if ((applicable & sec) == sec)
                {
                    segments.Add((unlocked & sec) == sec);
                }
            }

            if (segments.Count == 0)
            {
                return;
            }

            float gap = 2f;
            float segW = (rect.width - gap * (segments.Count - 1)) / segments.Count;
            for (int i = 0; i < segments.Count; i++)
            {
                Rect segRect = new Rect(rect.x + i * (segW + gap), rect.y, segW, rect.height);
                Widgets.DrawBoxSolid(segRect, segments[i] ? GoldAccentColor : new Color(0.18f, 0.20f, 0.24f, 0.92f));
            }
        }

        private void DrawToolTipRow(AbnormalityCategoryDef cat, Rect rect)
        {
            string labelCap = cat.label.Translate();
            labelCap += "\n\n" + ("LC_EntityCodexTip." + cat.defName).Translate();

            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, labelCap);
            }
        }

        private static void DrawSectionHeader(float width, ref float y, string label)
        {
            Rect barRect = new Rect(0f, y, width, 26f);
            if (TexSectionBar != null)
            {
                GUI.DrawTexture(barRect, TexSectionBar, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(barRect, new Color(0.10f, 0.12f, 0.16f, 0.95f));
                Widgets.DrawLineHorizontal(0f, barRect.yMax, width);
            }

            using (new TextBlock(GameFont.Small, GoldAccentColor))
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(14f, barRect.y, width - 18f, barRect.height), label);
            }

            y = barRect.yMax + 6f;
        }

        private static void DrawSummaryLine(float width, ref float y, string text)
        {
            float h = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, h), text);
            y += h + 2f;
        }

        private static void DrawFourColorIconLine(float width, ref float y, FourColorDamageType fcType, string text)
        {
            Texture2D icon = FourColorPopupTextures.GetIcon(fcType);
            float offset = icon != null ? 20f : 0f;
            float h = Mathf.Max(Text.LineHeight, Text.CalcHeight(text, width - offset));
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(0f, y + (h - 16f) * 0.5f, 16f, 16f), icon, ScaleMode.ScaleToFit);
            }

            Widgets.Label(new Rect(offset, y, width - offset, h), text);
            y += h + 3f;
        }

        private static void DrawRiskPill(Rect rect, string riskName, string displayLabel)
        {
            Color catColor = GetCategoryColor(riskName);
            Widgets.DrawBoxSolidWithOutline(rect,
                new Color(catColor.r * 0.22f, catColor.g * 0.22f, catColor.b * 0.22f, 0.95f),
                catColor);
            using (new TextBlock(GameFont.Tiny, catColor))
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(rect, displayLabel ?? riskName ?? "???");
            }
        }

        private static Color GetCategoryColor(string riskName)
        {
            switch (riskName)
            {
                case "ZAYIN": return new Color(0.36f, 0.90f, 0.36f);
                case "TETH": return new Color(0.30f, 0.65f, 1.00f);
                case "HE": return new Color(1.00f, 0.82f, 0.25f);
                case "WAW": return new Color(0.77f, 0.49f, 1.00f);
                case "ALEPH": return new Color(1.00f, 0.28f, 0.28f);
                default: return Color.white;
            }
        }

        private static Color GetRateColor(float rate, out Color bgColor)
        {
            if (rate <= 0.001f)
            {
                bgColor = new Color(0.28f, 0.08f, 0.08f, 0.9f);
                return new Color(0.95f, 0.32f, 0.32f);
            }

            if (rate < 0.30f)
            {
                bgColor = new Color(0.26f, 0.14f, 0.08f, 0.9f);
                return new Color(0.96f, 0.55f, 0.24f);
            }

            if (rate < 0.45f)
            {
                bgColor = new Color(0.24f, 0.20f, 0.08f, 0.9f);
                return new Color(0.95f, 0.79f, 0.28f);
            }

            if (rate < 0.60f)
            {
                bgColor = new Color(0.10f, 0.22f, 0.26f, 0.9f);
                return new Color(0.40f, 0.85f, 0.94f);
            }

            bgColor = new Color(0.09f, 0.25f, 0.14f, 0.9f);
            return new Color(0.31f, 0.88f, 0.48f);
        }

        private static Texture2D GetWorkTypeIcon(EAnomalyWorkType wt)
        {
            switch (wt)
            {
                case EAnomalyWorkType.Instinct: return TexWorkInstinct;
                case EAnomalyWorkType.Insight: return TexWorkInsight;
                case EAnomalyWorkType.Attachment: return TexWorkAttachment;
                case EAnomalyWorkType.Repression: return TexWorkRepression;
                default: return null;
            }
        }

        private static string GetWorkTypeLabel(EAnomalyWorkType wt)
        {
            switch (wt)
            {
                case EAnomalyWorkType.Instinct: return "TipDialogWorkType_Instinct".Translate();
                case EAnomalyWorkType.Insight: return "TipDialogWorkType_Insight".Translate();
                case EAnomalyWorkType.Attachment: return "TipDialogWorkType_Attachment".Translate();
                case EAnomalyWorkType.Repression: return "TipDialogWorkType_Repression".Translate();
                default: return wt.ToString();
            }
        }

        private static FourColorDamageType ResolveFourColorTypeFromDamageDef(DamageDef damageDef)
        {
            if (damageDef == null)
            {
                return FourColorDamageType.None;
            }

            FourColorDamageExtension ext = damageDef.GetModExtension<FourColorDamageExtension>();
            return ext != null ? ext.damageType : FourColorDamageType.None;
        }

        private static FourColorDamageType ResolveEntityAttackDamageType(ThingDef primaryDef, ref LCRiskLevel riskLevel)
        {
            if (primaryDef == null)
            {
                return FourColorDamageType.None;
            }

            FourColorWeaponExtension ext = primaryDef.GetModExtension<FourColorWeaponExtension>();
            if (ext != null && ext.damageType != FourColorDamageType.None)
            {
                if (ext.riskLevel > riskLevel)
                {
                    riskLevel = ext.riskLevel;
                }
                return ext.damageType;
            }

            if (FourColorOverrides.TryGetWeaponProfile(primaryDef, out FourColorOverrides.ResolvedWeaponProfile wp)
                && wp.damageType != FourColorDamageType.None)
            {
                if (wp.riskLevel > riskLevel)
                {
                    riskLevel = wp.riskLevel;
                }
                return wp.damageType;
            }

            return FourColorDamageType.None;
        }
    }
}
