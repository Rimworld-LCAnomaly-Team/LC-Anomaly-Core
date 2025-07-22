using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompProperties_AbnormalityHoldingPlatformTarget : CompProperties
    {
        public PawnKindDef heldPawnKind;

        [MustTranslate]
        public string capturedLetterLabel;

        [MustTranslate]
        public string capturedLetterText;

        public bool lookForTargetOnEscape = true;

        public bool canBeExecuted = true;

        public bool getsColdContainmentBonus;

        public bool hasAnimation = true;

        public CompProperties_AbnormalityHoldingPlatformTarget()
        {
            compClass = typeof(CompAbnormalityHoldingPlatformTarget);
        }
    }
}
