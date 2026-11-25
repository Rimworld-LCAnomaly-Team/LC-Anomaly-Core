using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using Verse;

namespace LCAnomalyCore.Interface
{
    /// <summary>
    /// 统一收容单元接口（兼容传统收容平台与房间收容控制终端）。
    /// </summary>
    public interface IAbnormalityContainmentUnit : IHoldingPlatformWorkTypeSelectable
    {
        /// <summary>收容单元对应的建筑实体。</summary>
        Thing UnitThing { get; }

        /// <summary>关联的收容宿主组件。</summary>
        CompAbnormalityHolder HolderComp { get; }

        /// <summary>当前收容中的异常实体 Pawn（若未收容则为 null）。</summary>
        Pawn HeldPawn { get; }

        /// <summary>当前是否已收容异想体。</summary>
        bool Occupied { get; }

        /// <summary>是否为房间内活体收容模式（而非容器隐藏式平台）。</summary>
        bool IsRoomContainment { get; }

        /// <summary>当前收容的是否为工具型异想体。</summary>
        bool IsHoldingTool { get; }

        /// <summary>当前收容的工具型异想体组件（若非工具型则为 null）。</summary>
        CompAbnormalityTool HeldToolComp { get; }

        /// <summary>当前收容异想体的研究组件。</summary>
        CompAbnormalityStudiable CompAbnormalityStudiable { get; }

        /// <summary>收容单元的员工指派组件。</summary>
        CompAssignableToPawn_LC_Entity CompAssignable { get; }

        /// <summary>当前收容单元所在房间。</summary>
        Room ContainmentRoom { get; }

        /// <summary>校验当前收容单元是否可接收指定待收容目标。</summary>
        bool CanAcceptCapture(Thing candidate, out string failReason);

        /// <summary>将待收容的蛋或异常实体纳入本收容单元。</summary>
        bool TryCaptureThing(Thing takee);

        /// <summary>开始一次研究工作时调用。</summary>
        void Notify_StudyStart(Pawn studier);

        /// <summary>研究工作进行期间每 Tick 调用。</summary>
        void Notify_Studying(Pawn studier);

        /// <summary>研究工作每轮间隔结算时调用。</summary>
        void Notify_StudyInterval(CompPawnStatus studier);

        /// <summary>释放或解除当前收容绑定。</summary>
        void EjectContents();
    }
}
