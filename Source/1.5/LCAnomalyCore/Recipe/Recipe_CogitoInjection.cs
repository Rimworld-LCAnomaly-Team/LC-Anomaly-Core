using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Recipe
{
    /// <summary>
    /// 提取Cogito的手术
    /// </summary>
    public class Recipe_CogitoInjection : Recipe_Surgery
    {
        /// <summary>
        /// 是否可执行
        /// </summary>
        /// <param name="surgeryTarget">被手术者</param>
        /// <returns>是否可执行</returns>
        public override bool CompletableEver(Pawn surgeryTarget)
        {
            if (surgeryTarget.health.hediffSet.HasHediff(Defs.HediffDefOf.CogitoInjected))
            {
                return false;
            }

            return base.CompletableEver(surgeryTarget);
        }

        /// <summary>
        /// 是否可用
        /// </summary>
        /// <param name="thing">被执行者</param>
        /// <param name="part">身体部位</param>
        /// <returns>是否可用</returns>
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing.MapHeld == null)
            {
                return false;
            }

            if (thing.MapHeld.listerThings.ThingsOfDef(Defs.ThingDefOf.Cogito).Count == 0)
            {
                return false;
            }

            if (thing is Pawn pawn && pawn.health.hediffSet.HasHediff(Defs.HediffDefOf.CogitoInjected))
            {
                return false;
            }

            return base.AvailableOnNow(thing, part);
        }

        /// <summary>
        /// 执行手术
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <param name="part">身体部位</param>
        /// <param name="billDoer">医生</param>
        /// <param name="ingredients">？</param>
        /// <param name="bill">手术</param>
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff hediff = HediffMaker.MakeHediff(Defs.HediffDefOf.CogitoInjected, pawn);
            hediff.Severity = 1f;
            pawn.health.AddHediff(hediff);
            OnSurgerySuccess(pawn, part, billDoer, ingredients, bill);
            if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction, -1, Defs.HistoryEventDefOf.CogitoInjected);
            }
        }
    }
}