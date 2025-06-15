using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 脑叶员工属性Tab
    /// </summary>
    public class ITab_PawnStatus : ITab
    {
        /// <summary>
        /// comp
        /// </summary>
        protected CompPawnStatus StatusComp => base.SelPawn.GetComp<CompPawnStatus>();

        /// <summary>
        /// 是否是职员（comp不为空且阵营为殖民者）
        /// </summary>
        protected bool IsEmployee => StatusComp != null && SelPawn.Faction == Faction.OfPlayer;

        /// <summary>
        /// 是否显示在面板上
        /// </summary>
        public override bool IsVisible
        {
            get
            {
                if (IsEmployee)
                    return true;

                return false;
            }
        }

        protected virtual bool DrawThingIcon => true;

        public ITab_PawnStatus()
        {
            size = new Vector2(Mathf.Min(220f, Verse.UI.screenWidth), 180f);
            labelKey = "LC_TabPawnStatusContents";
        }

        /// <summary>
        /// Tab内容绘制
        /// </summary>
        protected override void FillTab()
        {
            if (StatusComp.Enabled)
            {
                Rect rectAvgLevel = new Rect(10f, 20f, size.x, 20f);
                //rectAvgLevel = rectAvgLevel.ContractedBy(10f);

                using (new TextBlock(TextAnchor.UpperLeft))
                {
                    string text = "LC_ITab_PawnStatus_AvgLevel".Translate() + StatusComp.SingleLevel.ToString();
                    Widgets.LabelEllipses(rectAvgLevel, ColoredText.Colorize(text, ColorLibrary.Grey));
                }

                //分割线
                using (new TextBlock(Widgets.SeparatorLineColor))
                    Widgets.DrawLineHorizontal(0f, 40f, size.x);

                DrawStatusTab();
            }
            else
            {
                Rect rectAvgLevel = new Rect(10f, 20f, size.x, 40f);
                using (new TextBlock(TextAnchor.UpperLeft))
                {
                    string text = "LC_ITab_PawnStatus_NonTriggered".Translate() + StatusComp.SingleLevel.ToString();
                    Widgets.LabelEllipses(rectAvgLevel, ColoredText.Colorize(text, ColorLibrary.Red));
                }
            }
        }

        /// <summary>
        /// 具体内容绘制
        /// </summary>
        protected void DrawStatusTab()
        {
            float yOffset = 50;
            float rect_x = 10f;
            float rect_width = 80f;
            float barOffset_x = 30f;
            float rectLevelNumber_x = rect_x + rect_width - 20f;
            float rectBar_x = rect_width + barOffset_x;
            float rectLevelEnum_width = rect_width - 20f;

            //勇气
            Rect rect1 = new Rect(rect_x, yOffset, rectLevelEnum_width, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = "LC_ITab_PawnStatus_Fortitude".Translate() + " " + StatusComp.GetPawnStatusELevel(EPawnStatus.Fortitude).ToString();
                Widgets.Label(rect1, ColoredText.Colorize(text, ColorLibrary.DarkRed));
            }
            Rect rect1_1 = new Rect(rectLevelNumber_x, yOffset, 40f, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = StatusComp.GetPawnStatusLevel(EPawnStatus.Fortitude).Status.ToString();
                Widgets.Label(rect1_1, ColoredText.Colorize(text, ColorLibrary.DarkRed));
            }
            Rect rect1Exp = new Rect(rectBar_x, yOffset, 100f, 20f);
            Widgets.FillableBar(rect1Exp, StatusComp.GetPawnStatusLevel(EPawnStatus.Fortitude).Exp, ColorUtil.RedTex);

            yOffset += 30f;

            //谨慎
            Rect rect2 = new Rect(rect_x, yOffset, rectLevelEnum_width, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = "LC_ITab_PawnStatus_Prudence".Translate() + " " + StatusComp.GetPawnStatusELevel(EPawnStatus.Prudence).ToString();
                Widgets.Label(rect2, ColoredText.Colorize(text, Color.white));
            }
            Rect rect2_1 = new Rect(rectLevelNumber_x, yOffset, 40f, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = StatusComp.GetPawnStatusLevel(EPawnStatus.Prudence).Status.ToString();
                Widgets.Label(rect2_1, ColoredText.Colorize(text, Color.white));
            }
            Rect rect2Exp = new Rect(rectBar_x, yOffset, 100f, 20f);
            Widgets.FillableBar(rect2Exp, StatusComp.GetPawnStatusLevel(EPawnStatus.Prudence).Exp, ColorUtil.WhiteTex);

            yOffset += 30f;

            //自律
            Rect rect3 = new Rect(rect_x, yOffset, rectLevelEnum_width, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = "LC_ITab_PawnStatus_Temperance".Translate() + " " + StatusComp.GetPawnStatusELevel(EPawnStatus.Temperance).ToString();
                Widgets.Label(rect3, ColoredText.Colorize(text, ColorLibrary.Purple));
            }
            Rect rect3_1 = new Rect(rectLevelNumber_x, yOffset, 40f, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = StatusComp.GetPawnStatusLevel(EPawnStatus.Temperance).Status.ToString();
                Widgets.Label(rect3_1, ColoredText.Colorize(text, ColorLibrary.Purple));
            }
            Rect rect3Exp = new Rect(rectBar_x, yOffset, 100f, 20f);
            Widgets.FillableBar(rect3Exp, StatusComp.GetPawnStatusLevel(EPawnStatus.Temperance).Exp, ColorUtil.PurpleTex);

            yOffset += 30f;

            //正义
            Rect rect4 = new Rect(rect_x, yOffset, rectLevelEnum_width, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = "LC_ITab_PawnStatus_Justice".Translate() + " " + StatusComp.GetPawnStatusELevel(EPawnStatus.Justice).ToString();
                Widgets.Label(rect4, ColoredText.Colorize(text, ColorLibrary.Cyan));
            }
            Rect rect4_1 = new Rect(rectLevelNumber_x, yOffset, 40f, 20f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                string text = StatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Status.ToString();
                Widgets.Label(rect4_1, ColoredText.Colorize(text, ColorLibrary.Cyan));
            }
            Rect rect4Exp = new Rect(rectBar_x, yOffset, 100f, 20f);
            Widgets.FillableBar(rect4Exp, StatusComp.GetPawnStatusLevel(EPawnStatus.Justice).Exp, ColorUtil.CyanTex);
        }
    }
}