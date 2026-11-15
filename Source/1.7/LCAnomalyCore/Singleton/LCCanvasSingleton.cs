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
        private int ticksRemaining;
        private EWarningLevel level;
        private string message;

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

        public void Draw()
        {
            if (ticksRemaining <= 0 || Current.ProgramState != ProgramState.Playing)
                return;
            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            try
            {
                float alpha = Mathf.PingPong((1800 - ticksRemaining) * 0.01f, 1f);
                Color tint = level == EWarningLevel.First ? Color.yellow :
                    level == EWarningLevel.Second ? new Color(1f, 0.5f, 0f) : Color.red;
                GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
                if (message != null)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(0f, Verse.UI.screenHeight * 0.35f, Verse.UI.screenWidth, 160f), message);
                }
                else
                {
                    string suffix = level == EWarningLevel.First ? "First" :
                        level == EWarningLevel.Second ? "Second" : "Third";
                    Texture2D texture = ContentFinder<Texture2D>.Get("UI/WarningUI/WarningUI_" + suffix);
                    GUI.DrawTexture(new Rect(0f, 0f, Verse.UI.screenWidth, Verse.UI.screenHeight), texture, ScaleMode.ScaleToFit);
                }
            }
            finally
            {
                GUI.color = previousColor;
                Text.Anchor = previousAnchor;
            }
        }
    }
}