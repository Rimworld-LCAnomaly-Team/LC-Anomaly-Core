using System.Collections.Generic;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Settings;
using LCAnomalyCore.Util;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 屏幕左上角主管战术子弹快捷施法栏：免切选部门核心直接点选瞄准施放。
    /// </summary>
    public static class ManagerBulletHudDrawer
    {
        private static bool collapsed;
        private static readonly List<EManagerBulletType> tmpUnlockedBullets = new List<EManagerBulletType>(8);

        /// <summary>绘制主管战术子弹快捷施法 HUD。</summary>
        public static void DrawOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null
                || Find.ScreenshotModeHandler == null
                || Find.ScreenshotModeHandler.Active)
            {
                return;
            }

            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            if (settings == null || !settings.If_ShowManagerBulletHud)
            {
                return;
            }

            Map map = Find.CurrentMap;
            int maxCharges = ManagerBulletUtility.GetMaxBulletCharges(map);
            if (maxCharges <= 0)
            {
                return;
            }

            tmpUnlockedBullets.Clear();
            EManagerBulletType[] ordered = ManagerBulletUtility.OrderedBulletTypes;
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ManagerBulletUtility.IsBulletUnlocked(map, ordered[i]))
                {
                    tmpUnlockedBullets.Add(ordered[i]);
                }
            }

            if (tmpUnlockedBullets.Count == 0)
            {
                return;
            }

            GameComponent_LC lc = GameComponent_LC.Instance;
            if (lc == null)
            {
                return;
            }

            int charges = lc.GetManagerBulletCharges(map);
            bool meltdownVisible = map.GetComponent<MapComponent_LCQliphothMeltdown>()?.IsHudVisible == true;

            const float panelX = 166f;
            float panelY = meltdownVisible ? 48f : 8f;
            const float panelW = 216f;
            const float headerH = 24f;
            const float pad = 4f;
            const int cols = 4;
            const float btnW = 48f;
            const float btnH = 28f;
            const float gap = 4f;

            int rows = collapsed ? 0 : Mathf.CeilToInt((float)tmpUnlockedBullets.Count / cols);
            float bodyH = rows > 0 ? pad * 2f + rows * btnH + (rows - 1) * gap : 0f;
            Rect panelRect = new Rect(panelX, panelY, panelW, headerH + bodyH);

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Widgets.DrawBoxSolid(panelRect, new Color(0.06f, 0.08f, 0.11f, 0.88f));
                GUI.color = new Color(0.24f, 0.34f, 0.42f, 0.94f);
                Widgets.DrawBox(panelRect, 1);

                // 标题栏
                Widgets.DrawBoxSolid(new Rect(panelRect.x + 1f, panelRect.y + 2f, 4f, headerH - 4f), new Color(0.32f, 0.85f, 0.98f));

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.84f, 0.94f, 1.0f);
                Rect titleRect = new Rect(panelRect.x + 9f, panelRect.y + 1f, 118f, headerH - 4f);
                Widgets.LabelFit(titleRect, "LC_Bullet_HudTitle".Translate().ToString());

                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = charges > 0 ? new Color(0.45f, 0.95f, 0.68f) : new Color(0.95f, 0.36f, 0.36f);
                Rect chargeRect = new Rect(panelRect.xMax - 76f, panelRect.y + 1f, 50f, headerH - 4f);
                Widgets.LabelFit(chargeRect, $"{charges}/{maxCharges}");

                Rect toggleRect = new Rect(panelRect.xMax - 22f, panelRect.y + 2f, 18f, headerH - 6f);
                GUI.color = new Color(0.80f, 0.86f, 0.92f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(toggleRect, collapsed ? "▶" : "▼");
                if (Widgets.ButtonInvisible(toggleRect))
                {
                    collapsed = !collapsed;
                    RimWorld.SoundDefOf.Click.PlayOneShotOnCamera();
                }

                // 充能进度细条
                Rect progBgRect = new Rect(panelRect.x + 1f, panelRect.y + headerH - 3f, panelRect.width - 2f, 3f);
                Widgets.DrawBoxSolid(progBgRect, new Color(0.12f, 0.16f, 0.20f, 0.95f));
                float progress = lc.GetManagerBulletRechargeProgress(map);
                if (progress > 0f)
                {
                    Widgets.DrawBoxSolid(
                        new Rect(progBgRect.x, progBgRect.y, progBgRect.width * progress, progBgRect.height),
                        charges >= maxCharges ? new Color(0.38f, 0.90f, 0.56f, 0.95f) : new Color(0.32f, 0.82f, 0.98f, 0.95f));
                }

                Rect headerTipRect = new Rect(panelRect.x, panelRect.y, panelRect.width - 24f, headerH);
                string headerTip = charges >= maxCharges
                    ? "LC_Bullet_HudFullTip".Translate(charges, maxCharges).ToString()
                    : "LC_Bullet_HudRechargeTip".Translate(
                        charges,
                        maxCharges,
                        Mathf.CeilToInt(lc.GetManagerBulletRechargeSecondsRemaining(map))).ToString();
                TooltipHandler.TipRegion(headerTipRect, headerTip);

                if (collapsed)
                {
                    return;
                }

                bool isTargetingActive = Find.Targeter != null && Find.Targeter.IsTargeting;
                EManagerBulletType? activeBullet = isTargetingActive ? ManagerBulletUtility.ActiveTargetingBullet : null;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 8.0f);

                for (int i = 0; i < tmpUnlockedBullets.Count; i++)
                {
                    EManagerBulletType bulletType = tmpUnlockedBullets[i];
                    int col = i % cols;
                    int row = i / cols;
                    Rect btnRect = new Rect(
                        panelRect.x + pad + col * (btnW + gap),
                        panelRect.y + headerH + pad + row * (btnH + gap),
                        btnW,
                        btnH);

                    bool selected = activeBullet == bulletType;
                    Color accent = ManagerBulletUtility.GetBulletAccentColor(bulletType);
                    Color cellBg = selected
                        ? Color.Lerp(new Color(0.16f, 0.24f, 0.30f, 0.96f), new Color(0.24f, 0.36f, 0.44f, 0.98f), pulse)
                        : (Mouse.IsOver(btnRect) ? new Color(0.14f, 0.19f, 0.25f, 0.95f) : new Color(0.09f, 0.12f, 0.16f, 0.92f));

                    Widgets.DrawBoxSolid(btnRect, cellBg);
                    Widgets.DrawBoxSolid(new Rect(btnRect.x + 1f, btnRect.yMax - 3f, btnRect.width - 2f, 2f), charges > 0 ? accent : accent * 0.45f);
                    GUI.color = selected
                        ? Color.Lerp(accent, Color.white, pulse * 0.45f)
                        : new Color(0.24f, 0.30f, 0.36f, 0.85f);
                    Widgets.DrawBox(btnRect, 1);

                    Texture2D iconTex = ContentFinder<Texture2D>.Get(ManagerBulletUtility.GetBulletIconPath(bulletType), reportFailure: false) ?? BaseContent.BadTex;
                    float iconSize = 20f;
                    Rect iconRect = new Rect(btnRect.center.x - iconSize * 0.5f, btnRect.y + 2f, iconSize, iconSize);
                    GUI.color = charges > 0 ? Color.white : new Color(1f, 1f, 1f, 0.40f);
                    GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit);

                    string keySuffix = bulletType.ToString();
                    string tip = ("LC_Bullet_" + keySuffix + "_Label").Translate() + "\n"
                        + ("LC_Bullet_" + keySuffix + "_Desc").Translate(charges, maxCharges);
                    TooltipHandler.TipRegion(btnRect, tip);

                    if (Widgets.ButtonInvisible(btnRect))
                    {
                        ManagerBulletUtility.BeginBulletTargeting(map, bulletType);
                    }
                }
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
