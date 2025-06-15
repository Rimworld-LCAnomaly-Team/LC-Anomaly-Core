using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class LC_CompStudyUnlocks : CompStudyUnlocks
    {
        protected new LC_CompProperties_StudyUnlocks Props => (LC_CompProperties_StudyUnlocks)props;

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