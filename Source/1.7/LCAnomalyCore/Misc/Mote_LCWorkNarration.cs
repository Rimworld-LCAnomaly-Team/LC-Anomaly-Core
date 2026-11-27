using UnityEngine;
using Verse;

namespace LCAnomalyCore.Misc
{
    /// <summary>
    /// 收容工作期间浮现于收容室内的脑叶风格旁白/观察飘字 Mote。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Mote_LCWorkNarration : MoteThrown
    {
        private static readonly Texture2D TexNarrationBannerFrame = ContentFinder<Texture2D>.Get("UI/ContainmentRoom/NarrationBannerFrame", false);

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
                float scale = Mathf.Clamp(cellPx / 42f, 0.78f, 1.50f);
                float maxTextW = Mathf.Clamp(220f * scale, 150f, 340f);

                Text.Font = cellPx >= 64f ? GameFont.Small : GameFont.Tiny;
                Vector2 singleSize = Text.CalcSize(narrationText);
                float innerW = Mathf.Min(maxTextW, Mathf.Max(84f, Mathf.Ceil(singleSize.x + 2f)));
                float textH = Mathf.Max(16f, Mathf.Ceil(Text.CalcHeight(narrationText, innerW)));

                float padScale = Mathf.Min(scale, 1.25f);
                float padX = Mathf.Round(16f * padScale);
                float padY = Mathf.Round(8f * padScale);
                float boxW = innerW + padX;
                float boxH = textH + padY;
                Rect boxRect = new Rect(screenPos.x - boxW * 0.5f, screenPos.y - boxH * 0.5f, boxW, boxH);

                GUI.color = new Color(1f, 1f, 1f, alpha);
                if (TexNarrationBannerFrame != null)
                {
                    GUI.DrawTexture(boxRect, TexNarrationBannerFrame);
                }
                else
                {
                    GUI.color = Color.white;
                    Widgets.DrawBoxSolid(boxRect, new Color(0.027f, 0.027f, 0.035f, alpha));
                    GUI.color = new Color(0.937f, 0.898f, 0.541f, alpha);
                    Widgets.DrawBox(boxRect, 1);
                }
                GUI.color = Color.white;
                // Short stepped slash follows the banner's chamfered left end.
                float accentH = Mathf.Max(6f, boxRect.height - 10f);
                Color slashColor = new Color(accentColor.r, accentColor.g, accentColor.b, alpha);
                Widgets.DrawBoxSolid(new Rect(boxRect.x + 4f, boxRect.y + 5f, 2f, accentH * 0.5f), slashColor);
                Widgets.DrawBoxSolid(new Rect(boxRect.x + 3f, boxRect.y + 5f + accentH * 0.5f, 2f, accentH * 0.5f), slashColor);

                Text.Anchor = TextAnchor.MiddleLeft;
                Rect textRect = new Rect(boxRect.x + padX * 0.5f, boxRect.y + padY * 0.5f, innerW, textH);
                GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
                Widgets.Label(new Rect(textRect.x + 1f, textRect.y + 1f, textRect.width, textRect.height), narrationText);
                GUI.color = new Color(0.965f, 0.949f, 0.871f, alpha);
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
