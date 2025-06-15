using HarmonyLib;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 关于Pawn的补丁（为了提供暂停机制处理）
    /// </summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
    public class Patch_TickManager
    {
        /// <summary>
        /// Postfix
        /// </summary>
        /// <param name="__instance">原来的反射对象</param>
        private static void Postfix(TickManager __instance)
        {
            //Log.Warning("Patch_TickManager.Pause 注入成功");

            //Log.Warning("暂停机制：玩家尝试暂停");
            //Find.CurrentMap.mapPawns.FreeColonists.RandomElement().Kill(null);
            //Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        }
    }
}