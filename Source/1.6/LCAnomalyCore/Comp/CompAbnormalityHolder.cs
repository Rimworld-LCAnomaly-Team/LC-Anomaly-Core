using LCAnomalyCore.Buildings;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    public abstract class CompAbnormalityHolder : ThingComp
    {
        public virtual float ContainmentStrength => Util.LCContainmentUtility.GetContainmentStrength(parent);

        public CompProperties_AbnormalityHolder Props => (CompProperties_AbnormalityHolder)props;

        protected Building_AbnormalityHoldingPlatform HoldingPlatform => (Building_AbnormalityHoldingPlatform)parent;

        public abstract bool Available { get; }

        public abstract Pawn HeldPawn { get; }

        public abstract ThingOwner Container { get; }

        public abstract void EjectContents();

        public override string CompInspectStringExtra()
        {
            string text = base.CompInspectStringExtra();
            if (!text.NullOrEmpty())
            {
                text += "\n";
            }

            float statValue = ContainmentStrength;
            text += $"{Defs.StatDefOf.LC_ContainmentStrength.LabelCap}: {statValue:F0}";
            if (!parent.Spawned)
            {
                return text;
            }

            if (parent.IsOutside())
            {
                text += string.Format(" ({0})", "Outdoors".Translate());
            }
            return text;
        }
    }
}
