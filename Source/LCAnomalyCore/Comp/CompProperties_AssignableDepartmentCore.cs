using LCAnomalyCore.Building;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 部门核心CompProperties
    /// </summary>
    public class CompProperties_AssignableDepartmentCore : CompProperties
    {
        public EDepartmentType departmentType;
        public float healConsumeAmount;
        public float healAmount;
        public int healDuration;

        public CompProperties_AssignableDepartmentCore()
        {
            compClass = typeof(CompAssignableDepartmentCore);
        }
    }
}
