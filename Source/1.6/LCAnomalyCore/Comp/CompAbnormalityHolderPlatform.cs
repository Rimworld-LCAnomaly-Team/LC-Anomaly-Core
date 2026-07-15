using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompAbnormalityHolderPlatform</c> 类型。</summary>
    public class CompAbnormalityHolderPlatform : CompAbnormalityHolder
    {
        /// <inheritdoc />
        public override bool Available => !base.HoldingPlatform.Occupied;

        /// <inheritdoc />
        public override Pawn HeldPawn => base.HoldingPlatform.HeldPawn;

        /// <inheritdoc />
        public override ThingOwner Container => base.HoldingPlatform.innerContainer;

        /// <summary>获取 <c>Props</c>。</summary>
        public new CompProperties_AbnormalityHolderPlatform Props => (CompProperties_AbnormalityHolderPlatform)props;

        /// <inheritdoc />
        public override void EjectContents()
        {
            base.HoldingPlatform.EjectContents();
        }
    }
}
