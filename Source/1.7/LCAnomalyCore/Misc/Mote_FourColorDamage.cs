using System.Collections.Generic;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Util;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Misc
{
    /// <summary>四色伤害统一受击指示所需的图标、抗性词条与数字贴图缓存。</summary>
    [StaticConstructorOnStartup]
    internal static class FourColorPopupTextures
    {
        public static readonly Texture2D IconRed = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_DamageIcon_Red", reportFailure: false);
        public static readonly Texture2D IconWhite = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_DamageIcon_White", reportFailure: false);
        public static readonly Texture2D IconBlack = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_DamageIcon_Black", reportFailure: false);
        public static readonly Texture2D IconPale = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_DamageIcon_Pale", reportFailure: false);

        public static readonly Texture2D ResistImmune = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_Resist_Immune", reportFailure: false);
        public static readonly Texture2D ResistResist = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_Resist_Resist", reportFailure: false);
        public static readonly Texture2D ResistEndure = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_Resist_Endure", reportFailure: false);
        public static readonly Texture2D ResistWeak = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_Resist_Weak", reportFailure: false);
        public static readonly Texture2D ResistFatal = ContentFinder<Texture2D>.Get("Fleck/FourColor/LC_Resist_Fatal", reportFailure: false);

        public static readonly Texture2D DigitDot = ContentFinder<Texture2D>.Get("Fleck/FourColor/Digits/LC_Digit_Dot", reportFailure: false);
        private static readonly Texture2D[] Digits = BuildDigits();

        private static Texture2D[] BuildDigits()
        {
            var arr = new Texture2D[10];
            for (int i = 0; i < 10; i++)
            {
                arr[i] = ContentFinder<Texture2D>.Get($"Fleck/FourColor/Digits/LC_Digit_{i}", reportFailure: false);
            }
            return arr;
        }

        public static Texture2D GetIcon(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return IconRed;
                case FourColorDamageType.White: return IconWhite;
                case FourColorDamageType.Black: return IconBlack;
                case FourColorDamageType.Pale: return IconPale;
                default: return null;
            }
        }

        public static Texture2D GetTierTexture(FourColorResistanceTier tier)
        {
            switch (tier)
            {
                case FourColorResistanceTier.Immune: return ResistImmune;
                case FourColorResistanceTier.Resist: return ResistResist;
                case FourColorResistanceTier.Endure: return ResistEndure;
                case FourColorResistanceTier.Weak: return ResistWeak;
                case FourColorResistanceTier.Fatal: return ResistFatal;
                default: return null;
            }
        }

        public static Texture2D GetDigitTexture(char ch)
        {
            if (ch == '.')
            {
                return DigitDot;
            }
            if (ch >= '0' && ch <= '9')
            {
                return Digits[ch - '0'];
            }
            return null;
        }
    }

    /// <summary>
    /// 在屏幕 UI 覆盖层绘制 `[四色图标] [抗性词条] [伤害数字]` 的组合飘字，保证三者风格统一、同步上浮并淡出。
    /// </summary>
    public class Mote_FourColorDamage : MoteThrown
    {
        public FourColorDamageType damageType;
        public FourColorResistanceTier resistanceTier;
        public float damageAmount;
        public bool showIcon = true;
        public bool showNumber = true;

        private const float BaseIconHeight = 22f;
        private const float BaseWordHeight = 17f;
        private const float BaseDigitHeight = 18f;
        private const float IconWordGap = 3f;
        private const float WordDigitGap = 4f;
        private const float DigitKerning = -1f;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }

        public override void DrawGUIOverlay()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || Find.Camera == null)
            {
                return;
            }

            float age = AgeSecs;
            float fadeIn = def?.mote?.fadeInTime ?? 0.06f;
            float solid = def?.mote?.solidTime ?? 0.82f;
            float fadeOut = def?.mote?.fadeOutTime ?? 0.42f;

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

            float popScale = age < 0.09f ? Mathf.Lerp(1.18f, 1.0f, age / 0.09f) : 1.0f;

            Vector3 world3 = new Vector3(exactPosition.x, 0f, exactPosition.z);
            Vector3 screenPt3 = Find.Camera.WorldToScreenPoint(world3);
            if (screenPt3.z <= 0f)
            {
                return;
            }

            Vector2 screenPos = new Vector2(screenPt3.x, screenPt3.y) / Prefs.UIScale;
            screenPos.y = (float)Verse.UI.screenHeight - screenPos.y;
            if (screenPos.x < -120f || screenPos.x > Verse.UI.screenWidth + 120f || screenPos.y < -80f || screenPos.y > Verse.UI.screenHeight + 80f)
            {
                return;
            }

            Texture2D iconTex = showIcon ? FourColorPopupTextures.GetIcon(damageType) : null;
            Texture2D wordTex = FourColorPopupTextures.GetTierTexture(resistanceTier);
            string numText = showNumber ? FormatDamageNumber() : null;

            float iconH = BaseIconHeight * popScale;
            float wordH = BaseWordHeight * popScale;
            float digitH = BaseDigitHeight * popScale;

            float totalW = 0f;
            float iconW = 0f;
            if (iconTex != null)
            {
                iconW = iconH * iconTex.width / Mathf.Max(1f, iconTex.height);
                totalW += iconW;
            }

            float wordW = 0f;
            if (wordTex != null)
            {
                wordW = wordH * wordTex.width / Mathf.Max(1f, wordTex.height);
                if (totalW > 0f) totalW += IconWordGap * popScale;
                totalW += wordW;
            }

            float numW = 0f;
            bool allDigitsHaveTextures = false;
            List<Texture2D> digitTexList = null;
            if (!numText.NullOrEmpty())
            {
                digitTexList = new List<Texture2D>(numText.Length);
                allDigitsHaveTextures = true;
                for (int i = 0; i < numText.Length; i++)
                {
                    Texture2D dt = FourColorPopupTextures.GetDigitTexture(numText[i]);
                    if (dt == null)
                    {
                        allDigitsHaveTextures = false;
                        break;
                    }
                    digitTexList.Add(dt);
                    float dw = digitH * dt.width / Mathf.Max(1f, dt.height);
                    numW += dw;
                    if (i > 0) numW += DigitKerning * popScale;
                }

                if (!allDigitsHaveTextures)
                {
                    Text.Font = GameFont.Small;
                    numW = Text.CalcSize(numText).x;
                }

                if (totalW > 0f) totalW += (wordTex != null ? WordDigitGap : IconWordGap) * popScale;
                totalW += numW;
            }

            if (totalW <= 0f)
            {
                return;
            }

            Color prevColor = GUI.color;
            Color tint = GetBannerTint(damageType, alpha);
            try
            {
                float cursorX = screenPos.x - totalW * 0.5f;

                if (iconTex != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.DrawTexture(new Rect(cursorX, screenPos.y - iconH * 0.5f, iconW, iconH), iconTex);
                    cursorX += iconW;
                    if (wordTex != null || !numText.NullOrEmpty())
                    {
                        cursorX += IconWordGap * popScale;
                    }
                }

                if (wordTex != null)
                {
                    GUI.color = tint;
                    GUI.DrawTexture(new Rect(cursorX, screenPos.y - wordH * 0.5f, wordW, wordH), wordTex);
                    cursorX += wordW;
                    if (!numText.NullOrEmpty())
                    {
                        cursorX += WordDigitGap * popScale;
                    }
                }

                if (!numText.NullOrEmpty())
                {
                    if (allDigitsHaveTextures && digitTexList != null)
                    {
                        GUI.color = tint;
                        for (int i = 0; i < digitTexList.Count; i++)
                        {
                            Texture2D dt = digitTexList[i];
                            float dw = digitH * dt.width / Mathf.Max(1f, dt.height);
                            GUI.DrawTexture(new Rect(cursorX, screenPos.y - digitH * 0.5f, dw, digitH), dt);
                            cursorX += dw + DigitKerning * popScale;
                        }
                    }
                    else
                    {
                        Text.Font = GameFont.Small;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Rect textRect = new Rect(cursorX, screenPos.y - 12f, numW + 8f, 24f);
                        GUI.color = new Color(0f, 0f, 0f, alpha * 0.9f);
                        Widgets.Label(new Rect(textRect.x + 1f, textRect.y + 1f, textRect.width, textRect.height), numText);
                        GUI.color = tint;
                        Widgets.Label(textRect, numText);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private string FormatDamageNumber()
        {
            if (resistanceTier == FourColorResistanceTier.Immune || damageAmount <= 0f)
            {
                return "0";
            }

            if (damageAmount >= 10f)
            {
                return Mathf.RoundToInt(damageAmount).ToString();
            }

            return damageAmount.ToString("0.#");
        }

        private static Color GetBannerTint(FourColorDamageType type, float alpha)
        {
            switch (type)
            {
                case FourColorDamageType.Red:
                    return new Color(0.92f, 0.22f, 0.26f, alpha);
                case FourColorDamageType.White:
                    return new Color(0.98f, 0.95f, 0.78f, alpha);
                case FourColorDamageType.Black:
                    return new Color(0.75f, 0.36f, 0.92f, alpha);
                case FourColorDamageType.Pale:
                    return new Color(0.32f, 0.90f, 0.86f, alpha);
                default:
                    return new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
