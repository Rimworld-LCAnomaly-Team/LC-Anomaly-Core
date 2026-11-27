using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Settings;
using LCAnomalyCore.Util;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 控制脑叶战斗命中与闪避逻辑：
    /// 1. 大型/重型异想体与考验实体压低近战闪避率（BodySize >= 1.2 完全不闪避）；
    /// 2. 带有 guaranteedHit 标记的武器（如魔弹）必中并无视掩体与精度惩罚；
    /// 3. 开启“脑叶必中模式”设置后，所有 E.G.O / 四色武器与异想体/考验攻击均为必中。
    /// </summary>
    internal static class FourColorHitRuleHelper
    {
        public static bool ShouldGuaranteeHit(Thing caster, Verb verb)
        {
            ThingDef weaponDef = verb?.EquipmentSource?.def;
            if (weaponDef != null && FourColorOverrides.TryGetWeaponProfile(weaponDef, out FourColorOverrides.ResolvedWeaponProfile wp))
            {
                if (wp.guaranteedHit)
                {
                    return true;
                }

                if (Setting_LCAnomalyCore_Main.Settings?.If_GuaranteedFourColorHit == true)
                {
                    return true;
                }
            }

            if (Setting_LCAnomalyCore_Main.Settings?.If_GuaranteedFourColorHit == true
                && caster is Pawn casterPawn
                && DamageUtils.TryGetEntityRiskLevel(casterPawn.def, out _))
            {
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    internal static class Patch_ShotReport_HitReportFor_FourColor
    {
        private static readonly FieldInfo CoversField = AccessTools.Field(typeof(ShotReport), "covers");
        private static readonly FieldInfo CoversBlockField = AccessTools.Field(typeof(ShotReport), "coversOverallBlockChance");
        private static readonly FieldInfo ShooterDistField = AccessTools.Field(typeof(ShotReport), "factorFromShooterAndDist");
        private static readonly FieldInfo EquipmentField = AccessTools.Field(typeof(ShotReport), "factorFromEquipment");
        private static readonly FieldInfo TargetSizeField = AccessTools.Field(typeof(ShotReport), "factorFromTargetSize");
        private static readonly FieldInfo WeatherField = AccessTools.Field(typeof(ShotReport), "factorFromWeather");
        private static readonly FieldInfo ForcedMissField = AccessTools.Field(typeof(ShotReport), "forcedMissRadius");
        private static readonly FieldInfo DarknessField = AccessTools.Field(typeof(ShotReport), "offsetFromDarkness");
        private static readonly FieldInfo GasField = AccessTools.Field(typeof(ShotReport), "factorFromCoveringGas");
        private static readonly FieldInfo DistanceField = AccessTools.Field(typeof(ShotReport), "distance");

        private static void Postfix(ref ShotReport __result, Thing caster, Verb verb, LocalTargetInfo target)
        {
            if (!FourColorHitRuleHelper.ShouldGuaranteeHit(caster, verb))
            {
                return;
            }

            object boxed = __result;
            CoversField?.SetValue(boxed, new List<CoverInfo>());
            CoversBlockField?.SetValue(boxed, 0f);
            ShooterDistField?.SetValue(boxed, 1f);
            EquipmentField?.SetValue(boxed, 1f);
            WeatherField?.SetValue(boxed, 1f);
            GasField?.SetValue(boxed, 1f);
            DarknessField?.SetValue(boxed, 0f);
            ForcedMissField?.SetValue(boxed, 0f);

            float dist = DistanceField != null ? (float)DistanceField.GetValue(boxed) : 0f;
            bool hasPronePenalty = target.HasThing
                && target.Thing is Pawn targetPawn
                && dist >= 4.5f
                && targetPawn.GetPosture() != PawnPosture.Standing;
            TargetSizeField?.SetValue(boxed, hasPronePenalty ? 2f : 1f);

            __result = (ShotReport)boxed;
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetNonMissChance")]
    internal static class Patch_VerbMeleeAttack_GetNonMissChance_FourColor
    {
        private static void Postfix(Verb_MeleeAttack __instance, ref float __result)
        {
            if (FourColorHitRuleHelper.ShouldGuaranteeHit(__instance?.caster, __instance))
            {
                __result = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetDodgeChance")]
    internal static class Patch_VerbMeleeAttack_GetDodgeChance_FourColor
    {
        private static void Postfix(Verb_MeleeAttack __instance, LocalTargetInfo target, ref float __result)
        {
            if (__result <= 0f)
            {
                return;
            }

            if (FourColorHitRuleHelper.ShouldGuaranteeHit(__instance?.caster, __instance))
            {
                __result = 0f;
                return;
            }

            if (target.HasThing && target.Thing is Pawn targetPawn && DamageUtils.TryGetEntityRiskLevel(targetPawn.def, out _))
            {
                float bodySize = targetPawn.BodySize;
                if (bodySize >= 1.2f)
                {
                    __result = 0f;
                }
                else if (bodySize >= 0.7f)
                {
                    __result *= 0.25f;
                }
                else
                {
                    __result *= 0.45f;
                }
            }
        }
    }
}
