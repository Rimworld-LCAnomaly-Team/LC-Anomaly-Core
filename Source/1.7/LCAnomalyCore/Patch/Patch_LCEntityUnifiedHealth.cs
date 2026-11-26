using HarmonyLib;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 为非人类脑叶实体（异想体与考验实体）替换为脑叶公司式统一血池机制：
    /// 1) 禁止截肢、器官破坏、失血与疼痛休克倒地；
    /// 2) 全部物理伤害汇聚至核心血池，避免分摊/低血上限部位截断导致伤害丢失；
    /// 3) 仅在累计伤害耗尽完整血池（LethalDamageThreshold = 150 * HealthScale）时死亡。
    /// </summary>
    public static class Patch_LCEntityUnifiedHealth
    {
        public struct FinalizeInjuryState
        {
            public bool active;
            public float prevTotalDealt;
            public float expectedDamage;
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart")]
    public static class Patch_DamageWorker_AddInjury_ApplyDamageToPart_LCUnifiedHealth
    {
        public static void Prefix(ref DamageInfo dinfo, Pawn pawn)
        {
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(pawn))
            {
                return;
            }

            BodyPartRecord corePart = pawn.RaceProps?.body?.corePart;
            if (corePart != null)
            {
                dinfo.SetHitPart(corePart);
            }

            dinfo.SetAllowDamagePropagation(false);
        }
    }

    [HarmonyPatch(
        typeof(DamageWorker_AddInjury),
        "FinalizeAndAddInjury",
        new[] { typeof(Pawn), typeof(Hediff_Injury), typeof(DamageInfo), typeof(Verse.DamageWorker.DamageResult) })]
    public static class Patch_DamageWorker_AddInjury_FinalizeAndAddInjury_LCUnifiedHealth
    {
        public static void Prefix(
            Pawn pawn,
            Hediff_Injury injury,
            Verse.DamageWorker.DamageResult result,
            out Patch_LCEntityUnifiedHealth.FinalizeInjuryState __state)
        {
            __state = default;
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(pawn) || injury == null)
            {
                return;
            }

            injury.destroysBodyParts = false;
            BodyPartRecord corePart = pawn.RaceProps?.body?.corePart;
            if (corePart != null)
            {
                injury.Part = corePart;
            }

            __state = new Patch_LCEntityUnifiedHealth.FinalizeInjuryState
            {
                active = true,
                prevTotalDealt = result?.totalDamageDealt ?? 0f,
                expectedDamage = Mathf.Max(0f, injury.Severity)
            };
        }

        public static void Postfix(
            Pawn pawn,
            Verse.DamageWorker.DamageResult result,
            ref float __result,
            Patch_LCEntityUnifiedHealth.FinalizeInjuryState __state)
        {
            if (!__state.active)
            {
                return;
            }

            if (result != null)
            {
                result.totalDamageDealt = __state.prevTotalDealt + __state.expectedDamage;
            }

            __result = __state.expectedDamage;
            pawn?.health?.summaryHealth?.Notify_HealthChanged();
        }
    }

    [HarmonyPatch(
        typeof(Pawn_HealthTracker),
        nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(Verse.DamageWorker.DamageResult) })]
    public static class Patch_Pawn_HealthTracker_AddHediff_LCUnifiedHealth
    {
        public static bool Prefix(Pawn ___pawn, Hediff hediff, ref BodyPartRecord part)
        {
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(___pawn) || hediff == null)
            {
                return true;
            }

            if (!___pawn.Dead && hediff is Hediff_MissingPart)
            {
                return false;
            }

            if (hediff.def == RimWorld.HediffDefOf.BloodLoss)
            {
                return false;
            }

            if (hediff is Hediff_Injury injury)
            {
                injury.destroysBodyParts = false;
                BodyPartRecord corePart = ___pawn.RaceProps?.body?.corePart;
                if (corePart != null)
                {
                    part = corePart;
                    injury.Part = corePart;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    public static class Patch_Pawn_HealthTracker_ShouldBeDead_LCUnifiedHealth
    {
        public static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(___pawn))
            {
                return true;
            }

            if (___pawn.Dead)
            {
                __result = true;
                return false;
            }

            if (___pawn.health?.hediffSet?.HasPreventsDeath == true)
            {
                __result = false;
                return false;
            }

            __result = LCEntityHealthUtility.IsHealthDepleted(___pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDowned))]
    public static class Patch_Pawn_HealthTracker_ShouldBeDowned_LCUnifiedHealth
    {
        public static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(___pawn))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(HediffSet), "CalculatePain")]
    public static class Patch_HediffSet_CalculatePain_LCUnifiedHealth
    {
        public static bool Prefix(Pawn ___pawn, ref float __result)
        {
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(___pawn))
            {
                return true;
            }

            __result = 0f;
            return false;
        }
    }

    [HarmonyPatch(typeof(HediffSet), "CalculateBleedRate")]
    public static class Patch_HediffSet_CalculateBleedRate_LCUnifiedHealth
    {
        public static bool Prefix(Pawn ___pawn, ref float __result)
        {
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(___pawn))
            {
                return true;
            }

            __result = 0f;
            return false;
        }
    }

    [HarmonyPatch(typeof(SummaryHealthHandler), nameof(SummaryHealthHandler.SummaryHealthPercent), MethodType.Getter)]
    public static class Patch_SummaryHealthHandler_SummaryHealthPercent_LCUnifiedHealth
    {
        public static bool Prefix(Pawn ___pawn, ref float __result)
        {
            if (!LCEntityHealthUtility.UsesUnifiedHealthPool(___pawn))
            {
                return true;
            }

            __result = LCEntityHealthUtility.GetHealthPercent(___pawn);
            return false;
        }
    }
}
