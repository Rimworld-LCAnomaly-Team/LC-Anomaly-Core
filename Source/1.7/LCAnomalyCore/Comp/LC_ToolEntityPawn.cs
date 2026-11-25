using LCAnomalyCore.Buildings;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 工具型异想体实体 Pawn 基类：
    /// 收容于收容平台时作为无攻击性实体存在并驱动工具组件；
    /// 离开收容平台、被击杀或意外流落地面时自动洁净退化为异想体蛋。
    /// </summary>
    public class LC_ToolEntityPawn : LC_EntityBasePawn
    {
        private CompAbnormalityTool toolComp;

        /// <summary>获取关联的工具组件。</summary>
        public CompAbnormalityTool ToolComp => toolComp ??= GetComp<CompAbnormalityTool>();

        /// <inheritdoc />
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (mindState != null)
            {
                mindState.Active = false;
            }
        }

        /// <inheritdoc />
        protected override void Tick()
        {
            CompAbnormalityHoldingPlatformTarget holdTarget = GetComp<CompAbnormalityHoldingPlatformTarget>();
            if (holdTarget != null && holdTarget.CurrentlyContainedInRoom)
            {
                TickHolded();
                base.Tick();
                return;
            }

            // 工具型异想体严禁离开平台或房间收容范围活动：落地且未被搬运时立即温和化蛋并销毁实体
            if (Spawned && CarriedBy == null && !Dead && !Destroyed)
            {
                ToolComp?.RevertToEgg(Map, Position);
                return;
            }

            base.Tick();
        }

        /// <inheritdoc />
        public override void TickHolded()
        {
            base.TickHolded();
            ToolComp?.TickWhileHeld();
        }

        /// <inheritdoc />
        public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            Map prevMap = MapHeld;
            IntVec3 prevPos = PositionHeld;
            CompAbnormalityTool tool = ToolComp;
            tool?.OnLeftPlatform();

            base.Kill(dinfo, exactCulprit);

            tool?.RevertToEgg(prevMap, prevPos);
        }

        /// <inheritdoc />
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (ToolComp != null && ToolComp.TryDrawHeldOverride(phase, drawLoc))
            {
                return;
            }

            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }

        /// <inheritdoc />
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            ToolComp?.DrawExtraSelectionOverlaysForUnit(ToolComp.HeldUnit);
        }
    }
}
