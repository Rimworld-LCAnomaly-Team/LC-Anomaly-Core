using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Hediffs;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Misc;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>表示 <c>DamageUtils</c> 类型。</summary>
    public static class DamageUtils
    {
        /// <summary>执行 <c>LevelTag2Int</c> 定义的操作。</summary>
        public static int LevelTag2Int(string tag)
        {
            if (Enum.TryParse(tag, true, out LCRiskLevel level))
                return (int)level;

            Log.ErrorOnce($"LC Anomaly Core: unknown E.G.O risk level '{tag}', using ZAYIN.",
                tag?.GetHashCode() ?? 0);
            return (int)LCRiskLevel.ZAYIN;
        }

        /// <summary>执行 <c>GetDamageLevelFactor</c> 定义的操作。</summary>
        public static float GetDamageLevelFactor(int attacker, int victim)
        {
            int delta = Mathf.Clamp(victim - attacker, -4, 4);
            switch (delta)
            {
                case 4: return 0.4f;
                case 3: return 0.6f;
                case 2: return 0.7f;
                case 1: return 0.8f;
                case -2: return 1.2f;
                case -3: return 1.5f;
                case -4: return 2f;
                default: return 1f;
            }
        }

        /// <summary>执行 <c>ConvertWeaponDamage</c> 定义的操作。</summary>
        public static void ConvertWeaponDamage(ref DamageInfo dinfo)
        {
            if (dinfo.Def?.GetModExtension<FourColorDamageExtension>() != null)
                return;

            ThingDef sourceDef = dinfo.Weapon ?? dinfo.Instigator?.def;
            if (!FourColorOverrides.TryGetWeaponProfile(sourceDef, out FourColorOverrides.ResolvedWeaponProfile weaponProfile)
                || weaponProfile.damageType == FourColorDamageType.None)
            {
                return;
            }

            DamageDef convertedDef = DamageDefFor(weaponProfile.damageType);
            if (convertedDef != null)
                dinfo.Def = convertedDef;
        }

        /// <summary>执行 <c>GetAdjustedDamage</c> 定义的操作。</summary>
        public static float GetAdjustedDamage(DamageInfo dinfo, Thing victim,
            FourColorDamageType damageType, LCRiskLevel fallbackRiskLevel)
        {
            return ComputeAdjustedDamage(dinfo, victim, damageType, fallbackRiskLevel, out _);
        }

        /// <summary>尝试从 Pawn 的 ThingDef（或其抽象父级）解析脑叶危险等级扩展。</summary>
        public static bool TryGetEntityRiskLevel(ThingDef def, out LCRiskLevel level)
        {
            ModExtension_AbnormalityCategory ext = def?.GetModExtension<ModExtension_AbnormalityCategory>();
            if (ext?.abnormalityCategoryDef != null
                && !ext.abnormalityCategoryDef.defName.NullOrEmpty()
                && Enum.TryParse(ext.abnormalityCategoryDef.defName, true, out level))
            {
                return true;
            }

            level = LCRiskLevel.ZAYIN;
            return false;
        }

        /// <summary>计算四色伤害经等级压制与抗性倍率修正后的最终伤害，并返回目标的有效抗性倍率。</summary>
        public static float ComputeAdjustedDamage(DamageInfo dinfo, Thing victim,
            FourColorDamageType damageType, LCRiskLevel fallbackRiskLevel, out float effectiveResistance)
        {
            ThingDef sourceDef = dinfo.Weapon ?? dinfo.Instigator?.def;
            LCRiskLevel attackLevel = fallbackRiskLevel;
            float weaponMultiplier = 1f;
            bool isAuthoredWeapon = false;
            if (FourColorOverrides.TryGetWeaponProfile(sourceDef, out FourColorOverrides.ResolvedWeaponProfile weaponProfile))
            {
                attackLevel = weaponProfile.riskLevel;
                weaponMultiplier = weaponProfile.damageMultiplier;
                isAuthoredWeapon = weaponProfile.isAuthoredEgo;
            }
            else if (dinfo.Weapon == null
                && dinfo.Instigator is Pawn attackerPawn
                && TryGetEntityRiskLevel(attackerPawn.def, out LCRiskLevel attackerEntityLevel)
                && attackerEntityLevel > attackLevel)
            {
                attackLevel = attackerEntityLevel;
            }

            float weaponQualityFactor = 1f;
            float weaponBlessingFactor = 1f;
            if (dinfo.Weapon != null && dinfo.Instigator is Pawn attacker)
            {
                Thing weaponThing = FindWeaponThing(attacker, dinfo.Weapon);
                if (weaponThing != null)
                {
                    weaponQualityFactor = FourColorOverrides.GetWeaponQualityFactor(weaponThing, isAuthoredWeapon);
                    weaponBlessingFactor = GameComponent.GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(weaponThing) ?? 1f;
                }
            }

            int entityDefenseLevel = (victim is Pawn victimEntity && TryGetEntityRiskLevel(victimEntity.def, out LCRiskLevel entityDefLevel))
                ? (int)entityDefLevel
                : (int)LCRiskLevel.ZAYIN;
            float resistance = 1f;
            int defenseLevel = entityDefenseLevel;
            if (victim is Pawn pawn && pawn.apparel != null)
            {
                float bestFactor = float.MaxValue;
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (apparel == null || !FourColorOverrides.TryGetApparelProfile(apparel.def, out FourColorOverrides.ResolvedApparelProfile apparelProfile))
                    {
                        continue;
                    }

                    float effResistance = FourColorOverrides.ApplyApparelQuality(
                        apparelProfile.ResistanceFor(damageType),
                        apparel,
                        apparelProfile.isAuthoredEgo);
                    int candidateDefenseLevel = Mathf.Max(entityDefenseLevel, (int)apparelProfile.riskLevel);
                    float candidate = Mathf.Max(0f, effResistance)
                        * GetDamageLevelFactor((int)attackLevel, candidateDefenseLevel);
                    if (candidate < bestFactor)
                    {
                        bestFactor = candidate;
                        resistance = Mathf.Max(0f, effResistance);
                        defenseLevel = candidateDefenseLevel;
                    }
                }
            }

            float raceResistance;
            if (victim is ILCFourColorDynamicResistance dynamicRes
                && dynamicRes.TryGetDynamicFourColorResistance(damageType, out float dynVal))
            {
                raceResistance = Mathf.Max(0f, dynVal);
            }
            else
            {
                raceResistance = victim is Pawn victimPawn
                    ? FourColorOverrides.GetRaceResistance(victimPawn.def, damageType)
                    : 1f;
            }

            float globalScale = Mathf.Clamp(Setting_LCAnomalyCore_Main.Settings?.FourColorGlobalDamageScale ?? 1f, 0.25f, 3f);
            float levelFactor = GetDamageLevelFactor((int)attackLevel, defenseLevel);
            effectiveResistance = Mathf.Max(0f, resistance * raceResistance);
            return Mathf.Max(0f, dinfo.Amount * Mathf.Max(0f, weaponMultiplier) * weaponQualityFactor * weaponBlessingFactor * effectiveResistance * levelFactor * globalScale);
        }

        /// <summary>将有效抗性倍率归类为脑叶公司标准抗性等级标签。</summary>
        public static FourColorResistanceTier ClassifyResistanceTier(float effectiveResistance)
        {
            if (effectiveResistance <= 0.001f)
                return FourColorResistanceTier.Immune;
            if (effectiveResistance < 0.50f)
                return FourColorResistanceTier.Resist;
            if (effectiveResistance < 0.99f)
                return FourColorResistanceTier.Endure;
            if (effectiveResistance <= 1.01f)
                return FourColorResistanceTier.Normal;
            if (effectiveResistance < 1.99f)
                return FourColorResistanceTier.Weak;
            return FourColorResistanceTier.Fatal;
        }

        /// <summary>获取造成该伤害的武器实例附带的古老的信念赐福倍率（无赐福时返回 1）。</summary>
        public static float GetWeaponBlessingMultiplier(DamageInfo dinfo)
        {
            if (dinfo.Weapon != null && dinfo.Instigator is Pawn attacker)
            {
                Thing weaponThing = FindWeaponThing(attacker, dinfo.Weapon);
                if (weaponThing != null)
                {
                    return GameComponent.GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(weaponThing) ?? 1f;
                }
            }

            return 1f;
        }

        private static Thing FindWeaponThing(Pawn attacker, ThingDef weaponDef)
        {
            if (attacker?.equipment == null || weaponDef == null)
                return null;

            Thing primary = attacker.equipment.Primary;
            if (primary != null && primary.def == weaponDef)
                return primary;

            List<ThingWithComps> all = attacker.equipment.AllEquipmentListForReading;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].def == weaponDef)
                        return all[i];
                }
            }

            return null;
        }

        /// <summary>在受击位置生成四色伤害图标指示、抗性等级与伤害数字。</summary>
        public static void SpawnFourColorHitIndicator(FourColorDamageType damageType, Vector3 position, Map map, float adjustedAmount)
        {
            SpawnFourColorHitIndicator(damageType, position, map, adjustedAmount, 1f);
        }

        /// <summary>在受击位置生成统一风格的 `[四色图标] [抗性等级] [伤害数值]` 飘字指示。</summary>
        public static void SpawnFourColorHitIndicator(FourColorDamageType damageType, Vector3 position, Map map, float adjustedAmount, float effectiveResistance)
        {
            FourColorResistanceTier tier = ClassifyResistanceTier(effectiveResistance);
            if (map == null || (adjustedAmount <= 0f && tier != FourColorResistanceTier.Immune))
                return;

            IntVec3 cell = position.ToIntVec3();
            if (!cell.InBounds(map) || !position.ShouldSpawnMotesAt(map))
                return;

            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            bool showIcons = settings?.If_ShowFourColorHitIcons ?? true;
            bool showNumbers = settings?.If_ShowFourColorDamageNumbers ?? true;
            if (!showIcons && !showNumbers)
                return;

            ThingDef moteDef = Defs.ThingDefOf.LC_Mote_FourColorDamage;
            if (moteDef != null && ThingMaker.MakeThing(moteDef) is Mote_FourColorDamage banner)
            {
                Vector3 spawnPos = position + new Vector3(Rand.Range(-0.22f, 0.22f), 0f, Rand.Range(0.22f, 0.45f));
                IntVec3 spawnCell = spawnPos.ToIntVec3();
                if (!spawnCell.InBounds(map))
                {
                    spawnPos = position;
                    spawnCell = cell;
                }

                banner.exactPosition = spawnPos;
                banner.SetVelocity(Rand.Range(-10f, 10f), Rand.Range(0.38f, 0.48f));
                banner.damageType = damageType;
                banner.resistanceTier = tier;
                banner.damageAmount = tier == FourColorResistanceTier.Immune ? 0f : adjustedAmount;
                banner.showIcon = showIcons;
                banner.showNumber = showNumbers;
                GenSpawn.Spawn(banner, spawnCell, map);
                return;
            }

            if (showNumbers)
            {
                string numText = tier == FourColorResistanceTier.Immune
                    ? "0"
                    : (adjustedAmount >= 10f ? Mathf.RoundToInt(adjustedAmount).ToString() : adjustedAmount.ToString("0.#"));
                string fallback = tier == FourColorResistanceTier.Normal
                    ? numText
                    : $"{tier.ToString().ToUpperInvariant()} {numText}";
                MoteMaker.ThrowText(position, map, fallback, ColorForDamageType(damageType), 1.3f);
            }
        }

        private static Color ColorForDamageType(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return new Color(0.95f, 0.28f, 0.28f);
                case FourColorDamageType.White: return Color.white;
                case FourColorDamageType.Black: return new Color(0.75f, 0.38f, 0.98f);
                case FourColorDamageType.Pale: return new Color(0.45f, 0.92f, 1.0f);
                default: return Color.white;
            }
        }

        private static FleckDef HitFleckDefFor(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return Defs.FleckDefOf.LC_FourColorHit_Red;
                case FourColorDamageType.White: return Defs.FleckDefOf.LC_FourColorHit_White;
                case FourColorDamageType.Black: return Defs.FleckDefOf.LC_FourColorHit_Black;
                case FourColorDamageType.Pale: return Defs.FleckDefOf.LC_FourColorHit_Pale;
                default: return null;
            }
        }

        /// <summary>执行 <c>ApplyMentalDamage</c> 定义的操作。</summary>
        public static float ApplyMentalDamage(Pawn pawn, float amount, DamageInfo dinfo)
        {
            if (!CanTakeMentalDamage(pawn) || amount <= 0f)
                return 0f;

            Hediff_LCMentalDamage mentalDamage = pawn.health.hediffSet
                .GetFirstHediffOfDef(Defs.HediffDefOf.LC_MentalDamage) as Hediff_LCMentalDamage;

            bool suppressingPanic = pawn.InMentalState;
            if (mentalDamage == null)
            {
                if (suppressingPanic)
                    return 0f;
                mentalDamage = (Hediff_LCMentalDamage)HediffMaker.MakeHediff(
                    Defs.HediffDefOf.LC_MentalDamage, pawn);
                pawn.health.AddHediff(mentalDamage, null, dinfo);
            }

            float maxSP = GetMaxMentalPoints(pawn);
            float oldSeverity = mentalDamage.Severity;
            float severityDelta = amount / maxSP;

            if (suppressingPanic)
            {
                mentalDamage.Severity -= severityDelta;
                if (mentalDamage.Severity <= 0.0001f && pawn.MentalState != null)
                    pawn.MentalState.RecoverFromState();
            }
            else
            {
                mentalDamage.Severity = Mathf.Min(1f, mentalDamage.Severity + severityDelta);
                mentalDamage.NotifyMentalDamageTaken();
                if (oldSeverity < 1f && mentalDamage.Severity >= 1f)
                    StartPanic(pawn, dinfo.Instigator as Pawn);
            }

            return amount;
        }

        /// <summary>执行 <c>CanTakeMentalDamage</c> 定义的操作。</summary>
        public static bool CanTakeMentalDamage(Pawn pawn)
        {
            if (pawn?.mindState?.mentalStateHandler == null)
                return false;

            FourColorTargetKind targetKind = FourColorOverrides.GetRaceTargetKind(pawn.def);
            if (targetKind == FourColorTargetKind.Abnormality)
                return false;
            if (targetKind == FourColorTargetKind.Employee)
                return true;

            return pawn.RaceProps.Humanlike && !(pawn.def is Defs.ThingDef_AbnormalityBase);
        }

        /// <summary>执行 <c>GetMaxMentalPoints</c> 定义的操作。</summary>
        public static float GetMaxMentalPoints(Pawn pawn)
        {
            CompPawnStatus status = pawn.TryGetComp<CompPawnStatus>();
            if (status != null && status.Enabled)
                return Mathf.Max(1f, status.GetPawnStatusLevel(EPawnStatus.Prudence).Status);
            return 100f;
        }

        private static void StartPanic(Pawn pawn, Pawn instigator)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.mindState?.mentalStateHandler == null)
                return;

            MentalStateDef state = MentalStateDefOf.PanicFlee;
            CompPawnStatus status = pawn.TryGetComp<CompPawnStatus>();
            if (status != null && status.Enabled)
            {
                Dictionary<EPawnStatus, int> values = Enum.GetValues(typeof(EPawnStatus))
                    .Cast<EPawnStatus>()
                    .ToDictionary(x => x, x => status.GetPawnStatusLevel(x).Status);
                int maximum = values.Max(x => x.Value);
                EPawnStatus highest = values.Where(x => x.Value == maximum).Select(x => x.Key).RandomElement();
                switch (highest)
                {
                    case EPawnStatus.Fortitude: state = MentalStateDefOf.Berserk; break;
                    case EPawnStatus.Prudence: state = MentalStateDefOf.Wander_Sad; break;
                    case EPawnStatus.Temperance: state = MentalStateDefOf.Wander_Psychotic; break;
                    case EPawnStatus.Justice: state = MentalStateDefOf.PanicFlee; break;
                }
            }

            if (state == null || !state.Worker.StateCanOccur(pawn))
            {
                if (MentalStateDefOf.PanicFlee != null && MentalStateDefOf.PanicFlee.Worker.StateCanOccur(pawn))
                    state = MentalStateDefOf.PanicFlee;
                else
                    return;
            }

            if (pawn.Downed && (state.recoverFromDowned || !state.downedCanDo))
                return;

            pawn.mindState.mentalStateHandler.TryStartMentalState(state,
                "LC_FourColorPanicReason".Translate(), forced: true, forceWake: true,
                otherPawn: instigator, causedByDamage: true);
        }

        private static DamageDef DamageDefFor(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return Defs.DamageDefOf.LC_RedDamage;
                case FourColorDamageType.White: return Defs.DamageDefOf.LC_WhiteDamage;
                case FourColorDamageType.Black: return Defs.DamageDefOf.LC_BlackDamage;
                case FourColorDamageType.Pale: return Defs.DamageDefOf.LC_PaleDamage;
                default: return null;
            }
        }
    }
}
