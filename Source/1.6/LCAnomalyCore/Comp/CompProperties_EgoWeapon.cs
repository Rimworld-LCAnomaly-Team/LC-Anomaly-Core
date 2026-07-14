using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompProperties_EgoWeapon : CompProperties
    {
        public string level = "ZAYIN";

        public Damage.FourColorDamageType damageType = Damage.FourColorDamageType.None;

        public float damageMultiplier = 1f;

        public CompProperties_EgoWeapon()
        {
            compClass = typeof(CompEgoWeapon);
        }
    }
}
