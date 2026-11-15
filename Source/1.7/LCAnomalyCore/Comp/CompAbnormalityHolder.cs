using LCAnomalyCore.Buildings;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompAbnormalityHolder</c> 类型。</summary>
    public abstract class CompAbnormalityHolder : ThingComp
    {
        /// <summary>获取 <c>ContainmentStrength</c>。</summary>
        public virtual float ContainmentStrength => Util.LCContainmentUtility.GetContainmentStrength(parent);

        /// <summary>获取 <c>Props</c>。</summary>
        public CompProperties_AbnormalityHolder Props => (CompProperties_AbnormalityHolder)props;

        /// <summary>获取 <c>HoldingPlatform</c>。</summary>
        protected Building_AbnormalityHoldingPlatform HoldingPlatform => (Building_AbnormalityHoldingPlatform)parent;

        /// <summary>获取或设置 <c>Available</c>。</summary>
        public abstract bool Available { get; }

        /// <summary>获取或设置 <c>HeldPawn</c>。</summary>
        public abstract Pawn HeldPawn { get; }

        /// <summary>获取或设置 <c>Container</c>。</summary>
        public abstract ThingOwner Container { get; }

        /// <summary>执行 <c>EjectContents</c> 定义的操作。</summary>
        public abstract void EjectContents();

        /// <inheritdoc />
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
