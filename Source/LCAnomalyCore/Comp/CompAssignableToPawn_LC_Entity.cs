using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.UI;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 异想体研究平台分配单位自动研究的Comp
    /// </summary>
    public class CompAssignableToPawn_LC_Entity : CompAssignableToPawn
    {
        private List<Pawn> pawnToDelete = new List<Pawn>();

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

                //只有安排了异想体研究工作，且拥有脑叶员工属性，并且处于激活状态的小人，才能够出现在实体自动研究分配列表里
                return parent.Map.mapPawns.FreeColonists.Where(x => x.workSettings.WorkIsActive(Defs.WorkTypeDefOf.AbnormalityStudy) && x.GetComp<CompPawnStatus>() != null && x.GetComp<CompPawnStatus>().Enabled);
            }
        }

        public override IEnumerable<Verse.Gizmo> CompGetGizmosExtra()
        {
            if (ShouldShowAssignmentGizmo())
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "LC_AssignmentGizmo_HoldingPlatform_Label".Translate();
                command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/Assignment/HoldingPlatform", true);
                command_Action.defaultDesc = "LC_AssignmentGizmo_HoldingPlatform_Desc".Translate();
                command_Action.action = delegate ()
                {
                    CheckAssignedList();
                    Find.WindowStack.Add(new Dialog_LC_AssignEntity(this));
                };

                //不存在可分配工作的单位，或平台上没有实体，就禁用按钮
                if (!AssigningCandidates.Any<Pawn>())
                    command_Action.Disable("LC_AssignmentGizmo_HoldingPlatform_NoAssignablePawns_Desc".Translate());
                else if (((Building_HoldingPlatform)parent).HeldPawn == null)
                    command_Action.Disable("LC_AssignmentGizmo_HoldingPlatform_NoAbnormalityOnPlatform_Desc".Translate());

                yield return command_Action;
            }
        }

        /// <summary>
        /// 检测并移除列表中没有分配黑暗研究工作的单位
        /// </summary>
        protected void CheckAssignedList()
        {
            pawnToDelete.Clear();

            //从列表中移除没有安排黑暗研究工作的单位
            foreach (Pawn pawn in assignedPawns)
            {
                if (!pawn.workSettings.WorkIsActive(Defs.WorkTypeDefOf.AbnormalityStudy))
                    pawnToDelete.Add(pawn);
            }

            if (pawnToDelete.Count > 0)
            {
                foreach (Pawn pawn in pawnToDelete)
                    assignedPawns.Remove(pawn);

                pawnToDelete.Clear();
            }
        }

        /// <summary>
        /// 检查是否满足最低技能需求
        /// </summary>
        /// <param name="pawn">单位</param>
        /// <returns></returns>
        public float CheckSkillRequire(Pawn pawn)
        {
            var building = parent as Building.Building_HoldingPlatform;
            if (building != null && building.HeldPawn != null)
            {
                var comp = building.HeldPawn.GetComp<LC_CompEntity>();
                if (comp != null)
                    return comp.CheckStudierSkillRequire(pawn.GetComp<CompPawnStatus>(), building.CurWorkType);
            }

            return 0;
        }

        /// <summary>
        /// 清理所有分配人员
        /// </summary>
        public void ClearAllAssignments()
        {
            assignedPawns.Clear();
        }
    }
}