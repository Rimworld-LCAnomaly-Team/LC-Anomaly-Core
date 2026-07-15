using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Hediffs;
using LCAnomalyCore.ModExtensions;
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
            if (!TryGetWeaponProfile(sourceDef, out FourColorDamageType damageType, out _, out _)
                || damageType == FourColorDamageType.None)
            {
                return;
            }

            DamageDef convertedDef = DamageDefFor(damageType);
            if (convertedDef != null)
                dinfo.Def = convertedDef;
        }

        /// <summary>执行 <c>GetAdjustedDamage</c> 定义的操作。</summary>
        public static float GetAdjustedDamage(DamageInfo dinfo, Thing victim,
            FourColorDamageType damageType, LCRiskLevel fallbackRiskLevel)
        {
            ThingDef sourceDef = dinfo.Weapon ?? dinfo.Instigator?.def;
            LCRiskLevel attackLevel = fallbackRiskLevel;
            float weaponMultiplier = 1f;
            if (TryGetWeaponProfile(sourceDef, out _, out LCRiskLevel profileLevel, out float profileMultiplier))
            {
                attackLevel = profileLevel;
                weaponMultiplier = profileMultiplier;
            }

            float resistance = 1f;
            int defenseLevel = (int)LCRiskLevel.ZAYIN;
            if (victim is Pawn pawn && pawn.apparel != null)
            {
                float bestFactor = float.MaxValue;
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (!TryGetApparelProfile(apparel, damageType, out float apparelResistance,
                        out LCRiskLevel apparelLevel))
                    {
                        continue;
                    }

                    float candidate = Mathf.Max(0f, apparelResistance)
                        * GetDamageLevelFactor((int)attackLevel, (int)apparelLevel);
                    if (candidate < bestFactor)
                    {
                        bestFactor = candidate;
                        resistance = Mathf.Max(0f, apparelResistance);
                        defenseLevel = (int)apparelLevel;
                    }
                }
            }

            float levelFactor = GetDamageLevelFactor((int)attackLevel, defenseLevel);
            return Mathf.Max(0f, dinfo.Amount * Mathf.Max(0f, weaponMultiplier) * resistance * levelFactor);
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

            FourColorTargetKind targetKind = pawn.def
                .GetModExtension<FourColorTargetExtension>()?.targetKind ?? FourColorTargetKind.Default;
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

            pawn.mindState.mentalStateHandler.TryStartMentalState(state,
                "LC_FourColorPanicReason".Translate(), forced: true, forceWake: true,
                otherPawn: instigator, causedByDamage: true);
        }

        private static bool TryGetWeaponProfile(ThingDef def, out FourColorDamageType damageType,
            out LCRiskLevel riskLevel, out float multiplier)
        {
            damageType = FourColorDamageType.None;
            riskLevel = LCRiskLevel.ZAYIN;
            multiplier = 1f;
            if (def == null)
                return false;

            FourColorWeaponExtension extension = def.GetModExtension<FourColorWeaponExtension>();
            if (extension != null)
            {
                damageType = extension.damageType;
                riskLevel = extension.riskLevel;
                multiplier = extension.damageMultiplier;
                return true;
            }

            CompProperties_EgoWeapon comp = def.comps?.OfType<CompProperties_EgoWeapon>().FirstOrDefault();
            if (comp == null)
                return false;

            damageType = comp.damageType;
            riskLevel = (LCRiskLevel)LevelTag2Int(comp.level);
            multiplier = comp.damageMultiplier;
            return true;
        }

        private static bool TryGetApparelProfile(Apparel apparel, FourColorDamageType damageType,
            out float resistance, out LCRiskLevel riskLevel)
        {
            FourColorApparelExtension extension = apparel.def.GetModExtension<FourColorApparelExtension>();
            if (extension != null)
            {
                resistance = extension.ResistanceFor(damageType);
                riskLevel = extension.riskLevel;
                return true;
            }

            CompEgoSuit comp = apparel.TryGetComp<CompEgoSuit>();
            if (comp != null)
            {
                resistance = comp.ResistanceFor(damageType);
                riskLevel = (LCRiskLevel)LevelTag2Int(comp.Props.level);
                return true;
            }

            resistance = 1f;
            riskLevel = LCRiskLevel.ZAYIN;
            return false;
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
