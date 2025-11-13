using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Hediffs
{
    public class HediffComp_PawnStatus : Hediff
    {
        protected List<HediffStage> statusStages = new List<HediffStage>();

        private HediffStage curStage;

        protected CompPawnStatus PawnStatusComp => pawnStatusComp ??= pawn.GetComp<CompPawnStatus>();

        private CompPawnStatus pawnStatusComp;

        // 优化: 缓存 StatModifier 列表以避免重复分配
        private List<StatModifier> cachedStatFactors;
        private List<StatModifier> cachedStatOffsets;

        public override HediffStage CurStage
        {
            get
            {
                if (curStage == null)
                {
                    // 初始化缓存的修饰符列表
                    cachedStatFactors = new List<StatModifier>(4);
                    cachedStatOffsets = new List<StatModifier>(2);

                    curStage = new HediffStage
                    {
                        statFactors = cachedStatFactors,
                        statOffsets = cachedStatOffsets
                    };
                }

                return curStage;
            }
        }

        public override void Tick()
        {
            if (Find.TickManager.TicksGame % 3600 == 0)
            {
                StatusUpdate();
            }
        }

        private void StatusUpdate()
        {
            if (CurStage == null || PawnStatusComp == null)
                return;

            // 优化: 直接更新缓存列表中的值,而不是创建新列表
            UpdateStatsOffsetList();
            UpdateStatsFactorList();
        }

        private void UpdateStatsOffsetList()
        {
            if (cachedStatOffsets == null)
            {
                cachedStatOffsets = new List<StatModifier>(2);
            }

            cachedStatOffsets.Clear();

            cachedStatOffsets.Add(new StatModifier
            {
                stat = StatDefOf.WorkSpeedGlobal,
                value = 0.01f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Temperance).Status
            });

            cachedStatOffsets.Add(new StatModifier
            {
                stat = StatDefOf.MoveSpeed,
                value = 0.01f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Status
            });
        }

        private void UpdateStatsFactorList()
        {
            if (cachedStatFactors == null)
            {
                cachedStatFactors = new List<StatModifier>(4);
            }

            cachedStatFactors.Clear();

            cachedStatFactors.Add(new StatModifier
            {
                stat = StatDefOf.IncomingDamageFactor,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Fortitude).Status
            });

            cachedStatFactors.Add(new StatModifier
            {
                stat = StatDefOf.MentalBreakThreshold,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Prudence).Status
            });

            cachedStatFactors.Add(new StatModifier
            {
                stat = StatDefOf.MeleeCooldownFactor,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Status
            });

            cachedStatFactors.Add(new StatModifier
            {
                stat = StatDefOf.RangedCooldownFactor,
                value = 1 - 0.005f * PawnStatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Status
            });
        }
    }
}