using Verse;

namespace LCAnomalyLibrary.Comp.Concrete
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