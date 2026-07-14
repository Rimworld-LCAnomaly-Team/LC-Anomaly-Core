using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompAbnormalityStudiable : ThingComp
    {
        private static readonly CachedTexture StudyToggleIcon = new CachedTexture("UI/Commands/WorkableUI");

        public CompProperties_AbnormalityStudiable Props => (CompProperties_AbnormalityStudiable)props;

        public bool Completed
        {
            get
            {
                if (Props.Completable)
                {
                    return ProgressPercent >= 1f;
                }

                return false;
            }
        }

        public float ProgressPercent
        {
            get
            {
                if (!Props.Completable)
                {
                    return 0f;
                }

                return studyPoints / Props.studyAmountToComplete;
            }
        }

        public float studyPoints;
        public bool studyEnabled = true;
        public int lastStudiedTick = -9999999;
        public int TicksTilNextStudy => lastStudiedTick + Props.frequencyTicks - Find.TickManager.TicksGame;
        public int studyInteractions;

        protected CompAbnormalityStudyUnlocks CompStudyUnlocks
        {
            get
            {
                compStudyUnlocks ??= parent.GetComp<CompAbnormalityStudyUnlocks>();
                return compStudyUnlocks;
            }
        }

        private CompAbnormalityStudyUnlocks compStudyUnlocks;

        public int StudyTimesPeriod => Props.studyTimesPeriod;

        #region 生命周期

        public override void PostPostMake()
        {
            base.PostPostMake();
            studyEnabled = Props.studyEnabledByDefault;
        }

        #endregion

        public void SetStudyEnabled(bool enabled)
        {
            studyEnabled = enabled;
        }

        public virtual void Study(Pawn studier, float studyAmount)
        {
            bool completed = Completed;

            studyAmount *= Find.Storyteller.difficulty.researchSpeedFactor;
            if (studier != null)
            {
                studyAmount *= studier.GetStatValue(StatDefOf.ResearchSpeed);
            }

            studyPoints += studyAmount;

            studier?.skills.Learn(SkillDefOf.Intellectual, 0.1f);

            if (Props.Completable && studyPoints >= Props.studyAmountToComplete)
            {
                studyPoints = Props.studyAmountToComplete;
            }

            CompStudyUnlocks?.OnStudied(studier, studyPoints);

            if (!completed && Completed)
            {
                QuestUtility.SendQuestTargetSignals(parent.questTags, "Researched", parent.Named("SUBJECT"), studier.Named("STUDIER"));
                if (!Props.completedMessage.NullOrEmpty())
                {
                    Messages.Message(Props.completedMessage, parent, MessageTypeDefOf.NeutralEvent);
                }

                if (studier != null && !Props.completedLetterText.NullOrEmpty() && !Props.completedLetterTitle.NullOrEmpty())
                {
                    Find.LetterStack.ReceiveLetter(Props.completedLetterTitle.Formatted(studier.Named("STUDIER"), parent.Named("PARENT")), Props.completedLetterText.Formatted(studier.Named("STUDIER"), parent.Named("PARENT")), Props.completedLetterDef ?? LetterDefOf.NeutralEvent, new List<Thing> { parent, studier });
                }
            }
        }

        public bool CurrentlyStudiable()
        {
            if (!studyEnabled)
            {
                return false;
            }

            if (Props.frequencyTicks > 0 && TicksTilNextStudy > 0)
            {
                return false;
            }

            if (parent is Pawn pawn)
            {
                var compHoldingPlatformTarget = pawn.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
                if (compHoldingPlatformTarget == null || !compHoldingPlatformTarget.CanStudy)
                {
                    return false;
                }

                if (pawn.Downed || !pawn.Awake())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 研究解锁带来的工作速度加成
        /// </summary>
        /// <returns></returns>
        public virtual float GetWorkSpeedOffset()
        {
            return 0;
        }

        /// <summary>
        /// 研究解锁带来的成功率加成
        /// </summary>
        /// <returns></returns>
        public virtual float GetWorkSuccessRateOffset()
        {
            return 0;
        }

        public void Notify_ActivityDeactivated()
        {
            if (Props.canBeActivityDeactivated)
            {
                studyEnabled = false;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastStudiedTick, "lastStudiedTick", 0);
            Scribe_Values.Look(ref studyEnabled, "studyEnabled", defaultValue: true);
            Scribe_Values.Look(ref studyPoints, "studiedAmount", 0f);
            Scribe_Values.Look(ref studyInteractions, "studyInteractions", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                float value = 0f;
                Scribe_Values.Look(ref value, "progress", 0f);
                if (value > 0f)
                {
                    float num = value / Props.studyAmountToComplete;
                    studyPoints = Props.studyAmountToComplete * num;
                }
            }

            if (ModsConfig.AnomalyActive && Scribe.mode == LoadSaveMode.PostLoadInit && Find.StudyManager.backCompatStudyProgress.TryGetValue(parent.def, out var value2))
            {
                studyPoints = Props.studyAmountToComplete * value2;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            string reason;
            if (Props.showToggleGizmo)
            {
                Command_Toggle command_Toggle = new Command_Toggle
                {
                    defaultLabel = "LC_CommandToggleStudy".Translate(),
                    defaultDesc = "LC_CommandToggleStudyDesc".Translate(),
                    icon = StudyToggleIcon.Texture,
                    isActive = () => studyEnabled,
                    toggleAction = delegate
                    {
                        SetStudyEnabled(!studyEnabled);
                    },
                    hideIconIfDisabled = true
                };
                command_Toggle.tutorTag = "ToggleStudy";

                yield return command_Toggle;
            }

            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            if (TicksTilNextStudy > 0)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "DEV: End study cooldown";
                command_Action.action = delegate
                {
                    lastStudiedTick = Find.TickManager.TicksGame - Props.frequencyTicks;
                };
                yield return command_Action;
            }

            if (!Props.Completable || Completed)
            {
                yield break;
            }

            Command_Action command_Action2 = new Command_Action();
            command_Action2.defaultLabel = "DEV: Complete study";
            command_Action2.action = delegate
            {
                int num = 100;
                while (!Completed && num > 0)
                {
                    Study(parent.Map?.mapPawns?.FreeColonists?.RandomElement(), float.MaxValue);
                    num--;
                }
            };
            yield return command_Action2;
        }

    }
}
