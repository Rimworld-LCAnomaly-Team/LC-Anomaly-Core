using RimWorld;
using Verse.AI;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该Mod所有的DutyDef
    /// </summary>
    [DefOf]
    public static class DutyDefOf
    {
        static DutyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DutyDefOf));
        }
    }
}