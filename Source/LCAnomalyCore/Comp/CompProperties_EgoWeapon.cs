using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompProperties_EgoWeapon : CompProperties
    {
        public string level = "ZAYIN";

        public CompProperties_EgoWeapon()
        {
            compClass = typeof(CompEgoWeapon);
        }
    }
}