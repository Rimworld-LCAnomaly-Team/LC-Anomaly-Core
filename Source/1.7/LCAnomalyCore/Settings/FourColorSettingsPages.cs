using System;
using System.Collections.Generic;
using System.Globalization;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Defs;
using LCAnomalyCore.Patch;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Settings
{
    /// <summary>
    /// 四色护甲、武器与种族运行时覆写可视化编辑页。
    /// </summary>
    internal static class FourColorSettingsPages
    {
        private const float RowHeight = 28f;
        private const float HeaderHeight = 20f;
        private const float ScrollbarWidth = 18f;
        private const float FieldWidth = 50f;
        private const float LevelButtonWidth = 68f;
        private const float TypeButtonWidth = 76f;
        private const float TargetKindWidth = 96f;
        private const float RevertWidth = 76f;

        private static List<ThingDef> apparelDefs;
        private static List<ThingDef> weaponDefs;
        private static List<ThingDef> raceDefs;

        private static Vector2 apparelScroll;
        private static Vector2 weaponScroll;
        private static Vector2 raceScroll;

        private static string apparelSearch = string.Empty;
        private static string weaponSearch = string.Empty;
        private static string raceSearch = string.Empty;

        private static bool apparelOnlyConfigured;
        private static bool apparelOnlyOverridden;
        private static bool weaponOnlyFourColor;
        private static bool weaponOnlyOverridden;
        private static bool raceOnlyOverridden;

        private const int RaceFilterAll = 0;
        private const int RaceFilterHumanlike = 1;
        private const int RaceFilterAnimal = 2;
        private const int RaceFilterMechanoid = 3;
        private const int RaceFilterInsect = 4;
        private static int raceFilter = RaceFilterAll;

        private static readonly Dictionary<string, string> buffers = new Dictionary<string, string>();

        public static void ClearBuffers()
        {
            buffers.Clear();
        }

        public static void InvalidateLists()
        {
            apparelDefs = null;
            weaponDefs = null;
            raceDefs = null;
            buffers.Clear();
        }

        private static void EnsureLists()
        {
            if (apparelDefs != null)
            {
                return;
            }

            apparelDefs = new List<ThingDef>();
            weaponDefs = new List<ThingDef>();
            raceDefs = new List<ThingDef>();

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (FourColorOverrides.IsEligibleApparel(def))
                {
                    apparelDefs.Add(def);
                }
                else if (FourColorOverrides.IsEligibleWeapon(def))
                {
                    weaponDefs.Add(def);
                }
                else if (FourColorOverrides.IsEligibleRace(def))
                {
                    raceDefs.Add(def);
                }
            }

            Comparison<ThingDef> byLabel = (a, b) => string.Compare(
                a.LabelCap.ToString(), b.LabelCap.ToString(), StringComparison.CurrentCultureIgnoreCase);
            apparelDefs.Sort(byLabel);
            weaponDefs.Sort(byLabel);
            raceDefs.Sort(byLabel);
        }

        public static void DrawApparelPage(Rect rect)
        {
            EnsureLists();

            float y = rect.y;
            DrawTopSearchBar(rect, ref y, ref apparelSearch,
                "LC_Page_OnlyConfigured".Translate(), ref apparelOnlyConfigured,
                ref apparelOnlyOverridden,
                () =>
                {
                    FourColorOverrides.RevertAllApparel();
                    ClearBuffers();
                });

            DrawHintLine(rect, ref y, "LC_Page_ApparelHint".Translate());

            float fixedWidth = LevelButtonWidth + FieldWidth * 4f + RevertWidth + 40f;
            float nameWidth = Mathf.Max(120f, rect.width - ScrollbarWidth - fixedWidth);
            Rect headerRect = new Rect(rect.x, y, rect.width - ScrollbarWidth, HeaderHeight);
            DrawApparelHeader(headerRect, nameWidth);
            y += HeaderHeight + 2f;

            var shown = new List<ThingDef>();
            for (int i = 0; i < apparelDefs.Count; i++)
            {
                ThingDef def = apparelDefs[i];
                if (!MatchesSearch(def, apparelSearch))
                {
                    continue;
                }
                if (apparelOnlyConfigured && !FourColorOverrides.TryGetApparelProfile(def, out _))
                {
                    continue;
                }
                if (apparelOnlyOverridden && !FourColorOverrides.HasApparelOverride(def.defName))
                {
                    continue;
                }
                shown.Add(def);
            }

            DrawVirtualRows(
                new Rect(rect.x, y, rect.width, rect.yMax - y),
                ref apparelScroll,
                shown,
                (row, def) => DrawApparelRow(row, def, nameWidth));
        }

        private static void DrawApparelHeader(Rect rect, float nameWidth)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleCenter, false))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.68f);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 28f, rect.y, nameWidth - 28f, HeaderHeight), "LC_Col_Apparel".Translate());

                Text.Anchor = TextAnchor.MiddleCenter;
                float x = rect.x + nameWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, LevelButtonWidth, HeaderHeight), "LC_Col_Level".Translate());
                x += LevelButtonWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_Red".Translate());
                x += FieldWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_White".Translate());
                x += FieldWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_Black".Translate());
                x += FieldWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_Pale".Translate());

                GUI.color = old;
            }
        }

        private static void DrawApparelRow(Rect row, ThingDef def, float nameWidth)
        {
            Widgets.DrawHighlightIfMouseover(row);
            bool hasProfile = FourColorOverrides.TryGetApparelProfile(def, out FourColorOverrides.ResolvedApparelProfile profile);
            bool overridden = FourColorOverrides.HasApparelOverride(def.defName);
            string sourceText = hasProfile
                ? FourColorDisplayHelper.FormatSource(profile.source)
                : "LC_FourColorSource_None".Translate();
            TooltipHandler.TipRegion(row, $"{def.defName}\n{sourceText}");

            Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 3f, 22f, 22f), def);

            Color oldColor = GUI.color;
            if (overridden)
            {
                GUI.color = new Color(0.85f, 0.96f, 1f);
            }
            else if (!hasProfile)
            {
                GUI.color = ColoredText.SubtleGrayColor;
            }

            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft, false))
            {
                Widgets.Label(new Rect(row.x + 28f, row.y, nameWidth - 28f, RowHeight), def.LabelCap.ToString().Truncate(nameWidth - 32f));
            }
            GUI.color = oldColor;

            float x = row.x + nameWidth + 4f;
            string levelLabel = hasProfile ? profile.riskLevel.ToString() : "LC_DamageType_None".Translate();
            if (Widgets.ButtonText(new Rect(x, row.y + 3f, LevelButtonWidth, 22f), levelLabel))
            {
                if (!hasProfile)
                {
                    FourColorOverrides.SetApparelOverride(def, true, LCRiskLevel.ZAYIN, 1f, 1f, 1f, 1f);
                }
                else if (profile.riskLevel == LCRiskLevel.ALEPH)
                {
                    FourColorOverrides.SetApparelOverride(def, false, LCRiskLevel.ZAYIN, 1f, 1f, 1f, 1f);
                }
                else
                {
                    FourColorOverrides.SetApparelOverride(def, true, profile.riskLevel + 1, profile.red, profile.white, profile.black, profile.pale);
                }
                ClearRowBuffers("A|" + def.defName);
            }

            LCRiskLevel activeLevel = hasProfile ? profile.riskLevel : LCRiskLevel.TETH;
            float r = hasProfile ? profile.red : 1f;
            float w = hasProfile ? profile.white : 1f;
            float b = hasProfile ? profile.black : 1f;
            float p = hasProfile ? profile.pale : 1f;

            x += LevelButtonWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"A|{def.defName}|r",
                hasProfile ? (float?)r : null, 0f, 5f,
                v => OnApparelFieldEdited(def, activeLevel, v ?? 1f, w, b, p));

            x += FieldWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"A|{def.defName}|w",
                hasProfile ? (float?)w : null, 0f, 5f,
                v => OnApparelFieldEdited(def, activeLevel, r, v ?? 1f, b, p));

            x += FieldWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"A|{def.defName}|b",
                hasProfile ? (float?)b : null, 0f, 5f,
                v => OnApparelFieldEdited(def, activeLevel, r, w, v ?? 1f, p));

            x += FieldWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"A|{def.defName}|p",
                hasProfile ? (float?)p : null, 0f, 5f,
                v => OnApparelFieldEdited(def, activeLevel, r, w, b, v ?? 1f));

            x += FieldWidth + 6f;
            if (Widgets.ButtonText(new Rect(x, row.y + 3f, RevertWidth, 22f), "LC_Page_Revert".Translate(), active: overridden))
            {
                FourColorOverrides.RevertApparel(def.defName);
                ClearRowBuffers("A|" + def.defName);
            }
        }

        private static void OnApparelFieldEdited(ThingDef def, LCRiskLevel level, float r, float w, float b, float p)
        {
            FourColorOverrides.SetApparelOverride(def, true, level, r, w, b, p);
        }

        public static void DrawWeaponPage(Rect rect)
        {
            EnsureLists();

            float y = rect.y;
            DrawTopSearchBar(rect, ref y, ref weaponSearch,
                "LC_Page_OnlyFourColorWeapons".Translate(), ref weaponOnlyFourColor,
                ref weaponOnlyOverridden,
                () =>
                {
                    FourColorOverrides.RevertAllWeapons();
                    ClearBuffers();
                });

            DrawHintLine(rect, ref y, "LC_Page_WeaponHint".Translate());

            float fixedWidth = TypeButtonWidth + LevelButtonWidth + FieldWidth + RevertWidth + 30f;
            float nameWidth = Mathf.Max(120f, rect.width - ScrollbarWidth - fixedWidth);
            Rect headerRect = new Rect(rect.x, y, rect.width - ScrollbarWidth, HeaderHeight);
            DrawWeaponHeader(headerRect, nameWidth);
            y += HeaderHeight + 2f;

            var shown = new List<ThingDef>();
            for (int i = 0; i < weaponDefs.Count; i++)
            {
                ThingDef def = weaponDefs[i];
                if (!MatchesSearch(def, weaponSearch))
                {
                    continue;
                }
                if (weaponOnlyFourColor && !FourColorOverrides.TryGetWeaponProfile(def, out _))
                {
                    continue;
                }
                if (weaponOnlyOverridden && !FourColorOverrides.HasWeaponOverride(def.defName))
                {
                    continue;
                }
                shown.Add(def);
            }

            DrawVirtualRows(
                new Rect(rect.x, y, rect.width, rect.yMax - y),
                ref weaponScroll,
                shown,
                (row, def) => DrawWeaponRow(row, def, nameWidth));
        }

        private static void DrawWeaponHeader(Rect rect, float nameWidth)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleCenter, false))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.68f);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 28f, rect.y, nameWidth - 28f, HeaderHeight), "LC_Col_Weapon".Translate());

                Text.Anchor = TextAnchor.MiddleCenter;
                float x = rect.x + nameWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, TypeButtonWidth, HeaderHeight), "LC_Col_DamageType".Translate());
                x += TypeButtonWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, LevelButtonWidth, HeaderHeight), "LC_Col_Level".Translate());
                x += LevelButtonWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_Multiplier".Translate());

                GUI.color = old;
            }
        }

        private static void DrawWeaponRow(Rect row, ThingDef def, float nameWidth)
        {
            Widgets.DrawHighlightIfMouseover(row);
            bool hasProfile = FourColorOverrides.TryGetWeaponProfile(def, out FourColorOverrides.ResolvedWeaponProfile profile);
            bool overridden = FourColorOverrides.HasWeaponOverride(def.defName);
            string sourceText = hasProfile
                ? FourColorDisplayHelper.FormatSource(profile.source)
                : "LC_FourColorSource_None".Translate();
            TooltipHandler.TipRegion(row, $"{def.defName}\n{sourceText}");

            Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 3f, 22f, 22f), def);

            Color oldColor = GUI.color;
            if (overridden)
            {
                GUI.color = new Color(0.85f, 0.96f, 1f);
            }
            else if (!hasProfile)
            {
                GUI.color = ColoredText.SubtleGrayColor;
            }

            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft, false))
            {
                Widgets.Label(new Rect(row.x + 28f, row.y, nameWidth - 28f, RowHeight), def.LabelCap.ToString().Truncate(nameWidth - 32f));
            }
            GUI.color = oldColor;

            FourColorDamageType activeType = hasProfile ? profile.damageType : FourColorDamageType.None;
            LCRiskLevel activeLevel = hasProfile ? profile.riskLevel : LCRiskLevel.ZAYIN;
            float activeMult = hasProfile ? profile.damageMultiplier : 1f;

            float x = row.x + nameWidth + 4f;
            if (Widgets.ButtonText(new Rect(x, row.y + 3f, TypeButtonWidth, 22f), FourColorDisplayHelper.FormatDamageType(activeType)))
            {
                FourColorDamageType next = activeType == FourColorDamageType.Pale
                    ? FourColorDamageType.None
                    : activeType + 1;
                FourColorOverrides.SetWeaponOverride(def, next, activeLevel, activeMult);
                ClearRowBuffers("W|" + def.defName);
            }

            x += TypeButtonWidth + 4f;
            if (Widgets.ButtonText(new Rect(x, row.y + 3f, LevelButtonWidth, 22f), activeLevel.ToString(), active: hasProfile))
            {
                LCRiskLevel nextLevel = activeLevel == LCRiskLevel.ALEPH
                    ? LCRiskLevel.ZAYIN
                    : activeLevel + 1;
                FourColorOverrides.SetWeaponOverride(def, activeType, nextLevel, activeMult);
                ClearRowBuffers("W|" + def.defName);
            }

            x += LevelButtonWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"W|{def.defName}|mult",
                hasProfile ? (float?)activeMult : null, 0.1f, 10f,
                v =>
                {
                    if (v.HasValue)
                    {
                        FourColorDamageType targetType = activeType == FourColorDamageType.None ? FourColorDamageType.Red : activeType;
                        FourColorOverrides.SetWeaponOverride(def, targetType, activeLevel, v.Value);
                    }
                });

            x += FieldWidth + 6f;
            if (Widgets.ButtonText(new Rect(x, row.y + 3f, RevertWidth, 22f), "LC_Page_Revert".Translate(), active: overridden))
            {
                FourColorOverrides.RevertWeapon(def.defName);
                ClearRowBuffers("W|" + def.defName);
            }
        }

        public static void DrawRacePage(Rect rect)
        {
            EnsureLists();

            float y = rect.y;
            DrawTopSearchBar(rect, ref y, ref raceSearch,
                null, ref raceOnlyOverridden,
                ref raceOnlyOverridden,
                () =>
                {
                    FourColorOverrides.RevertAllRaces();
                    ClearBuffers();
                });

            DrawRaceFilterBar(rect, ref y);
            DrawHintLine(rect, ref y, "LC_Page_RaceHint".Translate());

            float fixedWidth = TargetKindWidth + FieldWidth * 4f + RevertWidth + 40f;
            float nameWidth = Mathf.Max(120f, rect.width - ScrollbarWidth - fixedWidth);
            Rect headerRect = new Rect(rect.x, y, rect.width - ScrollbarWidth, HeaderHeight);
            DrawRaceHeader(headerRect, nameWidth);
            y += HeaderHeight + 2f;

            var shown = new List<ThingDef>();
            for (int i = 0; i < raceDefs.Count; i++)
            {
                ThingDef def = raceDefs[i];
                if (!MatchesSearch(def, raceSearch) || !MatchesRaceFilter(def))
                {
                    continue;
                }
                if (raceOnlyOverridden && !FourColorOverrides.HasRaceOverride(def.defName))
                {
                    continue;
                }
                shown.Add(def);
            }

            DrawVirtualRows(
                new Rect(rect.x, y, rect.width, rect.yMax - y),
                ref raceScroll,
                shown,
                (row, def) => DrawRaceRow(row, def, nameWidth));
        }

        private static void DrawRaceFilterBar(Rect rect, ref float y)
        {
            const float btnHeight = 24f;
            string[] labels =
            {
                "LC_Page_FilterAll".Translate(),
                "LC_Page_FilterHumanlike".Translate(),
                "LC_Page_FilterAnimal".Translate(),
                "LC_Page_FilterMechanoid".Translate(),
                "LC_Page_FilterInsect".Translate()
            };

            float width = Mathf.Min(110f, (rect.width - 24f) / labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                Rect btnRect = new Rect(rect.x + i * (width + 4f), y, width, btnHeight);
                if (Widgets.ButtonText(btnRect, labels[i]))
                {
                    raceFilter = i;
                }
                if (raceFilter == i)
                {
                    Widgets.DrawLineHorizontal(btnRect.x, btnRect.yMax + 1f, btnRect.width);
                }
            }

            y += btnHeight + 4f;
        }

        private static bool MatchesRaceFilter(ThingDef def)
        {
            RaceProperties race = def?.race;
            if (race == null)
            {
                return false;
            }

            switch (raceFilter)
            {
                case RaceFilterHumanlike: return race.Humanlike;
                case RaceFilterAnimal: return race.Animal && !race.Insect;
                case RaceFilterMechanoid: return race.IsMechanoid;
                case RaceFilterInsect: return race.Insect;
                default: return true;
            }
        }

        private static void DrawRaceHeader(Rect rect, float nameWidth)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleCenter, false))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.68f);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 4f, rect.y, nameWidth - 4f, HeaderHeight), "LC_Col_Race".Translate());

                Text.Anchor = TextAnchor.MiddleCenter;
                float x = rect.x + nameWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, TargetKindWidth, HeaderHeight), "LC_Col_TargetKind".Translate());
                x += TargetKindWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_Red".Translate());
                x += FieldWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_White".Translate());
                x += FieldWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_Black".Translate());
                x += FieldWidth + 4f;
                Widgets.Label(new Rect(x, rect.y, FieldWidth, HeaderHeight), "LC_Col_Pale".Translate());

                GUI.color = old;
            }
        }

        private static void DrawRaceRow(Rect row, ThingDef def, float nameWidth)
        {
            Widgets.DrawHighlightIfMouseover(row);
            FourColorOverrides.ResolvedRaceProfile profile = FourColorOverrides.GetRaceProfile(def);
            bool overridden = FourColorOverrides.HasRaceOverride(def.defName);
            TooltipHandler.TipRegion(row, $"{def.defName}\n{FourColorDisplayHelper.FormatSource(profile.source)}");

            Color oldColor = GUI.color;
            if (overridden)
            {
                GUI.color = new Color(0.85f, 0.96f, 1f);
            }

            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft, false))
            {
                Widgets.Label(new Rect(row.x + 4f, row.y, nameWidth - 4f, RowHeight), def.LabelCap.ToString().Truncate(nameWidth - 8f));
            }
            GUI.color = oldColor;

            float x = row.x + nameWidth + 4f;
            if (Widgets.ButtonText(new Rect(x, row.y + 3f, TargetKindWidth, 22f), FourColorDisplayHelper.FormatTargetKind(profile.targetKind)))
            {
                FourColorTargetKind next = profile.targetKind == FourColorTargetKind.Abnormality
                    ? FourColorTargetKind.Default
                    : profile.targetKind + 1;
                FourColorOverrides.SetRaceOverride(def, next, profile.red, profile.white, profile.black, profile.pale);
                ClearRowBuffers("R|" + def.defName);
            }

            x += TargetKindWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"R|{def.defName}|r",
                profile.red, 0f, 5f,
                v => FourColorOverrides.SetRaceOverride(def, profile.targetKind, v ?? 1f, profile.white, profile.black, profile.pale));

            x += FieldWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"R|{def.defName}|w",
                profile.white, 0f, 5f,
                v => FourColorOverrides.SetRaceOverride(def, profile.targetKind, profile.red, v ?? 1f, profile.black, profile.pale));

            x += FieldWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"R|{def.defName}|b",
                profile.black, 0f, 5f,
                v => FourColorOverrides.SetRaceOverride(def, profile.targetKind, profile.red, profile.white, v ?? 1f, profile.pale));

            x += FieldWidth + 4f;
            DrawFloatField(new Rect(x, row.y + 3f, FieldWidth, 22f), $"R|{def.defName}|p",
                profile.pale, 0f, 5f,
                v => FourColorOverrides.SetRaceOverride(def, profile.targetKind, profile.red, profile.white, profile.black, v ?? 1f));

            x += FieldWidth + 6f;
            if (Widgets.ButtonText(new Rect(x, row.y + 3f, RevertWidth, 22f), "LC_Page_Revert".Translate(), active: overridden))
            {
                FourColorOverrides.RevertRace(def.defName);
                ClearRowBuffers("R|" + def.defName);
            }
        }

        private static void DrawTopSearchBar(
            Rect rect,
            ref float y,
            ref string search,
            string primaryFilterLabel,
            ref bool primaryFilter,
            ref bool onlyOverridden,
            Action onRevertAll)
        {
            const float barHeight = 26f;
            search = Widgets.TextField(new Rect(rect.x, y, 180f, barHeight), search);

            float curX = rect.x + 190f;
            if (!primaryFilterLabel.NullOrEmpty())
            {
                Widgets.CheckboxLabeled(new Rect(curX, y + 1f, 145f, 24f), primaryFilterLabel, ref primaryFilter);
                curX += 152f;
            }

            Widgets.CheckboxLabeled(new Rect(curX, y + 1f, 145f, 24f), "LC_Page_OnlyOverridden".Translate(), ref onlyOverridden);

            Rect revertAllRect = new Rect(rect.xMax - 130f, y, 130f, barHeight);
            if (Widgets.ButtonText(revertAllRect, "LC_Page_RevertAll".Translate()))
            {
                onRevertAll?.Invoke();
            }

            y += barHeight + 4f;
        }

        private static void DrawHintLine(Rect rect, ref float y, string hint)
        {
            using (new TextBlock(GameFont.Tiny, ColoredText.SubtleGrayColor))
            {
                float h = Text.CalcHeight(hint, rect.width - 4f);
                Widgets.Label(new Rect(rect.x + 2f, y, rect.width - 4f, h), hint);
                y += h + 4f;
            }
        }

        private static void DrawVirtualRows(Rect outRect, ref Vector2 scrollPos, List<ThingDef> items, Action<Rect, ThingDef> drawRow)
        {
            float viewWidth = outRect.width - ScrollbarWidth;
            float viewHeight = Mathf.Max(items.Count * RowHeight + 6f, outRect.height);
            Rect viewRect = new Rect(0f, 0f, viewWidth, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            try
            {
                if (items.Count == 0)
                {
                    Widgets.Label(new Rect(4f, 6f, viewWidth, 24f), "LC_Page_NoMatch".Translate());
                }
                else
                {
                    int first = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / RowHeight));
                    int last = Mathf.Min(items.Count - 1, Mathf.CeilToInt((scrollPos.y + outRect.height) / RowHeight));
                    for (int i = first; i <= last; i++)
                    {
                        drawRow(new Rect(0f, i * RowHeight, viewWidth, RowHeight), items[i]);
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawFloatField(Rect rect, string key, float? current, float min, float max, Action<float?> onChange)
        {
            if (!buffers.TryGetValue(key, out string buffer))
            {
                buffer = current.HasValue
                    ? current.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : string.Empty;
                buffers[key] = buffer;
            }

            string edited = Widgets.TextField(rect, buffer);
            if (edited == buffer)
            {
                return;
            }

            buffers[key] = edited;
            string trimmed = edited.Trim();
            if (trimmed.Length == 0)
            {
                onChange(null);
                return;
            }

            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                onChange(Mathf.Clamp(parsed, min, max));
            }
        }

        private static bool MatchesSearch(ThingDef def, string search)
        {
            if (search.NullOrEmpty())
            {
                return true;
            }

            return def.label?.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0
                || def.defName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ClearRowBuffers(string prefix)
        {
            var toRemove = new List<string>();
            foreach (string k in buffers.Keys)
            {
                if (k.StartsWith(prefix + "|", StringComparison.Ordinal))
                {
                    toRemove.Add(k);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                buffers.Remove(toRemove[i]);
            }
        }
    }
}
