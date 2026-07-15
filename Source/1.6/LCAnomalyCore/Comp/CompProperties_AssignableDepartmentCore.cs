using LCAnomalyCore.Buildings;
using RimWorld;
using UnityEngine;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// 部门核心CompProperties
    /// </summary>
    public class CompProperties_AssignableDepartmentCore : CompProperties_AssignableToPawn
    {
        /// <summary>表示 <c>departmentType</c>。</summary>
        public EDepartmentType departmentType;
        /// <summary>表示 <c>healConsumeAmount</c>。</summary>
        public float healConsumeAmount;
        /// <summary>表示 <c>healAmount</c>。</summary>
        public float healAmount;
        /// <summary>表示 <c>healDuration</c>。</summary>
        public int healDuration;

        /// <summary>表示 <c>fillableBarColor</c>。</summary>
        public Color fillableBarColor = Color.white;

        /// <summary>初始化 <c>CompProperties_AssignableDepartmentCore</c> 类的新实例。</summary>
        public CompProperties_AssignableDepartmentCore()
        {
            compClass = typeof(CompAssignableDepartmentCore);
        }
    }
}