using LCAnomalyLibrary.Util;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.UI
{
    public class Dialog_LC_AssignWorkType : Window
    {
        private Building.Building_HoldingPlatform platform;
        private float buttonSize = 100f;

        /// <summary>
        /// 窗体的尺寸
        /// </summary>
        public override Vector2 InitialSize => new Vector2(260f, 260f);

        public Dialog_LC_AssignWorkType(Building.Building_HoldingPlatform platform)
        {
            this.platform = platform;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            /* 上半部分 */

            Rect UpperRect = new Rect(inRect);

            Rect instinctRect = new Rect(UpperRect);
            instinctRect.width = buttonSize;
            instinctRect.height = buttonSize;
            var bg1 = Util.GraphicUtil.DialogAssignWorkTypeNormalTexture_Get(EAnomalyWorkType.Instinct);
            DrawToolTipRow(instinctRect, ref bg1, EAnomalyWorkType.Instinct);
            if (Widgets.ButtonImage(instinctRect, bg1))
            {
                platform.CurWorkType = EAnomalyWorkType.Instinct;
                Close();
            }

            Rect insightRect = new Rect(UpperRect);
            insightRect.x = instinctRect.width + instinctRect.x + 25f;
            insightRect.width = buttonSize;
            insightRect.height = buttonSize;
            var bg2 = Util.GraphicUtil.DialogAssignWorkTypeNormalTexture_Get(EAnomalyWorkType.Insight);
            DrawToolTipRow(insightRect, ref bg2, EAnomalyWorkType.Insight);
            if (Widgets.ButtonImage(insightRect, bg2))
            {
                platform.CurWorkType = EAnomalyWorkType.Insight;
                Close();
            }

            /* 下半部分 */

            Rect LowerRect = new Rect(inRect);
            LowerRect.y = buttonSize + 25f;

            Rect attachmentRect = new Rect(LowerRect);
            attachmentRect.width = buttonSize;
            attachmentRect.height = buttonSize;
            var bg3 = Util.GraphicUtil.DialogAssignWorkTypeNormalTexture_Get(EAnomalyWorkType.Attachment);
            DrawToolTipRow(attachmentRect, ref bg3, EAnomalyWorkType.Attachment);
            if (Widgets.ButtonImage(attachmentRect, bg3))
            {
                platform.CurWorkType = EAnomalyWorkType.Attachment;
                Close();
            }

            Rect repressionRect = new Rect(LowerRect);
            repressionRect.x = attachmentRect.width + attachmentRect.x + 25f;
            repressionRect.width = buttonSize;
            repressionRect.height = buttonSize;
            var bg4 = Util.GraphicUtil.DialogAssignWorkTypeNormalTexture_Get(EAnomalyWorkType.Repression);
            DrawToolTipRow(repressionRect, ref bg4, EAnomalyWorkType.Repression);
            if (Widgets.ButtonImage(repressionRect, bg4))
            {
                platform.CurWorkType = EAnomalyWorkType.Repression;
                Close();
            }
        }

        private void DrawToolTipRow(Rect rect, ref Texture2D tex, EAnomalyWorkType type = EAnomalyWorkType.Unknown)
        {
            if (Mouse.IsOver(rect))
            {
                tex = Util.GraphicUtil.DialogAssignWorkTypeOnHoverTexture_Get(type);
                //TooltipHandler.TipRegion(rect, ("TipDialogWorkType_" + type.ToString()).Translate());
            }
        }
    }
}
