using LCAnomalyLibrary.Comp;
using LCAnomalyLibrary.Util;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompPeBoxProduce : LC_CompPeBoxProduce
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public new CompProperties_PeBoxProduce Props => (CompProperties_PeBoxProduce)props;

        public override void CheckSpawnPeBox(Pawn studier, LC_StudyResult result)
        {
            if (!Defs.ResearchProjectDefOf.ExtractEnkephalin.IsFinished)
            {
                Log.Warning($"工作：未完成研究项目：{Defs.ResearchProjectDefOf.ExtractEnkephalin.label.Translate()}，无法生成PeBox");
                return;
            }

            base.CheckSpawnPeBox(studier, result);
        }
    }
}