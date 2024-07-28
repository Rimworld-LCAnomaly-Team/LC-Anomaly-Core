using LCAnomalyCore.UI;
using LCAnomalyLibrary.Comp;
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
        List<Pawn> pawnToDelete = new List<Pawn>();

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

                //只有安排了黑暗研究工作的单位才会出现在列表中
                return parent.Map.mapPawns.FreeColonists.Where(x => x.workSettings.WorkIsActive(WorkTypeDefOf.DarkStudy));
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (ShouldShowAssignmentGizmo())
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "LC_AssignmentGizmoLabel".Translate();
                command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/AssignmentGizmo", true);
                command_Action.defaultDesc = "LC_AssignmentGizmoDesc".Translate();
                command_Action.action = delegate ()
                {
                    CheckAssignedList();
                    Find.WindowStack.Add(new Dialog_LC_AssignEntity(this));
                };

                //不存在可分配单位就禁用按钮
                if (!AssigningCandidates.Any<Pawn>())
                    command_Action.Disable(base.Props.noAssignablePawnsDesc);

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
                if (!pawn.workSettings.WorkIsActive(WorkTypeDefOf.DarkStudy))
                    pawnToDelete.Add(pawn);
            }

            if(pawnToDelete.Count > 0)
            {
                foreach(Pawn pawn in pawnToDelete)
                    assignedPawns.Remove(pawn);

                pawnToDelete.Clear();
            }
        }

        /// <summary>
        /// 检查是否满足最低技能需求
        /// </summary>
        /// <param name="pawn">单位</param>
        /// <returns></returns>
        public bool CheckSkillRequire(Pawn pawn)
        {
            var building = parent as Building.Building_HoldingPlatform;
            if (building != null && building.HeldPawn != null)
            {
                var comp = building.HeldPawn.GetComp<LC_CompEntity>();
                if (comp != null)
                    return comp.CheckStudierSkillRequire(pawn);
            }

            return true;
        }
    }
}
