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
        /// <summary>每次再生周期可恢复的精神损伤（LC_MentalDamage）严重度，默认 0（安保部启用）。</summary>
        public float healMentalAmount = 0f;
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