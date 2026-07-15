using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompProperties_EgoWeapon</c> 类型。</summary>
    public class CompProperties_EgoWeapon : CompProperties
    {
        /// <summary>表示 <c>level</c>。</summary>
        public string level = "ZAYIN";

        /// <summary>表示 <c>damageType</c>。</summary>
        public Damage.FourColorDamageType damageType = Damage.FourColorDamageType.None;

        /// <summary>表示 <c>damageMultiplier</c>。</summary>
        public float damageMultiplier = 1f;

        /// <summary>初始化 <c>CompProperties_EgoWeapon</c> 类的新实例。</summary>
        public CompProperties_EgoWeapon()
        {
            compClass = typeof(CompEgoWeapon);
        }
    }
}
