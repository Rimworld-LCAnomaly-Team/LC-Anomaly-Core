using Verse;

namespace LCAnomalyCore.Hediffs
{
    /// <summary>
    /// Severity is the lost fraction of the pawn's maximum SP (0..1).
    /// </summary>
    public class Hediff_LCMentalDamage : Hediff
    {
        private const int RecoveryDelayTicks = 600;
        private const float RecoveryPerDay = 2f;

        private int lastDamageTick;

        /// <inheritdoc />
        public override string LabelInBrackets => UnityEngine.Mathf.Clamp01(1f - Severity).ToStringPercent();

        /// <inheritdoc />
        public override bool ShouldRemove => Severity <= 0.0001f;

        /// <summary>执行 <c>NotifyMentalDamageTaken</c> 定义的操作。</summary>
        public void NotifyMentalDamageTaken()
        {
            lastDamageTick = Find.TickManager.TicksGame;
        }

        /// <inheritdoc />
        public override void Tick()
        {
            base.Tick();
            if (pawn.InMentalState || !pawn.IsHashIntervalTick(60)
                || Find.TickManager.TicksGame - lastDamageTick < RecoveryDelayTicks)
            {
                return;
            }

            Severity -= RecoveryPerDay * 60f / 60000f;
        }

        /// <inheritdoc />
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastDamageTick, "lastMentalDamageTick");
        }
    }
}
