using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompProperties_AbnormalityStudiable</c> 类型。</summary>
    public class CompProperties_AbnormalityStudiable : CompProperties
    {
        /// <summary>表示 <c>studyTimesPeriod</c>。</summary>
        public int studyTimesPeriod;

        /// <summary>表示 <c>frequencyTicks</c>。</summary>
        public int frequencyTicks = -1;

        /// <summary>表示 <c>studyAmountToComplete</c>。</summary>
        public float studyAmountToComplete = -1f;

        /// <summary>表示 <c>showToggleGizmo</c>。</summary>
        public bool showToggleGizmo;

        /// <summary>表示 <c>studyEnabledByDefault</c>。</summary>
        public bool studyEnabledByDefault = true;

        /// <summary>表示 <c>canBeActivityDeactivated</c>。</summary>
        public bool canBeActivityDeactivated;

        /// <summary>表示 <c>knowledgeFactorOutdoors</c>。</summary>
        public float knowledgeFactorOutdoors = 1f;

        /// <summary>表示 <c>minMonolithLevelForStudy</c>。</summary>
        public int minMonolithLevelForStudy;

        /// <summary>表示 <c>completedLetterTitle</c>。</summary>
        [MustTranslate]
        public string completedLetterTitle;

        /// <summary>表示 <c>completedLetterText</c>。</summary>
        [MustTranslate]
        public string completedLetterText;

        /// <summary>表示 <c>completedMessage</c>。</summary>
        [MustTranslate]
        public string completedMessage;

        /// <summary>表示 <c>completedLetterDef</c>。</summary>
        public LetterDef completedLetterDef;

        /// <summary>获取 <c>Completable</c>。</summary>
        public bool Completable => studyAmountToComplete > 0f;

        /// <summary>初始化 <c>CompProperties_AbnormalityStudiable</c> 类的新实例。</summary>
        public CompProperties_AbnormalityStudiable()
        {
            compClass = typeof(CompAbnormalityStudiable);
        }
    }
}