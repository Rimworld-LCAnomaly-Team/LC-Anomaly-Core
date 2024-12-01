using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.UI;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 部门核心Comp
    /// </summary>
    public class CompAssignableDepartmentCore : CompAssignableToPawn
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public CompProperties_AssignableDepartmentCore Props => (CompProperties_AssignableDepartmentCore)props;

        /// <summary>
        /// 候选者列表
        /// </summary>
        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                if (!parent.Spawned)
                {
                    return Enumerable.Empty<Pawn>();
                }

                //只有拥有员工属性Comp的单位才会出现在分配列表中
                return parent.Map.mapPawns.FreeColonists.Where(x => x.GetComp<CompPawnStatus>() != null);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (ShouldShowAssignmentGizmo())
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "LC_AssignmentGizmo_DepartmentCore_Label".Translate();
                command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/Assignment/DepartmentCore", true);
                command_Action.defaultDesc = "LC_AssignmentGizmo_DepartmentCore_Desc".Translate();
                command_Action.action = delegate ()
                {
                    Find.WindowStack.Add(new Dialog_LC_DepartmentCoreAssign(this));
                };

                //不存在可分配工作的单位，就禁用按钮
                if (!AssigningCandidates.Any<Pawn>())
                    command_Action.Disable("LC_AssignmentGizmo_NoAssignablePawns_Desc".Translate());

                yield return command_Action;
            }
        }

        public override void TryAssignPawn(Pawn pawn)
        {
            uninstalledAssignedPawns.Remove(pawn);
            if (!assignedPawns.Contains(pawn))
            {
                assignedPawns.Add(pawn);
                pawn.GetComp<CompPawnStatus>()?.SetPawnStatusEnabled(true);

                SortAssignedPawns();
            }
        }

        public override void TryUnassignPawn(Pawn pawn, bool sort = true, bool uninstall = false)
        {
            if (assignedPawns.Contains(pawn))
            {
                assignedPawns.Remove(pawn);
                pawn.GetComp<CompPawnStatus>()?.SetPawnStatusEnabled(false);

                if (uninstall && pawn != null && !uninstalledAssignedPawns.Contains(pawn))
                {
                    uninstalledAssignedPawns.Add(pawn);
                }
                if (sort)
                {
                    SortAssignedPawns();
                }
            }
        }

        /// <summary>
        /// 进行治疗
        /// </summary>
        /// <param name="employee">员工</param>
        /// <param name="curFuel">当前燃料数量</param>
        /// <returns>治疗消耗倍率</returns>
        public int DoHeal(Pawn employee, float curFuel)
        {
            int times = 0;
            int maxTimes = (int)(curFuel / Props.healConsumeAmount);

            //治疗断肢
            List<BodyPartRecord> list_missingPart = HealthUtil.FindAllMissingBodyPart(employee);
            if (list_missingPart.Count > 0)
            {
                foreach (BodyPartRecord part in list_missingPart)
                {
                    //检查燃料是否够用
                    if (times + 2 > maxTimes)
                        return times;

                    //以两倍率消耗来修复肢体
                    employee.health.RestorePart(part);
                    times += 2;

                    DoRestoreEffect(employee, part);
                }
            }

            //治疗伤口
            List<Hediff_Injury> list_injury = [];
            employee.health.hediffSet.GetHediffs(ref list_injury);
            if (list_injury.Count > 0)
            {
                foreach (Hediff_Injury injury in list_injury)
                {
                    //检查燃料是否够用
                    if (times + 1 > maxTimes)
                        return times;

                    //以1倍率和指定耐久来治疗伤口
                    injury.Heal(Props.healAmount);
                    times++;
                }

                DoHealEffect(employee, Props.healAmount, list_injury.Count);
            }

            return times;
        }

        /// <summary>
        /// 触发断肢修复特效
        /// </summary>
        /// <param name="employee">pawn</param>
        /// <param name="part">部位</param>
        protected void DoRestoreEffect(Pawn employee, BodyPartRecord part)
        {
            MoteMaker.ThrowText(employee.PositionHeld.ToVector3(), employee.MapHeld, $"{part.def.label.Translate()}", Color.cyan);
            FleckMaker.ThrowMetaIcon(employee.PositionHeld, employee.MapHeld, FleckDefOf.HealingCross, 0.42f);
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