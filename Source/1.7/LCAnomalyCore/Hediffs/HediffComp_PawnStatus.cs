using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Hediffs
{
    /// <summary>表示 <c>HediffComp_PawnStatus</c> 类型。</summary>
    public class HediffComp_PawnStatus : Hediff
    {
        /// <summary>表示 <c>statusStages</c>。</summary>
        protected List<HediffStage> statusStages = new List<HediffStage>();

        private HediffStage curStage;

        /// <summary>表示 <c>PawnStatusComp</c>。</summary>
        protected CompPawnStatus PawnStatusComp
        {
            get
            {
                if (pawnStatusComp == null)
                    pawnStatusComp = pawn.GetComp<CompPawnStatus>();

                return pawnStatusComp;
            }
        }

        private CompPawnStatus pawnStatusComp;

        /// <inheritdoc />
        public override HediffStage CurStage
        {
            get
            {
                if (curStage == null)
                {
                    curStage = new HediffStage();
                    StatusUpdate();
                }

                return curStage;
            }
        }

        /// <inheritdoc />
        public override void Tick()
        {
            if (Find.TickManager.TicksGame % 3600 == 0)
            {
                LogUtil.Warning("PawnStatus Hediff update.");

                StatusUpdate();
            }
        }

        /// <summary>
        /// 立即刷新员工属性与部门核心增益。
        /// </summary>
        public void RefreshStatus()
        {
            if (curStage == null)
            {
                curStage = new HediffStage();
            }

            StatusUpdate();
        }

        private void StatusUpdate()
        {
            if (curStage == null)
                return;

            List<StatModifier> offsets = GetStatsOffsetList();
            List<StatModifier> factors = GetStatsFactorList();
            Buildings.EDepartmentType? dept = DepartmentCoreUtility.GetPawnDepartmentType(pawn);
            if (dept.HasValue)
            {
                DepartmentCoreUtility.ApplyDepartmentStatModifiers(dept.Value, offsets, factors);
            }

            curStage.statOffsets = offsets;
            curStage.statFactors = factors;
        }

        private List<StatModifier> GetStatsOffsetList()
        {
            if (PawnStatusComp == null)
            {
                return new List<StatModifier>();
            }

            StatModifier smTemperance = new StatModifier()
            {
                stat = StatDefOf.WorkSpeedGlobal,
                value = 0.01f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Temperance).Status
            };

            StatModifier smJustice_Move = new StatModifier()
            {
                stat = StatDefOf.MoveSpeed,
                value = 0.01f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Status
            };

            return new List<StatModifier> { smTemperance, smJustice_Move };
        }

        private List<StatModifier> GetStatsFactorList()
        {
            if (PawnStatusComp == null)
            {
                return new List<StatModifier>();
            }

            StatModifier smFortitude = new StatModifier()
            {
                stat = StatDefOf.IncomingDamageFactor,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Fortitude).Status
            };

            StatModifier smPrudence = new StatModifier()
            {
                stat = StatDefOf.MentalBreakThreshold,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Prudence).Status
            };

            StatModifier smJustice_Melee = new StatModifier()
            {
                stat = StatDefOf.MeleeCooldownFactor,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Status
            };

            StatModifier smJustice_Ranged = new StatModifier()
            {
                stat = StatDefOf.RangedCooldownFactor,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Status
            };

            return new List<StatModifier> { smFortitude, smPrudence, smJustice_Melee, smJustice_Ranged };
        }
    }
}