using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompProperties_AbnormalityStudyUnlocks</c> 类型。</summary>
    public class CompProperties_AbnormalityStudyUnlocks : CompProperties
    {
        /// <summary>表示 <c>studyNotes</c>。</summary>
        public List<StudyNote> studyNotes = new List<StudyNote>();

        /// <summary>表示 <c>defaultStudyAmount</c>。</summary>
        public float? defaultStudyAmount;

        /// <summary>表示 <c>defaultCategoryOverride</c>。</summary>
        public KnowledgeCategoryDef defaultCategoryOverride;

        /// <summary>初始化 <c>CompProperties_AbnormalityStudyUnlocks</c> 类的新实例。</summary>
        public CompProperties_AbnormalityStudyUnlocks()
        {
            compClass = typeof(CompAbnormalityStudyUnlocks);
        }

        /// <inheritdoc />
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
            float num = 0f;
            for (int i = 0; i < studyNotes.Count; i++)
            {
                StudyNote note = studyNotes[i];
                if (num != 0f)
                {
                    if (note.threshold != 0f && note.threshold <= num)
                    {
                        yield return $"Threshold for note at index {i} had a threshold value ({note.threshold}) lower than the previous maximum ({num})";
                    }
                    else if (note.threshold == 0f && note.thresholdRange.min <= num)
                    {
                        yield return $"Threshold for note at index {i} had a min threshold value ({note.thresholdRange.min}) lower than the previous maximum ({num})";
                    }
                }
                num = ((note.threshold != 0f) ? note.threshold : note.thresholdRange.max);
            }
        }
    }
}