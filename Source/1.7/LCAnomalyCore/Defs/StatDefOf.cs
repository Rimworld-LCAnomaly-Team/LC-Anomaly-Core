using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>表示 <c>StatDefOf</c> 类型。</summary>
    [DefOf]
    public static class StatDefOf
    {
        /// <summary>表示 <c>LC_ContainmentStrength</c>。</summary>
        public static StatDef LC_ContainmentStrength;

        /// <summary>表示 <c>LC_MinimumContainmentStrength</c>。</summary>
        public static StatDef LC_MinimumContainmentStrength;
        public static StatDef LC_AbnormalityStudyRate;

        public static StatDef LC_FourColor_RiskLevel;
        public static StatDef LC_FourColor_DamageType;
        public static StatDef LC_FourColor_WeaponDamage;
        public static StatDef LC_FourColor_SkillDamage;
        public static StatDef LC_FourColor_DamageMultiplier;
        public static StatDef LC_FourColor_ResistRed;
        public static StatDef LC_FourColor_ResistWhite;
        public static StatDef LC_FourColor_ResistBlack;
        public static StatDef LC_FourColor_ResistPale;

        static StatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(StatDefOf));
        }
    }
}
