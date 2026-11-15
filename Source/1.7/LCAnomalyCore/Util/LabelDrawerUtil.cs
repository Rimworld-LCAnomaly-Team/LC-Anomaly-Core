using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>表示 <c>LabelDrawerUtil</c> 类型。</summary>
    public static class LabelDrawerUtil
    {
        /// <summary>执行 <c>DrawLabels</c> 定义的操作。</summary>
        public static void DrawLabels(Pawn colonist, Vector2 pos, ColonistBar bar, Rect rect, float truncateToWidth = 9999f)
        {
            var comp = colonist.GetComp<CompPawnStatus>();
            if (comp != null && comp.Enabled && comp.AssignedCore != null)
            {
                Vector2 vector = new Vector2(0f
                    , Text.LineHeightOf(0) + Setting_LCAnomalyCore_Main.Settings.DepartmentLabel_ColonistBar_VerticalOffsetPerLine);
                pos += vector;

                var department = comp.AssignedCore.CompDepartmentCore.Props.departmentType;
                DrawCustomLabel(pos, department.ToString().Translate(), ColorUtil.TeamStyleColorDict[department]);
            }
        }

        /// <summary>执行 <c>DrawCustomLabel</c> 定义的操作。</summary>
        public static void DrawCustomLabel(Vector2 pos, string labelToDraw, Color labelColor)
        {
            bool tempDrawBG = true;
            float tempLabelAlpha = 0.9f;

            float x = Text.CalcSize(labelToDraw).x;
            Rect labelRect = GetLabelRect(pos, x);
            Rect labelBGRect = GetLabelBGRect(pos, x);
            if (tempDrawBG)
            {
                GUI.DrawTexture(labelBGRect, TexUI.GrayTextBG);
            }
            labelColor.a = tempLabelAlpha;
            Widgets.Label(labelRect, ColoredText.Colorize(labelToDraw, labelColor));
        }

        private static Rect GetLabelRect(Vector2 pos, float labelWidth)
        {
            Rect labelBGRect = GetLabelBGRect(pos, labelWidth);
            return new Rect(labelBGRect.center.x - labelWidth / 2f, labelBGRect.y - 2f, labelWidth, 100f);
        }

        private static Rect GetLabelBGRect(Vector2 pos, float labelWidth)
        {
            return new Rect(pos.x - labelWidth / 2f - 4f, pos.y, labelWidth + 8f, 12f);
        }
    }
}