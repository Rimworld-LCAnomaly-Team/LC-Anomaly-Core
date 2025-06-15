using HarmonyLib;
using LCAnomalyCore.Settings;
using LCAnomalyCore.Util;
using RimWorld;
using System.Reflection;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 关于MentalStateHandler的补丁（为了提供崩溃警报点数）
    /// </summary>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public class Patch_MentalBreak
    {
        /// <summary>
        /// Postfix
        /// </summary>
        private static void Postfix(MentalStateHandler __instance, bool __result, MentalStateDef stateDef, string reason = null, bool forced = false, bool forceWake = false, bool causedByMood = false, Pawn otherPawn = null, bool transitionSilently = false, bool causedByDamage = false, bool causedByPsycast = false)
        {
            //Log.Warning("MentalStateHandler.TryStartMentalState 注入成功");

            //如果并没有精神崩溃，就不处理
            if (!__result)
            {
                //Log.Message("警报点数（崩溃）：并没有崩溃");
                return;
            }

            //LC组件null直接退出
            if (Components.LC == null)
            {
                //Log.Error("警报点数（崩溃）：GameComponent_LC is null");
                return;
            }

            //如果未启用警报机制，就不处理
            if (!Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarning)
            {
                //Log.Warning("警报点数（崩溃）：未启用警报机制");
                return;
            }

            //如果未启用精神崩溃警报机制，就不处理
            if (!Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarningMentalBreak)
            {
                //Log.Warning("警报点数（崩溃）：未启用崩溃警报机制");
                return;
            }

            //反射获取Pawn
            FieldInfo field = typeof(MentalStateHandler).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
            Pawn pawn = (Pawn)field.GetValue(__instance);
            if (pawn == null)
            {
                //Log.Error("警报点数（崩溃）：Pawn is null");
                return;
            }

            //非人类不提供点数
            if (!pawn.RaceProps.Humanlike)
            {
                //Log.Message("警报点数（崩溃）：非人类不提供点数");
                return;
            }

            //如果是无派系人就和中立派系相同
            if (pawn.Faction == null)
            {
                //Log.Message($"警报点数（崩溃）：无派系人类，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_NeturalFactionMentalBreak}");
                Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_NeturalFactionMentalBreak;
                return;
            }

            //如果是玩家派系
            if (pawn.Faction.IsPlayer)
            {
                //Log.Message($"警报点数（崩溃）：玩家派系人类，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_PlayerFactionMentalBreak}");
                Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_PlayerFactionMentalBreak;
                return;
            }

            //如果是盟友和中立派系
            if (!pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                if (pawn.Faction.PlayerRelationKind == FactionRelationKind.Ally)
                {
                    //Log.Message($"警报点数（崩溃）：盟友派系人类，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_AllyFactionMentalBreak}");
                    Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_AllyFactionMentalBreak;
                }
                else
                {
                    //Log.Message($"警报点数（崩溃）：中立派系人类，点数 +{Setting_LCAnomalyLibrary_Main.Settings.PointsOfWarning_NeturalFactionMentalBreak}");
                    Components.LC.CurWarningPoints += Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_NeturalFactionMentalBreak;
                }
            }
        }
    }
}