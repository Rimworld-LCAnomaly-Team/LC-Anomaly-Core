using LCAnomalyLibrary.Util;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.UI
{
    public class Dialog_LC_AssignWorkType : Window
    {
        private Building.Building_HoldingPlatform platform;

        private Vector2 scrollPosition;

        private static readonly List<Pawn> tmpPawnSorted = new List<Pawn>(12);

        /// <summary>
        /// 窗体的尺寸
        /// </summary>
        public override Vector2 InitialSize => new Vector2(500f, 550f);

        public Dialog_LC_AssignWorkType(Building.Building_HoldingPlatform platform)
        {
            this.platform = platform;
            doCloseButton = true;
            //doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            /* 上半部分 */

            Rect UpperRect = new Rect(inRect);

            Rect instinctRect = new Rect(UpperRect);
            instinctRect.width = 200;
            instinctRect.height = instinctRect.width;
            var bg1 = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/" + EAnomalyWorkType.Instinct.ToString(), true);
            DrawToolTipRow(instinctRect, ref bg1, EAnomalyWorkType.Instinct);
            if (Widgets.ButtonImageWithBG(instinctRect, bg1))
            {
                platform.CurWorkType = EAnomalyWorkType.Instinct;
                Close();
            }

            Rect insightRect = new Rect(UpperRect);
            insightRect.x = instinctRect.width + instinctRect.x + 65f;
            insightRect.width = 200;
            insightRect.height = instinctRect.width;
            var bg2 = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/" + EAnomalyWorkType.Insight.ToString(), true);
            DrawToolTipRow(insightRect, ref bg2, EAnomalyWorkType.Insight);
            if (Widgets.ButtonImageWithBG(insightRect, bg2))
            {
                platform.CurWorkType = EAnomalyWorkType.Insight;
                Close();
            }

            /* 下半部分 */

            Rect LowerRect = new Rect(inRect);
            LowerRect.y = 200f + 65f;

            Rect attachmentRect = new Rect(LowerRect);
            attachmentRect.width = 200;
            attachmentRect.height = attachmentRect.width;
            var bg3 = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/" + EAnomalyWorkType.Attachment.ToString(), true);
            DrawToolTipRow(attachmentRect, ref bg3, EAnomalyWorkType.Attachment);
            if (Widgets.ButtonImageWithBG(attachmentRect, bg3))
            {
                platform.CurWorkType = EAnomalyWorkType.Attachment;
                Close();
            }

            Rect repressionRect = new Rect(LowerRect);
            repressionRect.x = attachmentRect.width + attachmentRect.x + 65f;
            repressionRect.width = 200;
            repressionRect.height = attachmentRect.width;
            var bg4 = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/" + EAnomalyWorkType.Repression.ToString(), true);
            DrawToolTipRow(repressionRect, ref bg4, EAnomalyWorkType.Repression);
            if (Widgets.ButtonImageWithBG(repressionRect, bg4))
            {
                platform.CurWorkType = EAnomalyWorkType.Repression;
                Close();
            }
        }

        private void DrawToolTipRow(Rect rect, ref Texture2D tex, EAnomalyWorkType type = EAnomalyWorkType.Unknown)
        {
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, "TipDialogWorkType_" + type.ToString());


                //TODO Malkuth核心抑制机制预留
                //tex = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Unknown");
            }
        }
    }
}
