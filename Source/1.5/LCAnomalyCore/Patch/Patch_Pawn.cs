using HarmonyLib;
using LCAnomalyCore.Settings;
using LCAnomalyCore.Util;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 关于Pawn的补丁（为了提供死亡警报点数）
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public class Patch_PawnKill
    {
        /// <summary>
        /// Postfix
        /// </summary>
        /// <param name="__instance">原来的反射对象</param>
        /// <param name="dinfo">伤害信息</param>
        /// <param name="exactCulprit">?</param>
        private static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            //Log.Warning("Patch_Pawn.Kill 注入成功");

            PatchPawnUtils.CalculateWarningPoints(__instance);
        }
    }

    /// <summary>
    /// 关于Pawn的补丁（为了提供DeSpawn警报点数）
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    public class Patch_PawnDespawn
    {
        /// <summary>
        /// PostFix
        /// </summary>
        /// <param name="__instance">原来的反射对象</param>
        /// <param name="mode">消失模式</param>
        private static void Postfix(Pawn __instance, DestroyMode mode = DestroyMode.Vanish)
        {
            //Log.Warning("Patch_Pawn.Despawn 注入成功");

            //似了后DeSpawn不重复计算
            //if(!__instance.Dead)
            //    PatchPawnUtils.CalculateWarningPoints(__instance);
        }
    }

    public static class PatchPawnUtils
    {
        public static void CalculateWarningPoints(Pawn pawn)
        {
            //LC组件null直接返回原方法
            if (Components.LC == null)
            {
                //Log.Error("警报点数（Pawn）：GameComponent_LC is null");
                return;
            }

            //Pawn不能为null
            if (pawn == null)
            {
                //Log.Error("警报点数（Pawn）：Pawn is null");
                return;
            }

            //如果未启用警报机制，就返回原方法
            if (!Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarning)
            {
                //Log.Warning("警报点数（Pawn）：未启用警报机制");
                return;
            }

            //如果未启用死亡警报机制，就返回原方法
            if (!Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarningDeath)
            {
                //Log.Warning("警报点数（Pawn）：未启用死亡警报机制");
                return;
            }

            //非人类不提供点数
            if (!pawn.RaceProps.Humanlike)
            {
                //Log.Message("警报点数（Pawn）：非人类死亡不提供点数");
                return;
            }

            //如果是无派系人就和中立派系死亡相同
            if (pawn.Faction == null)
            {
                //Log.Message($"警报点数（Pawn）：无派系人类死亡，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_NeturalFactionDie}");
                Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_NeturalFactionDie;
                return;
            }

            //如果是玩家派系死亡
            if (pawn.Faction.IsPlayer)
            {
                //Log.Message($"警报点数（Pawn）：玩家派系人类死亡，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_PlayerFactionDie}");
                Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_PlayerFactionDie;
                return;
            }

            //如果是盟友和中立派系死亡
            if (!pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                if (pawn.Faction.PlayerRelationKind == FactionRelationKind.Ally)
                {
                    //Log.Message($"警报点数（Pawn）：盟友派系人类死亡，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_AllyFactionDie}");
                    Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_AllyFactionDie;
                }
                else
                {
                    //Log.Message($"警报点数（Pawn）：中立派系人类死亡，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_NeturalFactionDie}");
                    Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_NeturalFactionDie;
                }
            }
        }
    }
}