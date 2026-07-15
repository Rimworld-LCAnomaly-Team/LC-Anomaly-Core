using LCAnomalyCore.Damage;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.DamageWorker
{
    /// <summary>表示 <c>DamageWorker_FourColor</c> 类型。</summary>
    public class DamageWorker_FourColor : DamageWorker_AddInjury
    {
        /// <inheritdoc />
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            FourColorDamageExtension extension = dinfo.Def.GetModExtension<FourColorDamageExtension>();
            if (extension == null || extension.damageType == FourColorDamageType.None)
                return base.Apply(dinfo, victim);

            float adjustedAmount = DamageUtils.GetAdjustedDamage(dinfo, victim, extension.damageType,
                extension.riskLevel);
            dinfo.SetAmount(adjustedAmount);

            Pawn pawn = victim as Pawn;
            switch (extension.damageType)
            {
                case FourColorDamageType.White:
                    return DamageUtils.CanTakeMentalDamage(pawn)
                        ? ApplyMentalDamage(dinfo, pawn)
                        : base.Apply(dinfo, victim);
                case FourColorDamageType.Black:
                {
                    DamageResult result = base.Apply(dinfo, victim);
                    if (pawn != null && !pawn.Dead)
                        DamageUtils.ApplyMentalDamage(pawn, adjustedAmount, dinfo);
                    return result;
                }
                case FourColorDamageType.Pale:
                    dinfo.SetAmount(GetPaleDamage(adjustedAmount, victim));
                    if (pawn != null)
                        dinfo.SetHitPart(pawn.RaceProps.body.corePart);
                    dinfo.SetIgnoreArmor(ignoreArmor: true);
                    return base.Apply(dinfo, victim);
                default:
                    return base.Apply(dinfo, victim);
            }
        }

        private static DamageResult ApplyMentalDamage(DamageInfo dinfo, Pawn pawn)
        {
            DamageResult result = new DamageResult();
            if (pawn != null && !pawn.Dead)
                result.totalDamageDealt = DamageUtils.ApplyMentalDamage(pawn, dinfo.Amount, dinfo);
            return result;
        }

        private static float GetPaleDamage(float palePoints, Thing victim)
        {
            if (palePoints <= 0f)
                return 0f;

            if (victim is Pawn pawn && DamageUtils.CanTakeMentalDamage(pawn))
            {
                float incomingFactor = Mathf.Max(0.01f, pawn.GetStatValue(RimWorld.StatDefOf.IncomingDamageFactor));
                return Mathf.Max(1f, pawn.RaceProps.body.corePart.def.GetMaxHealth(pawn)
                    * palePoints * 0.01f / incomingFactor);
            }
            return palePoints;
        }
    }
}
