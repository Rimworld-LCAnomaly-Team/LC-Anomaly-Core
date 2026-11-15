using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompAbnormalityStudyUnlocks</c> 类型。</summary>
    public class CompAbnormalityStudyUnlocks : ThingComp
    {
        private List<float> studyThresholds = new List<float>();

        /// <summary>表示 <c>letters</c>。</summary>
        protected List<ChoiceLetter> letters = new List<ChoiceLetter>();

        /// <summary>表示 <c>nextIndex</c>。</summary>
        protected int nextIndex;

        /// <summary>表示 <c>studyProgress</c>。</summary>
        protected int studyProgress;

        /// <summary>获取 <c>Props</c>。</summary>
        public CompProperties_AbnormalityStudyUnlocks Props => (CompProperties_AbnormalityStudyUnlocks)props;

        /// <summary>获取 <c>Letters</c>。</summary>
        public IReadOnlyList<ChoiceLetter> Letters => letters;

        /// <summary>获取 <c>Progress</c>。</summary>
        public int Progress => studyProgress;

        /// <summary>获取 <c>Completed</c>。</summary>
        public bool Completed => nextIndex >= Props.studyNotes.Count;

        /// <inheritdoc />
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

        /// <summary>执行 <c>OnStudied</c> 定义的操作。</summary>
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

        /// <summary>执行 <c>RegisterStudyLevel</c> 定义的操作。</summary>
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

        /// <summary>执行 <c>Notify_StudyLevelChanged</c> 定义的操作。</summary>
        protected virtual void Notify_StudyLevelChanged(ChoiceLetter keptLetter)
        {
        }

        /// <summary>执行 <c>TransferStudyProgress</c> 定义的操作。</summary>
        public virtual void TransferStudyProgress(int progress)
        {
            int levelsToTransfer = System.Math.Max(0, System.Math.Min(progress, Props.studyNotes.Count));
            for (int i = 0; i < levelsToTransfer; i++)
            {
                TransferStudyLevel(i);
            }

            UnlockNameCheck();
        }

        /// <summary>执行 <c>TransferStudyLevel</c> 定义的操作。</summary>
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

        /// <summary>执行 <c>UnlockNameCheck</c> 定义的操作。</summary>
        public virtual void UnlockNameCheck()
        {
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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
