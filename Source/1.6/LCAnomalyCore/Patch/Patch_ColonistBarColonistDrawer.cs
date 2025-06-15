using HarmonyLib;
using LCAnomalyCore.Settings;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Patch
{
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawColonist))]
    public class Patch_ColonistBarColonistDrawer
    {
        private static void Postfix(Rect rect, Pawn colonist, Map pawnMap, bool highlight, bool reordering)
        {
            if (Setting_LCAnomalyCore_Main.Settings.If_ShowDepartmentLabel_ColonistBar)
            {
                var colonistBar = Find.ColonistBar;
                float num = 4 * colonistBar.Scale;

                Vector2 pos = new Vector2(rect.center.x
                    , rect.yMax - num + Setting_LCAnomalyCore_Main.Settings.DepartmentLabel_ColonistBar_VerticalOffset);

                if (colonist == null)
                    return;

                LabelDrawerUtil.DrawLabels(colonist, pos, colonistBar, rect, rect.width + colonistBar.SpaceBetweenColonistsHorizontal);
            }
        }
    }
}