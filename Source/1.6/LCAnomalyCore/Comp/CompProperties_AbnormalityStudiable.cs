using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompProperties_AbnormalityStudiable : CompProperties
    {
        public int studyTimesPeriod;

        public int frequencyTicks = -1;

        public float studyAmountToComplete = -1f;

        public bool showToggleGizmo;

        public bool studyEnabledByDefault = true;

        public bool canBeActivityDeactivated;

        public float knowledgeFactorOutdoors = 1f;

        public int minMonolithLevelForStudy;

        [MustTranslate]
        public string completedLetterTitle;

        [MustTranslate]
        public string completedLetterText;

        [MustTranslate]
        public string completedMessage;

        public LetterDef completedLetterDef;

        public bool Completable => studyAmountToComplete > 0f;

        public CompProperties_AbnormalityStudiable()
        {
            compClass = typeof(CompAbnormalityStudiable);
        }
    }
}