using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompProperties_AbnormalityHoldingPlatformTarget</c> 类型。</summary>
    public class CompProperties_AbnormalityHoldingPlatformTarget : CompProperties
    {
        /// <summary>表示 <c>heldPawnKind</c>。</summary>
        public PawnKindDef heldPawnKind;

        /// <summary>表示 <c>capturedLetterLabel</c>。</summary>
        [MustTranslate]
        public string capturedLetterLabel;

        /// <summary>表示 <c>capturedLetterText</c>。</summary>
        [MustTranslate]
        public string capturedLetterText;

        /// <summary>表示 <c>lookForTargetOnEscape</c>。</summary>
        public bool lookForTargetOnEscape = true;

        /// <summary>表示 <c>canBeExecuted</c>。</summary>
        public bool canBeExecuted = true;

        /// <summary>表示 <c>getsColdContainmentBonus</c>。</summary>
        public bool getsColdContainmentBonus;

        /// <summary>表示 <c>hasAnimation</c>。</summary>
        public bool hasAnimation = true;

        /// <summary>初始化 <c>CompProperties_AbnormalityHoldingPlatformTarget</c> 类的新实例。</summary>
        public CompProperties_AbnormalityHoldingPlatformTarget()
        {
            compClass = typeof(CompAbnormalityHoldingPlatformTarget);
        }
    }
}
