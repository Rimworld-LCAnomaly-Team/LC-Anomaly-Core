using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompAbnormalityStudyUnlocks : ThingComp
    {
        private List<float> studyThresholds = new List<float>();

        protected List<ChoiceLetter> letters = new List<ChoiceLetter>();

        protected int nextIndex;

        protected int studyProgress;

        public CompProperties_AbnormalityStudyUnlocks Props => (CompProperties_AbnormalityStudyUnlocks)props;

        public IReadOnlyList<ChoiceLetter> Letters => letters;

        public int Progress => studyProgress;

        public bool Completed => nextIndex >= Props.studyNotes.Count;

        public override void PostPostMake()
        {
            base.PostPostMake();
            SetupStudyThresholds();

            var component = Components.LC;
            if (component != null)
            {
                component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                TransferStudyProgress(saved.StudyProgress);
            }
        }

        public virtual void OnStudied(Pawn studier, float amount, KnowledgeCategoryDef category = null)
        {
            if (!Completed)
            {
                float studyKnowledgeGained = amount;
                for (int i = nextIndex; i < Props.studyNotes.Count && !(studyKnowledgeGained < studyThresholds[i]); i++)
                {
                    RegisterStudyLevel(studier, i);
                }
            }
        }

        protected virtual void RegisterStudyLevel(Pawn studier, int i)
        {
            if (nextIndex <= i)
            {
                StudyNote studyNote = Props.studyNotes[i];
                nextIndex = i + 1;
                studyProgress = nextIndex;
                TaggedString label = studyNote.label;
                TaggedString text = studyNote.text;
                ChoiceLetter choiceLetter = LetterMaker.MakeLetter(label, text, LetterDefOf.NeutralEvent, parent);
                Find.LetterStack.ReceiveLetter(choiceLetter);
                ChoiceLetter keptLetter = LetterMaker.MakeLetter(label, text, LetterDefOf.NeutralEvent, parent);
                keptLetter.arrivalTick = Find.TickManager.TicksGame;
                letters.Add(keptLetter);
                Notify_StudyLevelChanged(keptLetter);

                var component = Components.LC;
                if (component != null)
                {
                    component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                    saved.StudyProgress = i + 1;
                    component.AnomalyStatusSavedDict[parent.def] = saved;
                }

                UnlockNameCheck();
            }
        }

        protected virtual void Notify_StudyLevelChanged(ChoiceLetter keptLetter)
        {
        }

        public virtual void TransferStudyProgress(int progress)
        {
            int levelsToTransfer = System.Math.Max(0, System.Math.Min(progress, Props.studyNotes.Count));
            for (int i = 0; i < levelsToTransfer; i++)
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

                var component = Components.LC;
                if (component != null)
                {
                    component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                    saved.StudyProgress = i + 1;
                    component.AnomalyStatusSavedDict[parent.def] = saved;
                }
            }
        }

        public virtual void UnlockNameCheck()
        {
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.ShowDevGizmos && !Completed)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Advance study",
                    action = delegate
                    {
                        RegisterStudyLevel(parent.MapHeld?.mapPawns?.FreeColonists?.RandomElement(), nextIndex);
                    }
                };
            }
        }

        private void SetupStudyThresholds()
        {
            studyThresholds.Clear();
            foreach (StudyNote studyNote in Props.studyNotes)
            {
                studyThresholds.Add((studyNote.threshold != 0f) ? studyNote.threshold : studyNote.thresholdRange.RandomInRange);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextIndex, "nextIndex", 0);
            Scribe_Values.Look(ref studyProgress, "studyProgress", 0);
            Scribe_Collections.Look(ref studyThresholds, "studyThresholds", LookMode.Value);
            Scribe_Collections.Look(ref letters, "letters", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (letters == null)
                {
                    letters = new List<ChoiceLetter>();
                }
                if (studyThresholds == null || studyThresholds.Count != Props.studyNotes.Count)
                {
                    studyThresholds = new List<float>();
                    SetupStudyThresholds();
                }

                nextIndex = System.Math.Max(0, System.Math.Min(nextIndex, Props.studyNotes.Count));
                studyProgress = System.Math.Max(0, System.Math.Min(studyProgress, Props.studyNotes.Count));
            }
        }
    }
}
