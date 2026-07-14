using HarmonyLib;
using LCAnomalyCore.Util;
using Verse;

namespace LCAnomalyCore.Patch
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage
    {
        public static void Prefix(ref DamageInfo dinfo)
        {
            DamageUtils.ConvertWeaponDamage(ref dinfo);
        }
    }
}
