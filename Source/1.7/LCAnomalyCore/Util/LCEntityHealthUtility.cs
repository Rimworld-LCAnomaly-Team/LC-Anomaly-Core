using LCAnomalyCore.Comp;
using LCAnomalyCore.ModExtensions;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 脑叶异常生物/异想体统一血池工具：禁用原版肢体/器官缺失致死、流血与痛觉倒地，仅在总血池（LethalDamageThreshold = 150 * HealthScale）归零时死亡。
    /// </summary>
    public static class LCEntityHealthUtility
    {
        /// <summary>判断目标 Pawn 是否受脑叶统一血池机制管控（非人类的 LC 实体、异想体或考验单位）。</summary>
        public static bool UsesUnifiedHealthPool(Pawn pawn)
        {
            if (pawn == null || pawn.RaceProps == null || pawn.RaceProps.Humanlike)
            {
                return false;
            }

            return pawn is LC_EntityBasePawn
                || pawn.TryGetComp<CompAbnormality>() != null
                || pawn.def?.GetModExtension<ModExtension_AbnormalityCategory>() != null;
        }

        /// <summary>获取实体统一总生命上限（对应 LethalDamageThreshold = 150 * HealthScale）。</summary>
        public static float GetMaxHealth(Pawn pawn)
        {
            return Mathf.Max(1f, pawn?.health?.LethalDamageThreshold ?? 150f);
        }

        /// <summary>统计当前实体身上的全部创伤总伤害量。</summary>
        public static float GetTotalInjuryDamage(Pawn pawn)
        {
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null || hediffs.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury && injury.Severity > 0f)
                {
                    total += injury.Severity;
                }
            }

            return total;
        }

        /// <summary>获取实体当前剩余生命值。</summary>
        public static float GetCurrentHealth(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return 0f;
            }

            return Mathf.Max(0f, GetMaxHealth(pawn) - GetTotalInjuryDamage(pawn));
        }

        /// <summary>获取实体当前统一血池百分比（0~1）。</summary>
        public static float GetHealthPercent(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetCurrentHealth(pawn) / GetMaxHealth(pawn));
        }

        /// <summary>判断实体的总血池是否已被完全打空。</summary>
        public static bool IsHealthDepleted(Pawn pawn)
        {
            return pawn != null && (pawn.Dead || GetTotalInjuryDamage(pawn) >= GetMaxHealth(pawn) - 0.001f);
        }
    }
}
