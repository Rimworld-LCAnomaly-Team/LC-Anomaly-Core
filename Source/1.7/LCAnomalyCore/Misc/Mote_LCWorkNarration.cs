using UnityEngine;
using Verse;

namespace LCAnomalyCore.Misc
{
    /// <summary>
    /// 收容工作期间浮现于收容室内的脑叶风格旁白/观察飘字 Mote。
    /// </summary>
    public class Mote_LCWorkNarration : MoteThrown
    {
        public string narrationText;
        public Color accentColor = new Color(0.38f, 0.84f, 0.96f);

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }

        public override void DrawGUIOverlay()
        {
            if (Event.current == null
                || Event.current.type != EventType.Repaint
                || Find.Camera == null
                || narrationText.NullOrEmpty())
            {
                return;
            }

            float cellPx = (Find.CameraDriver != null && Find.CameraDriver.CellSizePixels > 0.1f)
                ? Find.CameraDriver.CellSizePixels
                : 42f;
            if (cellPx < 18f)
            {
                return;
            }

            float age = AgeSecs;
            float fadeIn = def?.mote?.fadeInTime ?? 0.22f;
            float solid = def?.mote?.solidTime ?? 3.0f;
            float fadeOut = def?.mote?.fadeOutTime ?? 0.75f;

            float alpha = 1f;
            if (fadeIn > 0f && age < fadeIn)
            {
                alpha = Mathf.Clamp01(age / fadeIn);
            }
            else if (age > fadeIn + solid && fadeOut > 0f)
            {
                alpha = Mathf.Clamp01(1f - (age - fadeIn - solid) / fadeOut);
            }

            if (alpha <= 0.01f)
            {
                return;
            }

            Vector3 world3 = new Vector3(exactPosition.x, 0f, exactPosition.z);
            Vector3 screenPt3 = Find.Camera.WorldToScreenPoint(world3);
            if (screenPt3.z <= 0f)
            {
                return;
            }

            Vector2 screenPos = new Vector2(screenPt3.x, screenPt3.y) / Prefs.UIScale;
            screenPos.y = Verse.UI.screenHeight - screenPos.y;
            if (screenPos.x < -220f || screenPos.x > Verse.UI.screenWidth + 220f
                || screenPos.y < -100f || screenPos.y > Verse.UI.screenHeight + 100f)
            {
                return;
            }

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                float scale = Mathf.Clamp(cellPx / 42f, 0.78f, 1.0f);
                float maxTextW = Mathf.Clamp(220f * scale, 150f, 236f);

                Text.Font = GameFont.Tiny;
                Vector2 singleSize = Text.CalcSize(narrationText);
                float innerW = Mathf.Min(maxTextW, Mathf.Max(84f, Mathf.Ceil(singleSize.x + 2f)));
                float textH = Mathf.Max(16f, Mathf.Ceil(Text.CalcHeight(narrationText, innerW)));

                float boxW = innerW + 16f;
                float boxH = textH + 8f;
                Rect boxRect = new Rect(screenPos.x - boxW * 0.5f, screenPos.y - boxH * 0.5f, boxW, boxH);

                Widgets.DrawBoxSolid(boxRect, new Color(0.04f, 0.05f, 0.07f, 0.86f * alpha));
                GUI.color = new Color(0.25f, 0.29f, 0.35f, 0.90f * alpha);
                Widgets.DrawBox(boxRect, 1);
                Widgets.DrawBoxSolid(new Rect(boxRect.x + 1f, boxRect.y + 2f, 3f, Mathf.Max(6f, boxRect.height - 4f)), new Color(accentColor.r, accentColor.g, accentColor.b, alpha));

                Text.Anchor = TextAnchor.MiddleLeft;
                Rect textRect = new Rect(boxRect.x + 8f, boxRect.y + 4f, innerW, textH);
                GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
                Widgets.Label(new Rect(textRect.x + 1f, textRect.y + 1f, textRect.width, textRect.height), narrationText);
                GUI.color = new Color(0.95f, 0.96f, 0.98f, alpha);
                Widgets.Label(textRect, narrationText);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }
    }
}
