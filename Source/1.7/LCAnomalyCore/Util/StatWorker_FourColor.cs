using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LCAnomalyCore.Damage;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Patch;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>解析 E.G.O 武器附带主动技能的四色伤害阶段与图标元数据。</summary>
    internal static class FourColorSkillDamageUtility
    {
        internal struct ResolvedSkillStage
        {
            public float min;
            public float max;
            public int hitCount;
            public bool isPercent;
            public string label;
        }

        internal struct ResolvedSkillInfo
        {
            public AbilityDef abilityDef;
            public FourColorDamageType primaryType;
            public FourColorDamageType secondaryType;
            public float weaponEffMultiplier;
            public float totalMin;
            public float totalMax;
            public bool isPercent;
            public List<ResolvedSkillStage> stages;
        }

        public static void ResolveSkillIcons(StatRequest req, out FourColorDamageType primary, out FourColorDamageType secondary)
        {
            if (TryResolveSkillInfo(req, out ResolvedSkillInfo info))
            {
                primary = info.primaryType;
                secondary = info.secondaryType;
                return;
            }

            primary = FourColorDamageType.None;
            secondary = FourColorDamageType.None;
        }

        public static bool TryResolveSkillInfo(StatRequest req, out ResolvedSkillInfo info)
        {
            info = default;
            if (req.Empty)
            {
                return false;
            }

            AbilityDef abilityDef = null;
            FourColorDamageType primaryType = FourColorDamageType.None;
            FourColorDamageType secondaryType = FourColorDamageType.None;
            float weaponEffMult = 1f;

            if (req.Def is AbilityDef directAbility)
            {
                abilityDef = directAbility;
            }
            else if (req.Def is ThingDef weaponDef && weaponDef.IsWeapon)
            {
                CompProperties_EquippableAbility eqComp = weaponDef.comps?.OfType<CompProperties_EquippableAbility>().FirstOrDefault();
                abilityDef = eqComp?.abilityDef;
                if (FourColorOverrides.TryGetWeaponProfile(weaponDef, out FourColorOverrides.ResolvedWeaponProfile wp))
                {
                    primaryType = wp.damageType;
                    secondaryType = wp.secondaryDamageType;
                    float qualityFactor = req.HasThing ? FourColorOverrides.GetWeaponQualityFactor(req.Thing, wp.isAuthoredEgo) : 1f;
                    float blessingMult = req.HasThing ? (GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(req.Thing) ?? 1f) : 1f;
                    weaponEffMult = Mathf.Max(0f, wp.damageMultiplier) * qualityFactor * blessingMult;
                }
                else
                {
                    return false;
                }
            }

            if (abilityDef == null)
            {
                return false;
            }

            FourColorAbilityDamageExtension ext = abilityDef.GetModExtension<FourColorAbilityDamageExtension>();
            if (ext != null)
            {
                if (ext.damageType != FourColorDamageType.None)
                {
                    primaryType = ext.damageType;
                }

                if (ext.secondaryDamageType != FourColorDamageType.None)
                {
                    secondaryType = ext.secondaryDamageType;
                }
            }

            var stages = new List<ResolvedSkillStage>();
            bool defaultPercent = primaryType == FourColorDamageType.Pale;

            if (ext?.stages != null && ext.stages.Count > 0)
            {
                for (int i = 0; i < ext.stages.Count; i++)
                {
                    FourColorAbilityDamageStage stage = ext.stages[i];
                    if (stage == null)
                    {
                        continue;
                    }

                    FloatRange range = stage.fallbackRange;
                    if (!stage.fieldName.NullOrEmpty() && !TryReadAbilityDamageField(abilityDef, stage.fieldName, out range))
                    {
                        range = stage.fallbackRange;
                    }

                    float factor = stage.damageFactor > 0f ? stage.damageFactor : 1f;
                    int hitCount = Mathf.Max(1, stage.hitCount);
                    float sMin = Mathf.Max(0f, range.min * factor * weaponEffMult);
                    float sMax = Mathf.Max(sMin, range.max * factor * weaponEffMult);
                    if (sMax <= 0f)
                    {
                        continue;
                    }

                    string label = !stage.labelKey.NullOrEmpty()
                        ? stage.labelKey.Translate().ToString()
                        : FormatStageFieldName(stage.fieldName);
                    stages.Add(new ResolvedSkillStage
                    {
                        min = sMin,
                        max = sMax,
                        hitCount = hitCount,
                        isPercent = stage.isPercent || defaultPercent,
                        label = label
                    });
                }
            }
            else if (abilityDef.comps != null)
            {
                for (int i = 0; i < abilityDef.comps.Count; i++)
                {
                    AbilityCompProperties compProps = abilityDef.comps[i];
                    if (compProps == null)
                    {
                        continue;
                    }

                    FieldInfo[] fields = compProps.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                    for (int j = 0; j < fields.Length; j++)
                    {
                        FieldInfo fi = fields[j];
                        if (!IsDamageFieldName(fi.Name) || !TryExtractRange(fi.GetValue(compProps), out FloatRange range))
                        {
                            continue;
                        }

                        float sMin = Mathf.Max(0f, range.min * weaponEffMult);
                        float sMax = Mathf.Max(sMin, range.max * weaponEffMult);
                        if (sMax <= 0f)
                        {
                            continue;
                        }

                        stages.Add(new ResolvedSkillStage
                        {
                            min = sMin,
                            max = sMax,
                            hitCount = 1,
                            isPercent = defaultPercent,
                            label = FormatStageFieldName(fi.Name)
                        });
                    }
                }
            }

            if (stages.Count == 0)
            {
                return false;
            }

            float totalMin = 0f;
            float totalMax = 0f;
            bool anyPercent = false;
            for (int i = 0; i < stages.Count; i++)
            {
                totalMin += stages[i].min * stages[i].hitCount;
                totalMax += stages[i].max * stages[i].hitCount;
                if (stages[i].isPercent)
                {
                    anyPercent = true;
                }
            }

            if (totalMax <= 0f)
            {
                return false;
            }

            info = new ResolvedSkillInfo
            {
                abilityDef = abilityDef,
                primaryType = primaryType,
                secondaryType = secondaryType,
                weaponEffMultiplier = weaponEffMult,
                totalMin = totalMin,
                totalMax = totalMax,
                isPercent = anyPercent || defaultPercent,
                stages = stages
            };
            return true;
        }

        private static bool IsDamageFieldName(string name)
        {
            if (name.NullOrEmpty())
            {
                return false;
            }

            if (name.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return name.IndexOf("Multiplier", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Factor", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool TryReadAbilityDamageField(AbilityDef abilityDef, string fieldName, out FloatRange range)
        {
            range = FloatRange.Zero;
            if (abilityDef?.comps == null || fieldName.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < abilityDef.comps.Count; i++)
            {
                AbilityCompProperties compProps = abilityDef.comps[i];
                if (compProps == null)
                {
                    continue;
                }

                FieldInfo fi = compProps.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (fi != null && TryExtractRange(fi.GetValue(compProps), out range))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractRange(object rawValue, out FloatRange range)
        {
            if (rawValue is FloatRange fr)
            {
                range = fr;
                return true;
            }

            if (rawValue is float f)
            {
                range = new FloatRange(f, f);
                return true;
            }

            if (rawValue is int iv)
            {
                range = new FloatRange(iv, iv);
                return true;
            }

            range = FloatRange.Zero;
            return false;
        }

        private static string FormatStageFieldName(string fieldName)
        {
            string stem = null;
            if (!fieldName.NullOrEmpty())
            {
                string trimmed = fieldName;
                foreach (string suffix in new[] { "PaleDamage", "DamageRange", "Range", "Damage" })
                {
                    if (string.Equals(trimmed, suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        trimmed = string.Empty;
                        break;
                    }

                    if (trimmed.Length > suffix.Length && trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        trimmed = trimmed.Substring(0, trimmed.Length - suffix.Length);
                        break;
                    }
                }

                if (!trimmed.NullOrEmpty())
                {
                    stem = trimmed.CapitalizeFirst();
                }
            }

            if (stem.NullOrEmpty())
            {
                stem = "Hit";
            }

            string key = "LC_SkillStage_" + stem;
            return key.CanTranslate() ? key.Translate().ToString() : stem;
        }
    }

    /// <summary>E.G.O / 四色风险等级（ZAYIN ~ ALEPH）专用信息面板属性。</summary>
    public class StatWorker_FourColorRiskLevel : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            if (req.Empty || !(req.Def is ThingDef td))
            {
                return false;
            }

            if (td.IsApparel && FourColorOverrides.TryGetApparelProfile(td, out _))
            {
                return true;
            }

            if (td.IsWeapon && FourColorOverrides.TryGetWeaponProfile(td, out _))
            {
                return true;
            }

            if (req.Thing is Pawn)
            {
                return true;
            }

            return td.race != null && FourColorDisplayHelper.TryResolveEntityCategoryRiskLevel(td, out _);
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            return (float)ResolveRiskLevel(req);
        }

        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
        {
            LCRiskLevel level = !optionalReq.Empty
                ? ResolveRiskLevel(optionalReq)
                : ClampRiskLevel(Mathf.RoundToInt(value));
            return FourColorDisplayHelper.FormatColoredRiskLevel(level);
        }

        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            return FourColorDisplayHelper.FormatColoredRiskLevel(ClampRiskLevel(Mathf.RoundToInt(val)));
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (req.Empty || !(req.Def is ThingDef td))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            if (td.IsApparel && FourColorOverrides.TryGetApparelProfile(td, out FourColorOverrides.ResolvedApparelProfile ap))
            {
                sb.AppendLine("LC_Stat_RiskLevel".Translate(ap.riskLevel.ToString()));
                sb.AppendLine("LC_Stat_RiskLevelSuppressedNote".Translate());
                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(ap.source)));
            }
            else if (td.IsWeapon && FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                sb.AppendLine("LC_Stat_RiskLevel".Translate(wp.riskLevel.ToString()));
                sb.AppendLine("LC_Stat_RiskLevelSuppressedNote".Translate());
                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(wp.source)));
            }
            else if (req.Thing is Pawn pawn)
            {
                FourColorDisplayHelper.GetPawnDisplayRiskLevel(pawn, out LCRiskLevel armorLevel, out bool hasEntityCat, out LCRiskLevel entityLevel);
                bool hasWornArmor = FourColorDisplayHelper.HasWornFourColorArmor(pawn);
                if (hasEntityCat)
                {
                    sb.AppendLine("LC_Stat_PawnEntityRiskLevel".Translate(entityLevel.ToString()));
                }
                if (!hasEntityCat || hasWornArmor)
                {
                    sb.AppendLine("LC_Stat_PawnArmorRiskLevel".Translate(armorLevel.ToString()));
                }
                FourColorOverrides.ResolvedRaceProfile raceProfile = FourColorOverrides.GetRaceProfile(td);
                sb.AppendLine("LC_Stat_PawnTargetKind".Translate(FourColorDisplayHelper.FormatTargetKind(raceProfile.targetKind)));
                sb.AppendLine("LC_Stat_PawnMentalSusceptible".Translate(DamageUtils.CanTakeMentalDamage(pawn) ? "Yes".Translate() : "No".Translate()));
                sb.AppendLine("LC_Stat_RiskLevelSuppressedNote".Translate());
                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(raceProfile.source)));
            }
            else if (td.race != null && FourColorDisplayHelper.TryResolveEntityCategoryRiskLevel(td, out LCRiskLevel raceEntityLevel))
            {
                FourColorOverrides.ResolvedRaceProfile raceProfile = FourColorOverrides.GetRaceProfile(td);
                sb.AppendLine("LC_Stat_PawnEntityRiskLevel".Translate(raceEntityLevel.ToString()));
                sb.AppendLine("LC_Stat_PawnTargetKind".Translate(FourColorDisplayHelper.FormatTargetKind(raceProfile.targetKind)));
                sb.AppendLine("LC_Stat_RiskLevelSuppressedNote".Translate());
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            LCRiskLevel level = !req.Empty
                ? ResolveRiskLevel(req)
                : ClampRiskLevel(Mathf.RoundToInt(finalVal));
            return "LC_Stat_FinalRiskLevel".Translate(level.ToString());
        }

        private static LCRiskLevel ResolveRiskLevel(StatRequest req)
        {
            if (req.Def is ThingDef td)
            {
                if (td.IsApparel && FourColorOverrides.TryGetApparelProfile(td, out FourColorOverrides.ResolvedApparelProfile ap))
                {
                    return ap.riskLevel;
                }

                if (td.IsWeapon && FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
                {
                    return wp.riskLevel;
                }

                if (req.Thing is Pawn pawn)
                {
                    return FourColorDisplayHelper.GetPawnDisplayRiskLevel(pawn, out _, out _, out _);
                }

                if (FourColorDisplayHelper.TryResolveEntityCategoryRiskLevel(td, out LCRiskLevel entityLevel))
                {
                    return entityLevel;
                }
            }

            return LCRiskLevel.ZAYIN;
        }

        private static LCRiskLevel ClampRiskLevel(int raw)
        {
            if (raw < (int)LCRiskLevel.ZAYIN) return LCRiskLevel.ZAYIN;
            if (raw > (int)LCRiskLevel.ALEPH) return LCRiskLevel.ALEPH;
            return (LCRiskLevel)raw;
        }
    }

    /// <summary>E.G.O / 四色武器伤害类型专用信息面板属性。</summary>
    public class StatWorker_FourColorDamageType : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            return !req.Empty
                && req.Def is ThingDef td
                && td.IsWeapon
                && FourColorOverrides.TryGetWeaponProfile(td, out _);
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (req.Def is ThingDef td && FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                return (float)wp.damageType;
            }

            return 0f;
        }

        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
        {
            if (!optionalReq.Empty
                && optionalReq.Def is ThingDef td
                && FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                return FourColorDisplayHelper.FormatColoredWeaponDamageTypes(wp.damageType, wp.secondaryDamageType);
            }

            return ValueToString(value, finalized, numberSense);
        }

        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            int raw = Mathf.Clamp(Mathf.RoundToInt(val), (int)FourColorDamageType.None, (int)FourColorDamageType.Pale);
            return FourColorDisplayHelper.FormatColoredDamageType((FourColorDamageType)raw);
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (req.Empty || !(req.Def is ThingDef td) || !FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            string primaryLabel = FourColorDisplayHelper.FormatDamageType(wp.damageType);
            sb.AppendLine("LC_Stat_WeaponDamageType".Translate(primaryLabel));
            sb.AppendLine(FourColorDisplayHelper.FormatDamageTypeMechanism(wp.damageType));

            if (wp.secondaryDamageType != FourColorDamageType.None && wp.secondaryDamageType != wp.damageType)
            {
                sb.AppendLine();
                sb.AppendLine("LC_Stat_WeaponSecondaryDamageType".Translate(FourColorDisplayHelper.FormatDamageType(wp.secondaryDamageType)));
                sb.AppendLine(FourColorDisplayHelper.FormatDamageTypeMechanism(wp.secondaryDamageType));
            }

            sb.AppendLine();
            sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(wp.source)));
            return sb.ToString().TrimEndNewlines();
        }

        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            if (!req.Empty
                && req.Def is ThingDef td
                && FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                string typeText = wp.secondaryDamageType != FourColorDamageType.None && wp.secondaryDamageType != wp.damageType
                    ? $"{FourColorDisplayHelper.FormatDamageType(wp.damageType)} / {FourColorDisplayHelper.FormatDamageType(wp.secondaryDamageType)}"
                    : FourColorDisplayHelper.FormatDamageType(wp.damageType);
                return "LC_Stat_FinalDamageType".Translate(typeText);
            }

            int raw = Mathf.Clamp(Mathf.RoundToInt(finalVal), (int)FourColorDamageType.None, (int)FourColorDamageType.Pale);
            return "LC_Stat_FinalDamageType".Translate(FourColorDisplayHelper.FormatDamageType((FourColorDamageType)raw));
        }
    }

    /// <summary>E.G.O 武器常规攻击的四色伤害数值信息面板属性。</summary>
    public class StatWorker_FourColorWeaponDamage : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            return !req.Empty
                && req.Def is ThingDef td
                && td.IsWeapon
                && FourColorOverrides.TryGetWeaponProfile(td, out _);
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (!TryComputeWeaponDamage(req, out _, out _, out _, out float avgOrMaxValue, out _, out _))
            {
                return 0f;
            }

            return avgOrMaxValue;
        }

        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
        {
            if (TryComputeWeaponDamage(optionalReq, out FourColorOverrides.ResolvedWeaponProfile wp, out _, out string displayText, out _, out _, out _))
            {
                return FourColorDisplayHelper.FormatColoredDamageValue(displayText, wp.damageType);
            }

            return ValueToString(value, finalized, numberSense);
        }

        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            return FourColorDisplayHelper.FormatDamageNumber(val);
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (!TryComputeWeaponDamage(req, out FourColorOverrides.ResolvedWeaponProfile wp, out float effMult, out _, out _, out VerbProperties primaryVerb, out ThingDef td))
            {
                return string.Empty;
            }

            bool isPale = wp.damageType == FourColorDamageType.Pale;
            var sb = new StringBuilder();
            string typeText = wp.secondaryDamageType != FourColorDamageType.None && wp.secondaryDamageType != wp.damageType
                ? $"{FourColorDisplayHelper.FormatDamageType(wp.damageType)} / {FourColorDisplayHelper.FormatDamageType(wp.secondaryDamageType)}"
                : FourColorDisplayHelper.FormatDamageType(wp.damageType);
            sb.AppendLine("LC_Stat_WeaponDamageType".Translate(typeText));

            if (primaryVerb != null && primaryVerb.defaultProjectile?.projectile != null)
            {
                ProjectileProperties proj = primaryVerb.defaultProjectile.projectile;
                float baseShot = req.HasThing
                    ? proj.GetDamageAmount(req.Thing)
                    : proj.GetDamageAmount(td, req.StuffDef);
                float effShot = baseShot * effMult;
                int burst = Mathf.Max(1, primaryVerb.burstShotCount);
                float ap = proj.GetArmorPenetration(req.Thing);

                sb.AppendLine("LC_Stat_WeaponModeRanged".Translate());
                sb.AppendLine("LC_Stat_WeaponProjDamageLine".Translate(
                    FourColorDisplayHelper.FormatDamageNumber(effShot, isPale),
                    FourColorDisplayHelper.FormatDamageNumber(baseShot, isPale)));
                if (burst > 1)
                {
                    sb.AppendLine("LC_Stat_WeaponBurstLine".Translate(
                        burst.ToString(),
                        FourColorDisplayHelper.FormatDamageNumber(effShot * burst, isPale)));
                }
                sb.AppendLine("LC_Stat_WeaponApRangeLine".Translate($"{ap * 100f:0.#}%", $"{primaryVerb.range:0.#}"));
            }
            else
            {
                sb.AppendLine("LC_Stat_WeaponModeMelee".Translate());
            }

            if (!td.tools.NullOrEmpty())
            {
                float meleeMult = req.HasThing
                    ? req.Thing.GetStatValue(RimWorld.StatDefOf.MeleeWeapon_DamageMultiplier)
                    : td.GetStatValueAbstract(RimWorld.StatDefOf.MeleeWeapon_DamageMultiplier, req.StuffDef);
                if (meleeMult <= 0f)
                {
                    meleeMult = 1f;
                }

                for (int i = 0; i < td.tools.Count; i++)
                {
                    Tool tool = td.tools[i];
                    if (tool == null)
                    {
                        continue;
                    }

                    float effTool = tool.power * meleeMult * effMult;
                    float ap = tool.armorPenetration >= 0f ? tool.armorPenetration : tool.power * 0.015f;
                    string toolLabel = !tool.LabelCap.NullOrEmpty() ? tool.LabelCap.ToString() : (tool.label ?? "Melee");
                    sb.AppendLine("LC_Stat_WeaponMeleeToolLine".Translate(
                        toolLabel,
                        FourColorDisplayHelper.FormatDamageNumber(effTool, isPale),
                        tool.cooldownTime.ToString("0.##"),
                        $"{ap * 100f:0.#}%"));
                }
            }

            if (isPale)
            {
                sb.AppendLine("LC_Stat_PalePercentNote".Translate());
            }

            if (wp.guaranteedHit)
            {
                sb.AppendLine("LC_Stat_WeaponGuaranteedHit".Translate());
            }

            sb.AppendLine();
            sb.AppendLine("LC_Stat_WeaponMultiplier".Translate(wp.damageMultiplier.ToString("0.##"), effMult.ToString("0.##")));
            if (req.HasThing)
            {
                float qualityFactor = FourColorOverrides.GetWeaponQualityFactor(req.Thing, wp.isAuthoredEgo);
                sb.AppendLine("LC_Stat_QualityFactor".Translate(qualityFactor.ToString("0.##")));
                float blessingMult = GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(req.Thing) ?? 1f;
                if (blessingMult > 1.0001f)
                {
                    sb.AppendLine("LC_Stat_WeaponBlessingLine".Translate($"+{(blessingMult - 1f) * 100f:0.#}%"));
                }
            }
            sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(wp.source)));
            return sb.ToString().TrimEndNewlines();
        }

        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            if (TryComputeWeaponDamage(req, out _, out _, out string displayText, out _, out _, out _))
            {
                return "LC_Stat_FinalWeaponDamage".Translate(displayText);
            }

            return "LC_Stat_FinalWeaponDamage".Translate(FourColorDisplayHelper.FormatDamageNumber(finalVal));
        }

        private static bool TryComputeWeaponDamage(
            StatRequest req,
            out FourColorOverrides.ResolvedWeaponProfile wp,
            out float effMult,
            out string displayText,
            out float numericValue,
            out VerbProperties primaryVerb,
            out ThingDef weaponDef)
        {
            wp = default;
            effMult = 1f;
            displayText = string.Empty;
            numericValue = 0f;
            primaryVerb = null;
            weaponDef = req.Def as ThingDef;

            if (req.Empty || weaponDef == null || !weaponDef.IsWeapon || !FourColorOverrides.TryGetWeaponProfile(weaponDef, out wp))
            {
                return false;
            }

            float qualityFactor = req.HasThing ? FourColorOverrides.GetWeaponQualityFactor(req.Thing, wp.isAuthoredEgo) : 1f;
            float blessingMult = req.HasThing ? (GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(req.Thing) ?? 1f) : 1f;
            effMult = Mathf.Max(0f, wp.damageMultiplier) * qualityFactor * blessingMult;
            bool isPale = wp.damageType == FourColorDamageType.Pale;

            primaryVerb = weaponDef.Verbs?.FirstOrDefault(v => v != null && v.defaultProjectile?.projectile != null);
            if (primaryVerb != null)
            {
                ProjectileProperties proj = primaryVerb.defaultProjectile.projectile;
                float baseShot = req.HasThing
                    ? proj.GetDamageAmount(req.Thing)
                    : proj.GetDamageAmount(weaponDef, req.StuffDef);
                float effShot = Mathf.Max(0f, baseShot * effMult);
                int burst = Mathf.Max(1, primaryVerb.burstShotCount);
                numericValue = effShot * burst;
                string shotText = FourColorDisplayHelper.FormatDamageNumber(effShot, isPale);
                displayText = burst > 1 ? $"{shotText} × {burst}" : shotText;
                return true;
            }

            if (!weaponDef.tools.NullOrEmpty())
            {
                float meleeMult = req.HasThing
                    ? req.Thing.GetStatValue(RimWorld.StatDefOf.MeleeWeapon_DamageMultiplier)
                    : weaponDef.GetStatValueAbstract(RimWorld.StatDefOf.MeleeWeapon_DamageMultiplier, req.StuffDef);
                if (meleeMult <= 0f)
                {
                    meleeMult = 1f;
                }

                float min = float.MaxValue;
                float max = 0f;
                for (int i = 0; i < weaponDef.tools.Count; i++)
                {
                    Tool tool = weaponDef.tools[i];
                    if (tool == null || tool.power <= 0f)
                    {
                        continue;
                    }

                    float effTool = tool.power * meleeMult * effMult;
                    if (effTool < min) min = effTool;
                    if (effTool > max) max = effTool;
                }

                if (max > 0f && min <= max)
                {
                    numericValue = max;
                    displayText = FourColorDisplayHelper.FormatDamageRange(min, max, isPale);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>E.G.O 武器附带主动技能的四色总伤害与分段明细信息面板属性。</summary>
    public class StatWorker_FourColorSkillDamage : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            return FourColorSkillDamageUtility.TryResolveSkillInfo(req, out _);
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (!FourColorSkillDamageUtility.TryResolveSkillInfo(req, out FourColorSkillDamageUtility.ResolvedSkillInfo info))
            {
                return 0f;
            }

            return (info.totalMin + info.totalMax) * 0.5f;
        }

        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
        {
            if (FourColorSkillDamageUtility.TryResolveSkillInfo(optionalReq, out FourColorSkillDamageUtility.ResolvedSkillInfo info))
            {
                string rangeText = FourColorDisplayHelper.FormatDamageRange(info.totalMin, info.totalMax, info.isPercent);
                return FourColorDisplayHelper.FormatColoredDamageValue(rangeText, info.primaryType);
            }

            return ValueToString(value, finalized, numberSense);
        }

        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            return FourColorDisplayHelper.FormatDamageNumber(val);
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (!FourColorSkillDamageUtility.TryResolveSkillInfo(req, out FourColorSkillDamageUtility.ResolvedSkillInfo info))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            AbilityDef ab = info.abilityDef;
            sb.AppendLine("LC_Stat_SkillNameLine".Translate(ab.LabelCap));

            string typeText = info.secondaryType != FourColorDamageType.None && info.secondaryType != info.primaryType
                ? $"{FourColorDisplayHelper.FormatDamageType(info.primaryType)} / {FourColorDisplayHelper.FormatDamageType(info.secondaryType)}"
                : FourColorDisplayHelper.FormatDamageType(info.primaryType);
            sb.AppendLine("LC_Stat_WeaponDamageType".Translate(typeText));

            float cdSecs = ab.cooldownTicksRange.max / 60f;
            float range = ab.verbProperties != null ? ab.verbProperties.range : -1f;
            if (cdSecs > 0f)
            {
                string rangeText = range > 0f ? range.ToString("0.#") : "LC_Stat_SkillRangeMelee".Translate().ToString();
                sb.AppendLine("LC_Stat_SkillCooldownRangeLine".Translate(cdSecs.ToString("0.#"), rangeText));
            }

            sb.AppendLine();
            for (int i = 0; i < info.stages.Count; i++)
            {
                FourColorSkillDamageUtility.ResolvedSkillStage st = info.stages[i];
                string dmgStr = FourColorDisplayHelper.FormatDamageRange(st.min, st.max, st.isPercent);
                string hitStr = st.hitCount > 1 ? $"{dmgStr} × {st.hitCount}" : dmgStr;
                sb.AppendLine("LC_Stat_SkillStageLine".Translate(st.label, hitStr));
            }

            if (info.isPercent)
            {
                sb.AppendLine("LC_Stat_PalePercentNote".Translate());
            }

            if (!Mathf.Approximately(info.weaponEffMultiplier, 1f))
            {
                sb.AppendLine("LC_Stat_SkillMultiplierLine".Translate(info.weaponEffMultiplier.ToString("0.##")));
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            if (FourColorSkillDamageUtility.TryResolveSkillInfo(req, out FourColorSkillDamageUtility.ResolvedSkillInfo info))
            {
                return "LC_Stat_FinalSkillDamage".Translate(
                    FourColorDisplayHelper.FormatDamageRange(info.totalMin, info.totalMax, info.isPercent));
            }

            return "LC_Stat_FinalSkillDamage".Translate(FourColorDisplayHelper.FormatDamageNumber(finalVal));
        }
    }

    /// <summary>E.G.O / 四色武器输出倍率专用信息面板属性。</summary>
    public class StatWorker_FourColorDamageMultiplier : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            return !req.Empty
                && req.Def is ThingDef td
                && td.IsWeapon
                && FourColorOverrides.TryGetWeaponProfile(td, out _);
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (req.Empty || !(req.Def is ThingDef td) || !FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                return 1f;
            }

            float qualityFactor = req.HasThing
                ? FourColorOverrides.GetWeaponQualityFactor(req.Thing, wp.isAuthoredEgo)
                : 1f;
            float blessingMult = req.HasThing
                ? (GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(req.Thing) ?? 1f)
                : 1f;
            return wp.damageMultiplier * qualityFactor * blessingMult;
        }

        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
        {
            return $"x{value:0.##}";
        }

        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            return $"x{val:0.##}";
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (req.Empty || !(req.Def is ThingDef td) || !FourColorOverrides.TryGetWeaponProfile(td, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("LC_Stat_WeaponBaseMultiplier".Translate(wp.damageMultiplier.ToString("0.##")));
            if (req.HasThing)
            {
                float qualityFactor = FourColorOverrides.GetWeaponQualityFactor(req.Thing, wp.isAuthoredEgo);
                sb.AppendLine("LC_Stat_QualityFactor".Translate(qualityFactor.ToString("0.##")));
                float blessingMult = GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(req.Thing) ?? 1f;
                if (blessingMult > 1.0001f)
                {
                    string bonusPct = $"+{(blessingMult - 1f) * 100f:0.#}%";
                    sb.AppendLine("LC_Stat_WeaponBlessingLine".Translate(bonusPct));
                }
            }
            sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(wp.source)));
            return sb.ToString().TrimEndNewlines();
        }

        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            return "LC_Stat_FinalDamageMultiplier".Translate(finalVal.ToString("0.##"));
        }
    }

    /// <summary>四色单通道抗性通用 StatWorker 基类（适用于 E.G.O 护甲、四色护甲及角色/异想体）。</summary>
    public abstract class StatWorker_FourColorResistanceBase : StatWorker
    {
        protected abstract FourColorDamageType DamageType { get; }

        public override bool ShouldShowFor(StatRequest req)
        {
            if (req.Empty || !(req.Def is ThingDef td))
            {
                return false;
            }

            if (td.IsApparel && FourColorOverrides.TryGetApparelProfile(td, out _))
            {
                return true;
            }

            return req.Thing is Pawn || td.race != null;
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (req.Empty || !(req.Def is ThingDef td))
            {
                return 1f;
            }

            if (td.IsApparel && FourColorOverrides.TryGetApparelProfile(td, out FourColorOverrides.ResolvedApparelProfile ap))
            {
                float baseRes = Mathf.Max(0f, ap.ResistanceFor(DamageType));
                return req.HasThing
                    ? Mathf.Max(0f, FourColorOverrides.ApplyApparelQuality(baseRes, req.Thing, ap.isAuthoredEgo))
                    : baseRes;
            }

            if (req.Thing is Pawn pawn)
            {
                return FourColorDisplayHelper.ComputeEffectivePawnResistance(
                    pawn, DamageType, out _, out _, out _, out _, out _);
            }

            if (td.race != null)
            {
                return Mathf.Max(0f, FourColorOverrides.GetRaceResistance(td, DamageType));
            }

            return 1f;
        }

        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
        {
            return FourColorDisplayHelper.FormatResistanceValue(value, DamageType);
        }

        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            return FourColorDisplayHelper.FormatResistanceValue(val, DamageType);
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (req.Empty || !(req.Def is ThingDef td))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            if (td.IsApparel && FourColorOverrides.TryGetApparelProfile(td, out FourColorOverrides.ResolvedApparelProfile ap))
            {
                float baseRes = Mathf.Max(0f, ap.ResistanceFor(DamageType));
                sb.AppendLine("LC_Stat_ApparelBaseResist".Translate(baseRes.ToString("0.##")));
                if (req.HasThing)
                {
                    float qualityFactor = FourColorOverrides.GetApparelQualityFactor(req.Thing, ap.isAuthoredEgo);
                    sb.AppendLine("LC_Stat_QualityFactor".Translate(qualityFactor.ToString("0.##")));
                }
                sb.AppendLine("LC_Stat_RiskLevel".Translate(ap.riskLevel.ToString()));
                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(ap.source)));
            }
            else if (req.Thing is Pawn pawn)
            {
                FourColorOverrides.ResolvedRaceProfile raceProfile = FourColorOverrides.GetRaceProfile(td);
                FourColorDisplayHelper.ComputeEffectivePawnResistance(
                    pawn, DamageType,
                    out float raceOrDynResist,
                    out bool isDynamic,
                    out float apparelResist,
                    out string apparelLabel,
                    out LCRiskLevel armorLevel);

                bool hasEntityCat = FourColorDisplayHelper.TryResolveEntityCategoryRiskLevel(td, out LCRiskLevel entityLevel);
                bool hasWornArmor = FourColorDisplayHelper.HasWornFourColorArmor(pawn);

                sb.AppendLine("LC_Stat_PawnTargetKind".Translate(FourColorDisplayHelper.FormatTargetKind(raceProfile.targetKind)));
                if (hasEntityCat)
                {
                    sb.AppendLine("LC_Stat_PawnEntityRiskLevel".Translate(entityLevel.ToString()));
                }
                sb.AppendLine(isDynamic
                    ? "LC_Stat_PawnDynamicRaceResist".Translate(raceOrDynResist.ToString("0.##"))
                    : "LC_Stat_PawnRaceBaseResist".Translate(raceOrDynResist.ToString("0.##")));
                if (!hasEntityCat || hasWornArmor)
                {
                    sb.AppendLine("LC_Stat_PawnArmorResist".Translate(apparelResist.ToString("0.##"), apparelLabel));
                    sb.AppendLine("LC_Stat_PawnArmorRiskLevel".Translate(armorLevel.ToString()));
                }

                if (DamageType == FourColorDamageType.White || DamageType == FourColorDamageType.Black)
                {
                    sb.AppendLine("LC_Stat_PawnMentalSusceptible".Translate(DamageUtils.CanTakeMentalDamage(pawn) ? "Yes".Translate() : "No".Translate()));
                }

                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(raceProfile.source)));
            }
            else if (td.race != null)
            {
                FourColorOverrides.ResolvedRaceProfile raceProfile = FourColorOverrides.GetRaceProfile(td);
                float raceRes = Mathf.Max(0f, raceProfile.ResistanceFor(DamageType));
                sb.AppendLine("LC_Stat_PawnTargetKind".Translate(FourColorDisplayHelper.FormatTargetKind(raceProfile.targetKind)));
                if (FourColorDisplayHelper.TryResolveEntityCategoryRiskLevel(td, out LCRiskLevel raceEntityLevel))
                {
                    sb.AppendLine("LC_Stat_PawnEntityRiskLevel".Translate(raceEntityLevel.ToString()));
                }
                sb.AppendLine("LC_Stat_PawnRaceBaseResist".Translate(raceRes.ToString("0.##")));
                sb.AppendLine("LC_Stat_ProfileSource".Translate(FourColorDisplayHelper.FormatSource(raceProfile.source)));
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            FourColorResistanceTier tier = DamageUtils.ClassifyResistanceTier(finalVal);
            return "LC_Stat_FinalResistance".Translate(finalVal.ToString("0.##"), FourColorDisplayHelper.FormatResistanceTier(tier));
        }
    }

    /// <summary>物理（红）抗性 StatWorker。</summary>
    public class StatWorker_FourColorResistRed : StatWorker_FourColorResistanceBase
    {
        protected override FourColorDamageType DamageType => FourColorDamageType.Red;
    }

    /// <summary>精神（白）抗性 StatWorker。</summary>
    public class StatWorker_FourColorResistWhite : StatWorker_FourColorResistanceBase
    {
        protected override FourColorDamageType DamageType => FourColorDamageType.White;
    }

    /// <summary>侵蚀（黑）抗性 StatWorker。</summary>
    public class StatWorker_FourColorResistBlack : StatWorker_FourColorResistanceBase
    {
        protected override FourColorDamageType DamageType => FourColorDamageType.Black;
    }

    /// <summary>灵魂（蓝）抗性 StatWorker。</summary>
    public class StatWorker_FourColorResistPale : StatWorker_FourColorResistanceBase
    {
        protected override FourColorDamageType DamageType => FourColorDamageType.Pale;
    }
}
