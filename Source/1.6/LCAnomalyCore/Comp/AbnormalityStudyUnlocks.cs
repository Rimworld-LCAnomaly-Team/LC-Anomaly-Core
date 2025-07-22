using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompAbnormalityStudyUnlocks : CompStudyUnlocks
    {
        protected new CompProperties_AbnormalityStudyUnlocks Props => (CompProperties_AbnormalityStudyUnlocks)props;

        public virtual void TransferStudyProgress(int progress)
        {
            for (int i = 0; i < progress; i++)
            {
                TransferStudyLevel(i);
            }

            UnlockNameCheck();
        }

        protected virtual void TransferStudyLevel(int i)
        {
            if (nextIndex <= i)
            {
                StudyNote studyNote = Props.studyNotes[i];
                nextIndex = i + 1;
                studyProgress = nextIndex;
                TaggedString label = studyNote.label;
                TaggedString text = studyNote.text;
                ChoiceLetter choiceLetter = LetterMaker.MakeLetter(label, text, LetterDefOf.NeutralEvent, parent);
                choiceLetter.arrivalTick = Find.TickManager.TicksGame;
                letters.Add(choiceLetter);
                Notify_StudyLevelChanged(choiceLetter);
            }
        }

        public virtual void UnlockNameCheck()
        {
        }
    }
}