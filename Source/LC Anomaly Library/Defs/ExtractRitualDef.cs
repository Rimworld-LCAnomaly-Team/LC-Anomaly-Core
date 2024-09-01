using LCAnomalyLibrary.Jobs;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LCAnomalyLibrary.Defs
{
    /// <summary>
    /// 提取仪式Def
    /// </summary>
    public class ExtractRitualDef : PsychicRitualDef
    {
        /// <summary>
        /// 开一个新的Lord
        /// </summary>
        /// <param name="assignments"></param>
        /// <returns></returns>
        public override Lord MakeNewLord(PsychicRitualRoleAssignments assignments)
        {
            Lord lord = LordMaker.MakeNewLord(Faction.OfPlayer, new LordJob_ExtractRitual(this, assignments), Find.CurrentMap, assignments.AllAssignedPawns);
            if (assignments.Target.Thing is Verse.Building b)
            {
                lord.AddBuilding(b);
            }

            return lord;
        }
    }
}