using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 部门核心差异化增益与设施级被动效果工具类。
    /// </summary>
    public static class DepartmentCoreUtility
    {
        /// <summary>
        /// 获取指定员工当前已分配且生效的部门类型。
        /// </summary>
        public static EDepartmentType? GetPawnDepartmentType(Pawn pawn)
        {
            CompPawnStatus status = pawn?.GetComp<CompPawnStatus>();
            if (status == null || !status.Enabled)
            {
                return null;
            }

            Building_DepartmentCore core = status.AssignedCore;
            if (core == null || core.Destroyed)
            {
                return null;
            }

            EDepartmentType dept = core.CompDepartmentCore?.Props?.departmentType ?? EDepartmentType.Null;
            return dept == EDepartmentType.Null ? null : dept;
        }

        /// <summary>
        /// 检查指定地图是否存在至少一座通电运行中的目标部门核心。
        /// </summary>
        public static bool HasActiveDepartmentCore(Map map, EDepartmentType departmentType)
        {
            if (map?.listerBuildings == null || departmentType == EDepartmentType.Null)
            {
                return false;
            }

            foreach (Building_DepartmentCore core in map.listerBuildings.AllBuildingsColonistOfClass<Building_DepartmentCore>())
            {
                if (core == null || core.Destroyed || !core.Spawned)
                {
                    continue;
                }

                if (core.CompDepartmentCore?.Props?.departmentType != departmentType)
                {
                    continue;
                }

                if (core.CompPower == null || core.CompPower.PowerOn)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 惩戒部核心通电（+1）与惩戒部核心抑制永久奖励（+1）叠加，提升全设施 E.G.O 可提取上限。
        /// </summary>
        public static int GetEgoExtractionCapBonus(Map map)
        {
            int bonus = HasActiveDepartmentCore(map, EDepartmentType.DisciplinaryTeam) ? 1 : 0;
            if (GameComponent.GameComponent_LC.Instance?.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Gebura) == true)
            {
                bonus += 1;
            }
            return bonus;
        }

        /// <summary>
        /// 研发部核心通电（×0.75）与研发部核心抑制永久奖励（×0.80）叠加，降低全设施 E.G.O 提取消耗。
        /// </summary>
        public static float GetEgoExtractionCostFactor(Map map)
        {
            float factor = HasActiveDepartmentCore(map, EDepartmentType.ExtractionTeam) ? 0.75f : 1f;
            if (GameComponent.GameComponent_LC.Instance?.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Binah) == true)
            {
                factor *= 0.80f;
            }
            return factor;
        }

        /// <summary>
        /// 计算应用研发部折扣后的 E.G.O 提取 PE-Box 消耗（最低为 1）。
        /// </summary>
        public static int ApplyEgoExtractionCost(int baseCost, Map map)
        {
            if (baseCost <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.FloorToInt(baseCost * GetEgoExtractionCostFactor(map)));
        }

        /// <summary>
        /// 获取员工在异想体工作中的四维属性经验倍率（培训部 +50%，记录部 +20%，培训部核心抑制永久额外 +50%）。
        /// </summary>
        public static float GetExpGainMultiplier(Pawn pawn)
        {
            float mult = 1f;
            switch (GetPawnDepartmentType(pawn))
            {
                case EDepartmentType.TrainingTeam:
                    mult = 1.5f;
                    break;
                case EDepartmentType.RecordTeam:
                    mult = 1.2f;
                    break;
            }

            if (GameComponent.GameComponent_LC.Instance?.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Hod) == true)
            {
                mult += 0.5f;
            }
            return mult;
        }

        /// <summary>
        /// 获取员工在异想体工作中的成功率加成（情报部 +8%）。
        /// </summary>
        public static float GetStudySuccessRateBonus(Pawn pawn)
        {
            return GetPawnDepartmentType(pawn) == EDepartmentType.InformationTeam ? 0.08f : 0f;
        }

        /// <summary>
        /// 获取员工在完成异想体工作时额外产出 1 点 PE-Box 的概率（福利部 15%，情报部核心抑制永久 +25%）。
        /// </summary>
        public static float GetBonusPeBoxChance(Pawn pawn)
        {
            float chance = GetPawnDepartmentType(pawn) == EDepartmentType.WelfareTeam ? 0.15f : 0f;
            if (GameComponent.GameComponent_LC.Instance?.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Yesod) == true)
            {
                chance += 0.25f;
            }
            return Mathf.Clamp01(chance);
        }

        /// <summary>获取应用安保部核心抑制永久奖励后的实际房间再生燃料消耗。</summary>
        public static float GetEffectiveHealFuelCost(float baseCost)
        {
            if (GameComponent.GameComponent_LC.Instance?.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Netzach) == true)
            {
                return baseCost * 0.7f;
            }
            return baseCost;
        }

        /// <summary>获取应用安保部核心抑制永久奖励后的实际房间精神恢复量。</summary>
        public static float GetEffectiveHealMentalAmount(float baseMentalAmount)
        {
            if (GameComponent.GameComponent_LC.Instance?.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Netzach) == true)
            {
                return Mathf.Max(baseMentalAmount, 0.15f);
            }
            return baseMentalAmount;
        }

        /// <summary>
        /// 将部门专属属性偏移与系数（含控制部/情报部核心抑制永久强化）合并写入员工状态 HediffStage 列表。
        /// </summary>
        public static void ApplyDepartmentStatModifiers(
            EDepartmentType departmentType,
            List<StatModifier> offsets,
            List<StatModifier> factors)
        {
            switch (departmentType)
            {
                case EDepartmentType.ControlTeam:
                    MergeFactor(factors, StatDefOf.MoveSpeed, 1.15f);
                    MergeFactor(factors, StatDefOf.WorkSpeedGlobal, 1.10f);
                    break;

                case EDepartmentType.InformationTeam:
                    MergeOffset(offsets, Defs.StatDefOf.LC_AbnormalityStudyRate, 0.25f);
                    break;

                case EDepartmentType.TrainingTeam:
                    MergeFactor(factors, StatDefOf.GlobalLearningFactor, 1.15f);
                    break;

                case EDepartmentType.SafetyTeam:
                    MergeFactor(factors, StatDefOf.InjuryHealingFactor, 1.10f);
                    break;

                case EDepartmentType.CentralCommandTeam:
                    MergeFactor(factors, StatDefOf.MoveSpeed, 1.08f);
                    MergeFactor(factors, StatDefOf.WorkSpeedGlobal, 1.10f);
                    MergeFactor(factors, StatDefOf.IncomingDamageFactor, 0.92f);
                    break;

                case EDepartmentType.WelfareTeam:
                    MergeFactor(factors, StatDefOf.IncomingDamageFactor, 0.90f);
                    MergeOffset(offsets, StatDefOf.MentalBreakThreshold, -0.12f);
                    break;

                case EDepartmentType.DisciplinaryTeam:
                    MergeFactor(factors, StatDefOf.MeleeDamageFactor, 1.15f);
                    MergeFactor(factors, StatDefOf.AimingDelayFactor, 0.88f);
                    break;

                case EDepartmentType.RecordTeam:
                    MergeFactor(factors, StatDefOf.GlobalLearningFactor, 1.12f);
                    MergeOffset(offsets, StatDefOf.MentalBreakThreshold, -0.15f);
                    break;

                case EDepartmentType.ExtractionTeam:
                    MergeOffset(offsets, Defs.StatDefOf.LC_AbnormalityStudyRate, 0.15f);
                    break;

                case EDepartmentType.ArchitectureTeam:
                    MergeFactor(factors, StatDefOf.MoveSpeed, 1.10f);
                    MergeFactor(factors, StatDefOf.WorkSpeedGlobal, 1.10f);
                    MergeOffset(offsets, Defs.StatDefOf.LC_AbnormalityStudyRate, 0.10f);
                    MergeFactor(factors, StatDefOf.IncomingDamageFactor, 0.92f);
                    break;
            }

            GameComponent.GameComponent_LC lc = GameComponent.GameComponent_LC.Instance;
            if (lc != null)
            {
                if (lc.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Malkuth))
                {
                    MergeFactor(factors, StatDefOf.MoveSpeed, 1.10f);
                    MergeFactor(factors, StatDefOf.WorkSpeedGlobal, 1.10f);
                }

                if (lc.HasCoreSuppression(GameComponent.GameComponent_LC.CoreSuppression_Yesod))
                {
                    MergeOffset(offsets, Defs.StatDefOf.LC_AbnormalityStudyRate, 0.15f);
                }
            }
        }

        private static void MergeOffset(List<StatModifier> list, StatDef stat, float delta)
        {
            if (list == null || stat == null || Mathf.Approximately(delta, 0f))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].stat == stat)
                {
                    list[i].value += delta;
                    return;
                }
            }

            list.Add(new StatModifier { stat = stat, value = delta });
        }

        private static void MergeFactor(List<StatModifier> list, StatDef stat, float factor)
        {
            if (list == null || stat == null || Mathf.Approximately(factor, 1f))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].stat == stat)
                {
                    list[i].value *= factor;
                    return;
                }
            }

            list.Add(new StatModifier { stat = stat, value = factor });
        }
    }
}
