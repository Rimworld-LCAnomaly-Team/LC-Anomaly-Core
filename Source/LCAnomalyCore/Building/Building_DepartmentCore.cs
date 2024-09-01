using LCAnomalyCore.Comp;
using LCAnomalyLibrary.Comp.Pawns;
using LCAnomalyLibrary.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    /// <summary>
    /// 部门核心建筑
    /// </summary>
    public class Building_DepartmentCore : Verse.Building
    {
        /// <summary>
        /// 部门comp
        /// </summary>
        public CompAssignableDepartmentCore CompDepartment
        {
            get
            {
                compDepartment ??= GetComp<CompAssignableDepartmentCore>();
                return compDepartment;
            }
        }
        private CompAssignableDepartmentCore compDepartment;

        /// <summary>
        /// 燃料Comp
        /// </summary>
        public CompRefuelable CompRefuelable
        {
            get
            {
                compRefuelable ??= GetComp<CompRefuelable>();
                return compRefuelable;
            }
        }
        private CompRefuelable compRefuelable;

        public override void Tick()
        {
            base.Tick();
            
            //按设定的时间间隔进行房间治疗
            if (Find.TickManager.TicksGame % CompDepartment.Props.healDuration == 0)
                TryHeal();
        }

        /// <summary>
        /// 房间内的员工治疗
        /// </summary>
        protected void TryHeal()
        {
            //不在房间内则不进行治疗
            var room = InteractionCell.GetRoom(this.MapHeld);
            if (room == null)
                return;

            //获取房间内所有的Pawn
            int healTimes = 0;
            var roomPawns = room.ContainedThings<Pawn>();
            foreach (Pawn employee in roomPawns)
            {
                //不是玩家派系的pawn禁止恢复
                if (employee.Faction == null || employee.Faction != Faction.OfPlayer)
                    continue;

                //不存在或者没激活员工状态则禁止恢复
                var comp = employee.GetComp<CompPawnStatus>();
                if (comp == null || !comp.Triggered)
                    continue;


                if(CompDepartment.DoHeal(employee))
                    healTimes++;
            }

            //按照治疗次数消耗燃料
            if (healTimes > 0)
            {
                float amount = CompDepartment.Props.healConsumeAmount * healTimes;
                CompRefuelable.ConsumeFuel(amount);

                LogUtil.Message($"{def.defName} consumed {amount} KCorpAmpoule.");
            }
        }



        /// <summary>
        /// 绘制方法
        /// </summary>
        /// <param name="drawLoc">位置</param>
        /// <param name="flip">是否翻转</param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
        }
    }

    /// <summary>
    /// 部门类型枚举
    /// </summary>
    public enum EDepartmentType
    {
        /// <summary>
        /// 控制部
        /// </summary>
        ControlTeam = 0,
    }
}
