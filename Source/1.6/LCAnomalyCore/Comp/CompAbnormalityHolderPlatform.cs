using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompAbnormalityHolderPlatform : CompAbnormalityHolder
    {
        public override bool Available => !base.HoldingPlatform.Occupied;

        public override Pawn HeldPawn => base.HoldingPlatform.HeldPawn;

        public override ThingOwner Container => base.HoldingPlatform.innerContainer;

        public new CompProperties_AbnormalityHolderPlatform Props => (CompProperties_AbnormalityHolderPlatform)props;

        public override void EjectContents()
        {
            base.HoldingPlatform.EjectContents();
        }
    }
}
