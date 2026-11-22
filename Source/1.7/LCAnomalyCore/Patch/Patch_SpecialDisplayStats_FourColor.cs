using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using LCAnomalyCore.Damage;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Interface;
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

            if (__instance == null)
            {
                yield break;
            }

            float blessingMult = (__instance.IsWeapon && req.Thing != null)
                ? (GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(req.Thing) ?? 1f)
                : 1f;

            if (__instance.IsApparel && FourColorOverrides.TryGetApparelProfile(__instance, out FourColorOverrides.ResolvedApparelProfile ap))
            {
                Thing apparelThing = req.Thing;
                float qualityFactor = apparelThing != null
                    ? FourColorOverrides.GetApparelQualityFactor(apparelThing, ap.isAuthoredEgo)
                    : 1f;
                float red = apparelThing != null ? FourColorOverrides.ApplyApparelQuality(ap.red, apparelThing, ap.isAuthoredEgo) : ap.red;
                float white = apparelThing != null ? FourColorOverrides.ApplyApparelQuality(ap.white, apparelThing, ap.isAuthoredEgo) : ap.white;
                float black = apparelThing != null ? FourColorOverrides.ApplyApparelQuality(ap.black, apparelThing, ap.isAuthoredEgo) : ap.black;
                float pale = apparelThing != null ? FourColorOverrides.ApplyApparelQuality(ap.pale, apparelThing, ap.isAuthoredEgo) : ap.pale;

                string valueText = $"{ap.riskLevel} (R {red:0.##} / W {white:0.##} / B {black:0.##} / P {pale:0.##})";
                var sb = new StringBuilder();
                sb.AppendLine("LC_Stat_FourColorArmorDesc".Translate());
                sb.AppendLine();
                sb.AppendLine("LC_Stat_RiskLevel".Translate(ap.riskLevel.ToString()));
                sb.AppendLine("LC_Stat_ResistLine".Translate("LC_DamageType_Red".Translate(), red.ToString("0.##"), ap.red.ToString("0.##")));
                sb.AppendLine("LC_Stat_ResistLine".Translate("LC_DamageType_White".Translate(), white.ToString("0.##"), ap.white.ToString("0.##")));
                sb.AppendLine("LC_Stat_ResistLine".Translate("LC_DamageType_Black".Translate(), black.ToString("0.##"), ap.black.ToString("0.##")));
                sb.AppendLine("LC_Stat_ResistLine".Translate("LC_DamageType_Pale".Translate(), pale.ToString("0.##"), ap.pale.ToString("0.##")));
                if (apparelThing != null)
                {
                    sb.AppendLine("LC_Stat_QualityFactor".Translate(qualityFactor.ToString("0.##")));
                }
                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(ap.source)));

                yield return new StatDrawEntry(
                    StatCategoryDefOf.Apparel,
                    "LC_Stat_FourColorArmor".Translate(),
                    valueText,
                    sb.ToString().TrimEndNewlines(),
                    2150);
            }
            else if (__instance.IsWeapon && FourColorOverrides.TryGetWeaponProfile(__instance, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                Thing weaponThing = req.Thing;
                float qualityFactor = weaponThing != null
                    ? FourColorOverrides.GetWeaponQualityFactor(weaponThing, wp.isAuthoredEgo)
                    : 1f;
                float effMult = wp.damageMultiplier * qualityFactor * blessingMult;
                string typeLabel = FourColorDisplayHelper.FormatDamageType(wp.damageType);
                string valueText = $"{typeLabel} · {wp.riskLevel} (x{effMult:0.##})";

                var sb = new StringBuilder();
                sb.AppendLine("LC_Stat_FourColorWeaponDesc".Translate());
                sb.AppendLine();
                sb.AppendLine("LC_Stat_WeaponDamageType".Translate(typeLabel));
                sb.AppendLine("LC_Stat_RiskLevel".Translate(wp.riskLevel.ToString()));
                sb.AppendLine("LC_Stat_WeaponMultiplier".Translate(wp.damageMultiplier.ToString("0.##"), effMult.ToString("0.##")));
                if (weaponThing != null)
                {
                    sb.AppendLine("LC_Stat_QualityFactor".Translate(qualityFactor.ToString("0.##")));
                }
                if (blessingMult > 1.0001f)
                {
                    string bonusPct = $"+{(blessingMult - 1f) * 100f:0.#}%";
                    sb.AppendLine("LC_Stat_WeaponBlessingLine".Translate(bonusPct));
                }
                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(wp.source)));

                yield return new StatDrawEntry(
                    StatCategoryDefOf.Weapon,
                    "LC_Stat_FourColorWeapon".Translate(),
                    valueText,
                    sb.ToString().TrimEndNewlines(),
                    2150);
            }

            if (__instance.IsWeapon && blessingMult > 1.0001f)
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

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpecialDisplayStats))]
    internal static class Patch_Pawn_SpecialDisplayStats_FourColor
    {
        private static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, Pawn __instance)
        {
            foreach (StatDrawEntry entry in __result)
            {
                yield return entry;
            }

            if (__instance == null)
            {
                yield break;
            }

            FourColorOverrides.ResolvedRaceProfile raceProfile = FourColorOverrides.GetRaceProfile(__instance.def);
            LCRiskLevel bestLevel = LCRiskLevel.ZAYIN;

            ILCFourColorDynamicResistance dynResist = __instance as ILCFourColorDynamicResistance;
            float baseRed = (dynResist != null && dynResist.TryGetDynamicFourColorResistance(FourColorDamageType.Red, out float dynRed)) ? dynRed : raceProfile.red;
            float baseWhite = (dynResist != null && dynResist.TryGetDynamicFourColorResistance(FourColorDamageType.White, out float dynWhite)) ? dynWhite : raceProfile.white;
            float baseBlack = (dynResist != null && dynResist.TryGetDynamicFourColorResistance(FourColorDamageType.Black, out float dynBlack)) ? dynBlack : raceProfile.black;
            float basePale = (dynResist != null && dynResist.TryGetDynamicFourColorResistance(FourColorDamageType.Pale, out float dynPale)) ? dynPale : raceProfile.pale;

            float red = ComputeEffectivePawnResistance(__instance, FourColorDamageType.Red, baseRed, ref bestLevel, out string redApparel);
            float white = ComputeEffectivePawnResistance(__instance, FourColorDamageType.White, baseWhite, ref bestLevel, out string whiteApparel);
            float black = ComputeEffectivePawnResistance(__instance, FourColorDamageType.Black, baseBlack, ref bestLevel, out string blackApparel);
            float pale = ComputeEffectivePawnResistance(__instance, FourColorDamageType.Pale, basePale, ref bestLevel, out string paleApparel);

            string valueText = $"{bestLevel} (R {red:0.##} / W {white:0.##} / B {black:0.##} / P {pale:0.##})";
            bool takesMental = DamageUtils.CanTakeMentalDamage(__instance);

            var sb = new StringBuilder();
            sb.AppendLine("LC_Stat_FourColorPawnDesc".Translate());
            sb.AppendLine();
            sb.AppendLine("LC_Stat_RiskLevel".Translate(bestLevel.ToString()));
            sb.AppendLine("LC_Stat_PawnMentalSusceptible".Translate(takesMental ? "Yes".Translate() : "No".Translate()));
            sb.AppendLine("LC_Stat_PawnTargetKind".Translate(FourColorDisplayHelper.FormatTargetKind(raceProfile.targetKind)));
            sb.AppendLine("LC_Stat_PawnResistRow".Translate("LC_DamageType_Red".Translate(), red.ToString("0.##"), baseRed.ToString("0.##"), redApparel));
            sb.AppendLine("LC_Stat_PawnResistRow".Translate("LC_DamageType_White".Translate(), white.ToString("0.##"), baseWhite.ToString("0.##"), whiteApparel));
            sb.AppendLine("LC_Stat_PawnResistRow".Translate("LC_DamageType_Black".Translate(), black.ToString("0.##"), baseBlack.ToString("0.##"), blackApparel));
            sb.AppendLine("LC_Stat_PawnResistRow".Translate("LC_DamageType_Pale".Translate(), pale.ToString("0.##"), basePale.ToString("0.##"), paleApparel));
            sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(raceProfile.source)));

            yield return new StatDrawEntry(
                StatCategoryDefOf.PawnResistances,
                "LC_Stat_FourColorPawn".Translate(),
                valueText,
                sb.ToString().TrimEndNewlines(),
                2150);
        }

        private static float ComputeEffectivePawnResistance(
            Pawn pawn,
            FourColorDamageType damageType,
            float raceResist,
            ref LCRiskLevel highestApparelLevel,
            out string sourceApparelLabel)
        {
            float bestApparelResist = 1f;
            float bestScore = float.MaxValue;
            sourceApparelLabel = "None".Translate();

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

                    float eff = FourColorOverrides.ApplyApparelQuality(ap.ResistanceFor(damageType), apparel, ap.isAuthoredEgo);
                    float score = eff * DamageUtils.GetDamageLevelFactor((int)LCRiskLevel.ZAYIN, (int)ap.riskLevel);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestApparelResist = eff;
                        sourceApparelLabel = apparel.LabelCap;
                    }
                }
            }

            return Mathf.Max(0f, bestApparelResist * raceResist);
        }
    }
}
