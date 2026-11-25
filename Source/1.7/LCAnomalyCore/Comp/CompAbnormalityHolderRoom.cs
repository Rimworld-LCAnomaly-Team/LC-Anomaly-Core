using LCAnomalyCore.Buildings;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>房间收容控制终端专用宿主组件属性。</summary>
    public class CompProperties_AbnormalityHolderRoom : CompProperties_AbnormalityHolder
    {
        /// <summary>房间结构失效或异想体越界后触发突破前的宽限 Tick 数。</summary>
        public int breachGraceTicks = 120;

        /// <summary>初始化 <c>CompProperties_AbnormalityHolderRoom</c> 类的新实例。</summary>
        public CompProperties_AbnormalityHolderRoom()
        {
            compClass = typeof(CompAbnormalityHolderRoom);
        }
    }

    /// <summary>房间活体收容模式的宿主组件（不隐藏实体，而是直接引用房间内 Pawn）。</summary>
    public class CompAbnormalityHolderRoom : CompAbnormalityHolder
    {
        /// <summary>关联的房间收容控制终端建筑。</summary>
        protected Building_AbnormalityContainmentTerminal Terminal => parent as Building_AbnormalityContainmentTerminal;

        /// <summary>获取 <c>Props</c>。</summary>
        public new CompProperties_AbnormalityHolderRoom Props => (CompProperties_AbnormalityHolderRoom)props;

        /// <inheritdoc />
        public override bool UsesContainer => false;

        /// <inheritdoc />
        public override bool Available => Terminal != null && !Terminal.Occupied && Terminal.IsRoomValidForContainment(out _);

        /// <inheritdoc />
        public override Pawn HeldPawn => Terminal?.HeldPawn;

        /// <inheritdoc />
        public override ThingOwner Container => null;

        /// <inheritdoc />
        public override bool CanAcceptAbnormality(Thing candidate, out string failReason)
        {
            if (Terminal == null)
            {
                failReason = null;
                return false;
            }

            return Terminal.CanAcceptCapture(candidate, out failReason);
        }

        /// <inheritdoc />
        public override void EjectContents()
        {
            Terminal?.EjectContents();
        }
    }
}
