using System;
using System.Collections.Generic;
using HarmonyLib;
using LCAnomalyCore.Interface;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Util;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>表示 <c>Patch_Thing_TakeDamage</c> 类型。</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage
    {
        /// <summary>执行 <c>Prefix</c> 定义的操作。</summary>
        public static void Prefix(ref DamageInfo dinfo)
        {
            DamageUtils.ConvertWeaponDamage(ref dinfo);

            // 非四色伤害由这里统一附加古老的信念赐福倍率，避免与 DamageUtils.GetAdjustedDamage 重复叠加
            if (dinfo.Def?.GetModExtension<FourColorDamageExtension>() == null)
            {
                float blessingMult = DamageUtils.GetWeaponBlessingMultiplier(dinfo);
                if (blessingMult > 1.0001f)
                {
                    dinfo.SetAmount(dinfo.Amount * blessingMult);
                }
            }
        }
    }

    /// <summary>
    /// 拦截即将生效的 Pawn 伤害，供异想体 Hediff（如异界肖像代偿）在零消耗且防递归的情况下转发或吸收伤害。
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_Pawn_PreApplyDamage
    {
        [ThreadStatic]
        private static int redirectDepth;

        /// <summary>当前线程是否正处于伤害重定向保护作用域内。</summary>
        public static bool IsInRedirect => redirectDepth > 0;

        /// <summary>在重定向递归守卫中执行回调，避免转嫁伤害被再次拦截造成死循环。</summary>
        public static void RunWithRedirectGuard(Action action)
        {
            if (action == null)
            {
                return;
            }

            redirectDepth++;
            try
            {
                action();
            }
            finally
            {
                redirectDepth--;
            }
        }

        /// <summary>执行 <c>Prefix</c> 定义的拦截逻辑。</summary>
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (redirectDepth > 0 || __instance?.health?.hediffSet?.hediffs == null)
            {
                return true;
            }

            List<Hediff> hediffs = __instance.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc) || hwc.comps == null)
                {
                    continue;
                }

                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is ILCPawnDamageInterceptor interceptor
                        && interceptor.TryInterceptDamage(__instance, ref dinfo, out bool interceptedAbsorb)
                        && interceptedAbsorb)
                    {
                        absorbed = true;
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
