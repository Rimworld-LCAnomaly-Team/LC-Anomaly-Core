using LCAnomalyCore.GameComponent;
using UnityEngine;
using Verse;
using static LCAnomalyCore.Util.MusicUtils;

namespace LCAnomalyCore.Singleton
{
    // Waylith 1.7 renders overlays with the game's GUI instead of Unity UGUI.
    public class LCCanvasSingleton
    {
        public static LCCanvasSingleton Instance => Current.Game.GetComponent<GameComponent_LC>().WarningOverlay;

        private Texture2D texFirst;
        private Texture2D texSecond;
        private Texture2D texThird;

        private int ticksRemaining;
        private EWarningLevel level;
        private string message;

        public int ScreenDrawCount { get; private set; }
        public int WindowDrawCount { get; private set; }
        public bool IsActive => ticksRemaining > 0 && Current.ProgramState == ProgramState.Playing && Current.Game != null;

        public void ShowWarningUI(EWarningLevel warningLevel)
        {
            level = warningLevel;
            message = null;
            ticksRemaining = 1800;
        }

        public void ShowText(string text)
        {
            message = text;
            ticksRemaining = 1800;
        }

        public void Tick()
        {
            if (ticksRemaining > 0 && --ticksRemaining == 0)
                message = null;
        }

        public void Draw() => DrawAt(Vector2.zero);

        public void DrawAt(Vector2 originOffset) => DrawAtInternal(originOffset, isWindow: false);

        public void DrawInWindow(Rect windowRect)
        {
            if (!IsActive || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            if (message == null && !IntersectsCornerAlertRegion(windowRect))
                return;

            DrawAtInternal(windowRect.position, isWindow: true);
        }

        private void DrawAtInternal(Vector2 originOffset, bool isWindow)
        {
            if (!IsActive || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            if (Find.UIRoot?.screenshotMode?.Active == true)
                return;

            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            GameFont previousFont = Text.Font;
            try
            {
                float alpha = 0.25f + 0.75f * Mathf.PingPong((1800 - ticksRemaining) * 0.01f, 1f);
                Color tint = level == EWarningLevel.First ? Color.yellow :
                    level == EWarningLevel.Second ? new Color(1f, 0.5f, 0f) : Color.red;
                GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
                if (message != null)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(-originOffset.x, Verse.UI.screenHeight * 0.35f - originOffset.y, Verse.UI.screenWidth, 160f), message);
                    if (isWindow) WindowDrawCount++; else ScreenDrawCount++;
                }
                else
                {
                    Texture2D texture = GetWarningTexture(level);
                    if (texture != null)
                    {
                        GUI.DrawTexture(
                            new Rect(-originOffset.x, -originOffset.y, Verse.UI.screenWidth, Verse.UI.screenHeight),
                            texture,
                            ScaleMode.StretchToFill);
                        if (isWindow) WindowDrawCount++; else ScreenDrawCount++;
                    }
                }
            }
            finally
            {
                GUI.color = previousColor;
                Text.Anchor = previousAnchor;
                Text.Font = previousFont;
            }
        }

        private Texture2D GetWarningTexture(EWarningLevel warningLevel)
        {
            switch (warningLevel)
            {
                case EWarningLevel.First:
                    return texFirst ??= ContentFinder<Texture2D>.Get("UI/WarningUI/WarningUI_First", false);
                case EWarningLevel.Second:
                    return texSecond ??= ContentFinder<Texture2D>.Get("UI/WarningUI/WarningUI_Second", false);
                default:
                    return texThird ??= ContentFinder<Texture2D>.Get("UI/WarningUI/WarningUI_Third", false);
            }
        }

        private static bool IntersectsCornerAlertRegion(Rect windowRect)
        {
            float sw = Verse.UI.screenWidth;
            float sh = Verse.UI.screenHeight;
            float cornerW = sw * 0.20f;
            float cornerH = sh * 0.28f;

            return windowRect.Overlaps(new Rect(0f, 0f, cornerW, cornerH))
                || windowRect.Overlaps(new Rect(sw - cornerW, 0f, cornerW, cornerH))
                || windowRect.Overlaps(new Rect(0f, sh - cornerH, cornerW, cornerH))
                || windowRect.Overlaps(new Rect(sw - cornerW, sh - cornerH, cornerW, cornerH));
        }
    }
}
