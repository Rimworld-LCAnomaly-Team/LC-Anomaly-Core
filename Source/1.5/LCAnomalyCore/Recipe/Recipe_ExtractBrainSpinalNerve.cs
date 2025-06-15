using LCAnomalyCore.Comp;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Recipe
{
    /// <summary>
    /// 提取脑脊髓神经的手术
    /// </summary>
    public class Recipe_ExtractBrainSpinalNerve : Recipe_Surgery
    {
        /// <summary>
        /// 是否可用
        /// </summary>
        /// <param name="thing">被执行者</param>
        /// <param name="part">身体部位</param>
        /// <returns>是否可用</returns>
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;

            if (pawn == null)
            {
                return false;
            }

            if (pawn.Faction == Faction.OfEntities)
            {
                return false;
            }

            if (pawn.health.hediffSet.HasHediff(Defs.HediffDefOf.CogitoExtracted))
            {
                return false;
            }

            return base.AvailableOnNow(thing, part);
        }

        /// <summary>
        /// 是否可用的提示
        /// </summary>
        /// <param name="thing">被执行者</param>
        /// <param name="part">身体部位</param>
        /// <returns>提示内容</returns>
        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn && pawn.DevelopmentalStage.Baby())
            {
                return "TooSmall".Translate();
            }

            return base.AvailableReport(thing, part);
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
            BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
            if (brain != null)
            {
                Find.BattleLog.Add(new BattleLogEntry_Event(pawn, RulePackDefOf.Event_UnnaturalCorpseAttack, billDoer));
                DamageDef psychic = DamageDefOf.Psychic;
                BodyPartRecord hitPart = brain;
                pawn.TakeDamage(new DamageInfo(psychic, 99999f, 99999f, -1f, billDoer, hitPart));

                OnSurgerySuccess(pawn, part, billDoer, ingredients, bill);
                if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
                {
                    ReportViolation(pawn, billDoer, pawn.HomeFaction, -1, Defs.HistoryEventDefOf.CogitoExtracted);
                }
            }
        }

        /// <summary>
        /// 执行手术成功后
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <param name="part">身体部位</param>
        /// <param name="billDoer">医生</param>
        /// <param name="ingredients">？</param>
        /// <param name="bill">手术</param>
        protected override void OnSurgerySuccess(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            var thing = ThingMaker.MakeThing(Defs.ThingDefOf.BrainSpinalNerve);
            thing.stackCount = 1;
            thing.TryGetComp<CompBrainSpinalNerve>().Init(pawn);

            bool spawnSuccess = GenPlace.TryPlaceThing(thing, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
            if (!spawnSuccess)
            {
                Log.Error("Could not drop BrainSpinalNerve near " + pawn.PositionHeld);
            }
        }
    }
}