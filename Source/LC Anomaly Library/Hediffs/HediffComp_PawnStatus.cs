using LCAnomalyLibrary.Comp.Pawns;
using LCAnomalyLibrary.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyLibrary.Hediffs
{
    public class HediffComp_PawnStatus : Hediff
    {
        protected List<HediffStage> statusStages = new List<HediffStage>();

        private HediffStage curStage;

        protected CompPawnStatus PawnStatusComp
        {
            get
            {
                if(pawnStatusComp == null)
                    pawnStatusComp = pawn.GetComp<CompPawnStatus>();

                return pawnStatusComp;
            }
        }
        private CompPawnStatus pawnStatusComp;


        public override HediffStage CurStage
        {
            get
            {
                if (curStage == null)
                {
                    curStage = new HediffStage
                    {
                        statFactors = GetStatsFactorList(),
                        statOffsets = GetStatsOffsetList()
                    };
                }

                return curStage;
            }
        }

        public override void Tick()
        {
            if (Find.TickManager.TicksGame % 3600 == 0)
            {
                LogUtil.Warning("PawnStatus Hediff update.");

                StatusUpdate();
            }
        }

        private void StatusUpdate()
        {
            if (CurStage == null)
                return;

            CurStage.statFactors = GetStatsFactorList();
            CurStage.statOffsets = GetStatsOffsetList();
        }

        private List<StatModifier> GetStatsOffsetList()
        {
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

            return [smTemperance, smJustice_Move];
        }

        private List<StatModifier> GetStatsFactorList()
        {
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

            return [smFortitude, smPrudence, smJustice_Melee, smJustice_Ranged];
        }
    }
}
