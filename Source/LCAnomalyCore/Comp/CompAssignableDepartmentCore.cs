using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 部门核心Comp
    /// </summary>
    public class CompAssignableDepartmentCore : ThingComp
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public CompProperties_AssignableDepartmentCore Props => (CompProperties_AssignableDepartmentCore)props;

        /// <summary>
        /// 进行治疗
        /// </summary>
        /// <param name="employee">员工</param>
        /// <returns>是否执行了治疗</returns>
        public bool DoHeal(Pawn employee)
        {
            List<Hediff_Injury> list = [];
            employee.health.hediffSet.GetHediffs(ref list);

            if (list.Count > 0)
            {
                foreach(Hediff_Injury injury in list)
                {
                    injury.Heal(Props.healAmount);
                    DoHealEffect(employee, Props.healAmount, list.Count);
                }
                    

                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// 触发治疗特效
        /// </summary>
        /// <param name="employee">pawn</param>
        /// <param name="amount">单位治疗量</param>
        /// <param name="count">治疗部位数量</param>
        protected void DoHealEffect(Pawn employee, float amount, int count)
        {
            MoteMaker.ThrowText(employee.PositionHeld.ToVector3(), employee.MapHeld, $"{amount} * {count}", Color.green);
            FleckMaker.ThrowMetaIcon(employee.PositionHeld, employee.MapHeld, FleckDefOf.HealingCross, 0.42f);
        }
    }
}
