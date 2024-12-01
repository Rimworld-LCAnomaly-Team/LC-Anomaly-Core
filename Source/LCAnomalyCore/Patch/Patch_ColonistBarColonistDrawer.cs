using HarmonyLib;
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
            var colonistBar = Find.ColonistBar;
            float num = 4 * colonistBar.Scale;

            float tempVOffset = -1f;

            Vector2 pos = new Vector2(rect.center.x, rect.yMax - num + tempVOffset);

            if (colonist == null)
                return;

            LabelDrawerUtil.DrawLabels(colonist, pos, colonistBar, rect, rect.width + colonistBar.SpaceBetweenColonistsHorizontal);
        }
    }
}