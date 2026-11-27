using System;
using System.Collections.Generic;
using HarmonyLib;
using LCAnomalyCore.Damage;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Misc;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Patch
{
    internal static class FourColorDisplayHelper
    {
        public static string FormatSource(FourColorProfileSource source)
        {
            switch (source)
            {
                case FourColorProfileSource.Authored: return "LC_FourColorSource_Authored".Translate();
                case FourColorProfileSource.DefaultTable: return "LC_FourColorSource_DefaultTable".Translate();
                case FourColorProfileSource.AutoInferred: return "LC_FourColorSource_AutoInferred".Translate();
                case FourColorProfileSource.UserOverride: return "LC_FourColorSource_UserOverride".Translate();
                default: return "LC_FourColorSource_None".Translate();
            }
        }

        public static string FormatDamageType(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return "LC_DamageType_Red".Translate();
                case FourColorDamageType.White: return "LC_DamageType_White".Translate();
                case FourColorDamageType.Black: return "LC_DamageType_Black".Translate();
                case FourColorDamageType.Pale: return "LC_DamageType_Pale".Translate();
                default: return "LC_DamageType_None".Translate();
            }
        }

        public static string FormatTargetKind(FourColorTargetKind kind)
        {
            switch (kind)
            {
                case FourColorTargetKind.Employee: return "LC_TargetKind_Employee".Translate();
                case FourColorTargetKind.Abnormality: return "LC_TargetKind_Abnormality".Translate();
                default: return "LC_TargetKind_Default".Translate();
            }
        }

        public static string GetDamageTypeHex(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return "#F24747";
                case FourColorDamageType.White: return "#F8F3D0";
                case FourColorDamageType.Black: return "#B866F5";
                case FourColorDamageType.Pale: return "#66E6F2";
                default: return "#D0D0D0";
            }
        }

        public static string GetRiskLevelHex(LCRiskLevel level)
        {
            switch (level)
            {
                case LCRiskLevel.ZAYIN: return "#5CE65C";
                case LCRiskLevel.TETH: return "#4DA6FF";
                case LCRiskLevel.HE: return "#FFD23F";
                case LCRiskLevel.WAW: return "#C47DFF";
                case LCRiskLevel.ALEPH: return "#FF4747";
                default: return "#D0D0D0";
            }
        }

        public static string FormatColoredDamageType(FourColorDamageType damageType)
        {
            string label = FormatDamageType(damageType);
            if (damageType == FourColorDamageType.None)
            {
                return label;
            }

            return $"<color={GetDamageTypeHex(damageType)}>{label}</color>";
        }

        public static string FormatColoredWeaponDamageTypes(FourColorDamageType primary, FourColorDamageType secondary)
        {
            if (secondary != FourColorDamageType.None && secondary != primary)
            {
                return $"{FormatColoredDamageType(primary)} / {FormatColoredDamageType(secondary)}";
            }

            return FormatColoredDamageType(primary);
        }

        public static string FormatDamageTypeMechanism(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return "LC_Stat_DamageMechanic_Red".Translate();
                case FourColorDamageType.White: return "LC_Stat_DamageMechanic_White".Translate();
                case FourColorDamageType.Black: return "LC_Stat_DamageMechanic_Black".Translate();
                case FourColorDamageType.Pale: return "LC_Stat_DamageMechanic_Pale".Translate();
                default: return "LC_Stat_DamageMechanic_None".Translate();
            }
        }

        public static string FormatColoredRiskLevel(LCRiskLevel level)
        {
            return $"<color={GetRiskLevelHex(level)}>{level}</color>";
        }

        public static string FormatResistanceTier(FourColorResistanceTier tier)
        {
            switch (tier)
            {
                case FourColorResistanceTier.Immune: return "LC_ResistTier_Immune".Translate();
                case FourColorResistanceTier.Resist: return "LC_ResistTier_Resist".Translate();
                case FourColorResistanceTier.Endure: return "LC_ResistTier_Endure".Translate();
                case FourColorResistanceTier.Weak: return "LC_ResistTier_Weak".Translate();
                case FourColorResistanceTier.Fatal: return "LC_ResistTier_Fatal".Translate();
                default: return "LC_ResistTier_Normal".Translate();
            }
        }

        public static string FormatResistanceValue(float value, FourColorDamageType channel)
        {
            FourColorResistanceTier tier = DamageUtils.ClassifyResistanceTier(value);
            string coloredNum = $"<color={GetDamageTypeHex(channel)}>x{value:0.##}</color>";
            return "LC_Stat_ResistDisplay".Translate(coloredNum, FormatResistanceTier(tier));
        }

        public static string FormatDamageNumber(float value, bool isPercent = false)
        {
            string formatted = value >= 10f ? value.ToString("0.#") : value.ToString("0.##");
            return isPercent ? $"{formatted}%" : formatted;
        }

        public static string FormatDamageRange(float min, float max, bool isPercent = false)
        {
            string minStr = FormatDamageNumber(min, isPercent: false);
            string maxStr = FormatDamageNumber(max, isPercent: false);
            if (Mathf.Approximately(min, max) || minStr == maxStr)
            {
                return isPercent ? $"{maxStr}%" : maxStr;
            }

            return isPercent ? $"{minStr}~{maxStr}%" : $"{minStr}~{maxStr}";
        }

        public static string FormatColoredDamageValue(string text, FourColorDamageType damageType)
        {
            if (damageType == FourColorDamageType.None)
            {
                return text;
            }

            return $"<color={GetDamageTypeHex(damageType)}>{text}</color>";
        }

        public static bool TryResolveEntityCategoryRiskLevel(ThingDef def, out LCRiskLevel level)
        {
            return DamageUtils.TryGetEntityRiskLevel(def, out level);
        }

        public static bool HasWornFourColorArmor(Pawn pawn)
        {
            if (pawn?.apparel == null)
            {
                return false;
            }

            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                Apparel apparel = worn[i];
                if (apparel != null && FourColorOverrides.TryGetApparelProfile(apparel.def, out _))
                {
                    return true;
                }
            }

            return false;
        }

        public static LCRiskLevel GetPawnHighestArmorRiskLevel(Pawn pawn)
        {
            LCRiskLevel highest = LCRiskLevel.ZAYIN;
            if (pawn?.apparel == null)
            {
                return highest;
            }

            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                Apparel apparel = worn[i];
                if (apparel != null && FourColorOverrides.TryGetApparelProfile(apparel.def, out FourColorOverrides.ResolvedApparelProfile ap))
                {
                    if (ap.riskLevel > highest)
                    {
                        highest = ap.riskLevel;
                    }
                }
            }

            return highest;
        }

        public static LCRiskLevel GetPawnDisplayRiskLevel(
            Pawn pawn,
            out LCRiskLevel armorLevel,
            out bool hasEntityCategory,
            out LCRiskLevel entityLevel)
        {
            armorLevel = GetPawnHighestArmorRiskLevel(pawn);
            entityLevel = LCRiskLevel.ZAYIN;
            hasEntityCategory = pawn != null && TryResolveEntityCategoryRiskLevel(pawn.def, out entityLevel);
            if (!hasEntityCategory)
            {
                return armorLevel;
            }

            return entityLevel > armorLevel ? entityLevel : armorLevel;
        }

        public static float ComputeEffectivePawnResistance(
            Pawn pawn,
            FourColorDamageType damageType,
            out float baseRaceResist,
            out bool isDynamic,
            out float apparelResist,
            out string sourceApparelLabel,
            out LCRiskLevel highestApparelLevel)
        {
            FourColorOverrides.ResolvedRaceProfile raceProfile = FourColorOverrides.GetRaceProfile(pawn.def);
            ILCFourColorDynamicResistance dynResist = pawn as ILCFourColorDynamicResistance;
            if (dynResist != null && dynResist.TryGetDynamicFourColorResistance(damageType, out float dynVal))
            {
                baseRaceResist = Mathf.Max(0f, dynVal);
                isDynamic = true;
            }
            else
            {
                baseRaceResist = Mathf.Max(0f, raceProfile.ResistanceFor(damageType));
                isDynamic = false;
            }

            apparelResist = 1f;
            highestApparelLevel = LCRiskLevel.ZAYIN;
            sourceApparelLabel = "None".Translate();

            float bestScore = float.MaxValue;
            if (pawn.apparel != null)
            {
                List<Apparel> worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                {
                    Apparel apparel = worn[i];
                    if (apparel == null || !FourColorOverrides.TryGetApparelProfile(apparel.def, out FourColorOverrides.ResolvedApparelProfile ap))
                    {
                        continue;
                    }

                    if (ap.riskLevel > highestApparelLevel)
                    {
                        highestApparelLevel = ap.riskLevel;
                    }

                    float eff = Mathf.Max(0f, FourColorOverrides.ApplyApparelQuality(ap.ResistanceFor(damageType), apparel, ap.isAuthoredEgo));
                    float score = eff * DamageUtils.GetDamageLevelFactor((int)LCRiskLevel.ZAYIN, (int)ap.riskLevel);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        apparelResist = eff;
                        sourceApparelLabel = apparel.LabelCap;
                    }
                }
            }

            return Mathf.Max(0f, apparelResist * baseRaceResist);
        }
    }

    [HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]
    internal static class Patch_ThingDef_SpecialDisplayStats_FourColor
    {
        private static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, ThingDef __instance, StatRequest req)
        {
            foreach (StatDrawEntry entry in __result)
            {
                yield return entry;
            }

            if (__instance == null || !__instance.IsWeapon || req.Thing == null)
            {
                yield break;
            }

            float blessingMult = GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(req.Thing) ?? 1f;
            if (blessingMult > 1.0001f)
            {
                string bonusPct = $"+{(blessingMult - 1f) * 100f:0.#}%";
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Weapon,
                    "LC_Stat_WeaponBlessing".Translate(),
                    bonusPct,
                    "LC_Stat_WeaponBlessingDesc".Translate(bonusPct),
                    2155);
            }
        }
    }

    [HarmonyPatch(typeof(StatDrawEntry), nameof(StatDrawEntry.Draw))]
    internal static class Patch_StatDrawEntry_Draw_FourColorIcons
    {
        private const float IconSize = 16f;
        private const float IconGap = 2f;
        private const float IconPaddingRight = 4f;

        private static bool Prefix(
            StatDrawEntry __instance,
            ref float __result,
            float x,
            float y,
            float width,
            bool selected,
            bool highlightLabel,
            bool lowlightLabel,
            Action clickedCallback,
            Action mousedOverCallback,
            Vector2 scrollPosition,
            Rect scrollOutRect,
            string valueCached)
        {
            if (__instance?.stat == null || !TryResolveIcons(__instance.stat, __instance.optionalReq, out Texture2D primaryIcon, out Texture2D secondaryIcon))
            {
                return true;
            }

            int iconCount = secondaryIcon != null ? 2 : 1;
            float iconBlockWidth = iconCount * IconSize + (iconCount - 1) * IconGap + IconPaddingRight;
            float valueColWidth = width * 0.45f;
            float textWidth = Mathf.Max(20f, valueColWidth - iconBlockWidth);
            string text = valueCached ?? __instance.ValueString;
            float rowHeight = Mathf.Max(20f, Text.CalcHeight(text, textWidth));
            Rect rect = new Rect(8f, y, width, rowHeight);

            if (!(y - scrollPosition.y + rect.height < 0f) && !(y - scrollPosition.y > scrollOutRect.height))
            {
                Color prevColor = GUI.color;
                try
                {
                    GUI.color = Color.white;
                    if (selected)
                    {
                        Widgets.DrawHighlightSelected(rect);
                    }
                    else if (Mouse.IsOver(rect))
                    {
                        Widgets.DrawHighlight(rect);
                    }

                    if (highlightLabel)
                    {
                        Widgets.DrawTextHighlight(rect);
                    }

                    GUI.color = lowlightLabel ? Color.grey : Color.white;
                    Rect labelRect = rect;
                    labelRect.width -= valueColWidth;
                    Widgets.Label(labelRect, __instance.LabelCap);

                    Rect valueRect = rect;
                    valueRect.x = labelRect.xMax;
                    valueRect.width = valueColWidth;

                    float iconY = valueRect.y + 2f;
                    GUI.color = lowlightLabel ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                    GUI.DrawTexture(new Rect(valueRect.x, iconY, IconSize, IconSize), primaryIcon);
                    if (secondaryIcon != null)
                    {
                        GUI.DrawTexture(new Rect(valueRect.x + IconSize + IconGap, iconY, IconSize, IconSize), secondaryIcon);
                    }

                    GUI.color = lowlightLabel ? Color.grey : Color.white;
                    Rect textRect = new Rect(valueRect.x + iconBlockWidth, valueRect.y, textWidth, valueRect.height);
                    Widgets.Label(textRect, text);

                    GUI.color = Color.white;
                    if (Mouse.IsOver(rect))
                    {
                        StatDef localStat = __instance.stat;
                        TooltipHandler.TipRegion(rect, new TipSignal(() => localStat.LabelCap + ": " + localStat.description, localStat.GetHashCode()));
                    }

                    if (Widgets.ButtonInvisible(rect))
                    {
                        clickedCallback?.Invoke();
                    }

                    if (Mouse.IsOver(rect))
                    {
                        mousedOverCallback?.Invoke();
                    }
                }
                finally
                {
                    GUI.color = prevColor;
                }
            }

            __result = rect.height;
            return false;
        }

        private static bool TryResolveIcons(StatDef stat, StatRequest req, out Texture2D primaryIcon, out Texture2D secondaryIcon)
        {
            primaryIcon = null;
            secondaryIcon = null;

            FourColorDamageType primary = FourColorDamageType.None;
            FourColorDamageType secondary = FourColorDamageType.None;

            if (stat == Defs.StatDefOf.LC_FourColor_ResistRed)
            {
                primary = FourColorDamageType.Red;
            }
            else if (stat == Defs.StatDefOf.LC_FourColor_ResistWhite)
            {
                primary = FourColorDamageType.White;
            }
            else if (stat == Defs.StatDefOf.LC_FourColor_ResistBlack)
            {
                primary = FourColorDamageType.Black;
            }
            else if (stat == Defs.StatDefOf.LC_FourColor_ResistPale)
            {
                primary = FourColorDamageType.Pale;
            }
            else if (stat == Defs.StatDefOf.LC_FourColor_DamageType || stat == Defs.StatDefOf.LC_FourColor_WeaponDamage)
            {
                if (!req.Empty && req.Def is ThingDef td && FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
                {
                    primary = wp.damageType;
                    secondary = wp.secondaryDamageType;
                }
            }
            else if (stat == Defs.StatDefOf.LC_FourColor_SkillDamage)
            {
                FourColorSkillDamageUtility.ResolveSkillIcons(req, out primary, out secondary);
            }

            primaryIcon = FourColorPopupTextures.GetIcon(primary);
            if (secondary != FourColorDamageType.None && secondary != primary)
            {
                secondaryIcon = FourColorPopupTextures.GetIcon(secondary);
            }

            if (primaryIcon == null && secondaryIcon != null)
            {
                primaryIcon = secondaryIcon;
                secondaryIcon = null;
            }

            return primaryIcon != null;
        }
    }
}
