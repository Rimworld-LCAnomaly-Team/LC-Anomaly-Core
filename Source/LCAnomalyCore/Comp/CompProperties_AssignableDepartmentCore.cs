using LCAnomalyCore.Building;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 部门核心CompProperties
    /// </summary>
    public class CompProperties_AssignableDepartmentCore : CompProperties_AssignableToPawn
    {
        public EDepartmentType departmentType;
        public float healConsumeAmount;
        public float healAmount;
        public int healDuration;

        public Color fillableBarColor = Color.white;

        public CompProperties_AssignableDepartmentCore()
        {
            compClass = typeof(CompAssignableDepartmentCore);
        }
    }
}
