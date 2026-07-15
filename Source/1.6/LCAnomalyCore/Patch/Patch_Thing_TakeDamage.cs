using HarmonyLib;
using LCAnomalyCore.Util;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>表示 <c>Patch_Thing_TakeDamage</c> 类型。</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage
    {
        /// <summary>执行 <c>Prefix</c> 定义的操作。</summary>
        public static void Prefix(ref DamageInfo dinfo)
        {
            DamageUtils.ConvertWeaponDamage(ref dinfo);
        }
    }
}
