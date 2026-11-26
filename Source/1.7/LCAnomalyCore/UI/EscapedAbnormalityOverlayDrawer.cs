using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Defs;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Patch;
using LCAnomalyCore.Util;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 突破收容/出逃异想体脚下的原版脑叶风格状态条（危险等级 + 名称 + 血量底/血条），支持按缩放分档自适应。
    /// </summary>
    public static class EscapedAbnormalityOverlayDrawer
    {
        private static readonly Color DefaultRiskColor = new Color(0.85f, 0.82f, 0.55f);
        private static readonly Color HpFillColor = new Color(0.86f, 0.16f, 0.18f);
        private static readonly Color HpSheenColor = new Color(0.96f, 0.40f, 0.40f);
        private static readonly Color HpWellColor = new Color(0.16f, 0.07f, 0.08f, 0.92f);

        /// <summary>判断指定 Pawn 当前是否应当在脚下绘制出逃异想体状态条。</summary>
        public static bool ShouldDrawEscapedOverlay(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Map == null)
            {
                return false;
            }

            if (Patch_RoomContainment.IsContainedInRoom(pawn))
            {
                return false;
            }

            if (pawn.TryGetComp<CompAbnormalityTool>() != null)
            {
                return false;
            }

            return Patch_RoomContainment.IsBreachedAbnormality(pawn)
                || pawn is LC_EntityBasePawn
                || (!pawn.RaceProps.Humanlike && pawn.def.GetModExtension<ModExtension_AbnormalityCategory>() != null);
        }

        /// <summary>获取异想体或异常实体的危险等级标签（ZAYIN/TETH/HE/WAW/ALEPH）。</summary>
        public static string GetRiskTag(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            string tag = pawn.TryGetComp<CompAbnormality>()?.AbnormalityCategory?.defName;
            if (!tag.NullOrEmpty())
            {
                return tag;
            }

            tag = pawn.def?.GetModExtension<ModExtension_AbnormalityCategory>()?.abnormalityCategoryDef?.defName;
            if (!tag.NullOrEmpty())
            {
                return tag;
            }

            return DefDatabase<AbnormalityCodexEntryDef>.GetNamedSilentFail(pawn.def?.defName)?.category?.defName;
        }

        /// <summary>按危险等级返回对应的标志色。</summary>
        public static Color GetRiskColor(string riskTag)
        {
            switch (riskTag)
            {
                case "ZAYIN": return new Color(0.25f, 0.85f, 0.25f);
                case "TETH": return new Color(0.28f, 0.65f, 1.0f);
                case "HE": return new Color(0.95f, 0.82f, 0.20f);
                case "WAW": return new Color(0.72f, 0.35f, 0.96f);
                case "ALEPH": return new Color(0.95f, 0.22f, 0.25f);
                default: return DefaultRiskColor;
            }
        }

        /// <summary>获取出逃异想体当前生命百分比（0~1，统一血池实体使用精确剩余血量比例）。</summary>
        public static float GetHealthPercent(Pawn pawn)
        {
            return LCEntityHealthUtility.UsesUnifiedHealthPool(pawn)
                ? LCEntityHealthUtility.GetHealthPercent(pawn)
                : Mathf.Clamp01(pawn?.health?.summaryHealth?.SummaryHealthPercent ?? 1f);
        }

        /// <summary>在 Repaint 阶段为出逃异想体绘制脚下等级+名称+血量底 UI。</summary>
        public static void DrawEscapedOverlay(Pawn pawn)
        {
            if (Event.current == null
                || Event.current.type != EventType.Repaint
                || Find.Camera == null
                || !ShouldDrawEscapedOverlay(pawn))
            {
                return;
            }

            float yOffset = -0.62f - Mathf.Clamp((pawn.BodySize - 1f) * 0.14f, 0f, 0.35f);
            Vector2 anchor = GenMapUI.LabelDrawPosFor(pawn, yOffset);
            if (anchor.x < -140f || anchor.x > Verse.UI.screenWidth + 140f
                || anchor.y < -80f || anchor.y > Verse.UI.screenHeight + 80f)
            {
                return;
            }

            float cellPx = (Find.CameraDriver != null && Find.CameraDriver.CellSizePixels > 0.1f)
                ? Find.CameraDriver.CellSizePixels
                : 42f;

            string riskTag = GetRiskTag(pawn);
            Color riskColor = GetRiskColor(riskTag);
            float hpPct = GetHealthPercent(pawn);
            float hpAlpha = pawn.Downed ? 0.65f : 0.98f;

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                if (cellPx < 14f)
                {
                    // Tier 3 (远景微缩条): 46x7 极简危险色块 + 血量槽
                    float barW = 46f;
                    float barH = 7f;
                    Rect outer = new Rect(anchor.x - barW * 0.5f, anchor.y, barW, barH);
                    Widgets.DrawBoxSolid(outer, new Color(0.07f, 0.08f, 0.11f, 0.90f));
                    Widgets.DrawBoxSolid(new Rect(outer.x + 1f, outer.y + 1f, 7f, barH - 2f), riskColor);

                    Rect track = new Rect(outer.x + 9f, outer.y + 1f, barW - 10f, barH - 2f);
                    Widgets.DrawBoxSolid(track, new Color(0.07f, 0.08f, 0.11f, 0.96f));
                    if (hpPct > 0f)
                    {
                        float fillW = Mathf.Max(2f, Mathf.Round(track.width * hpPct));
                        Widgets.DrawBoxSolid(new Rect(track.x, track.y, Mathf.Min(track.width, fillW), track.height), new Color(HpFillColor.r, HpFillColor.g, HpFillColor.b, hpAlpha));
                    }
                }
                else if (cellPx < 25f)
                {
                    // Tier 2 (中远距紧凑条): 名称行 + 风险色块与血量底
                    float width = Mathf.Round(Mathf.Clamp(cellPx * 4.4f, 76f, 108f));
                    float nameH = 15f;
                    float barH = 8f;

                    Rect nameRect = new Rect(anchor.x - width * 0.5f, anchor.y, width, nameH);
                    Widgets.DrawBoxSolid(nameRect, new Color(0.08f, 0.10f, 0.13f, 0.90f));
                    GUI.color = new Color(0.28f, 0.33f, 0.38f, 0.95f);
                    Widgets.DrawBox(nameRect, 1);
                    Widgets.DrawBoxSolid(new Rect(nameRect.x + 1f, nameRect.y + 2f, 3f, nameH - 4f), riskColor);

                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = Color.Lerp(Color.white, riskColor, 0.18f);
                    Widgets.LabelFit(new Rect(nameRect.x + 6f, nameRect.y, nameRect.width - 9f, nameRect.height), pawn.LabelShortCap);

                    float riskW = 26f;
                    Rect riskRect = new Rect(nameRect.x, nameRect.yMax - 1f, riskW, barH);
                    Widgets.DrawBoxSolid(riskRect, new Color(riskColor.r, riskColor.g, riskColor.b, 0.95f));
                    GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.95f);
                    Widgets.DrawBox(riskRect, 1);

                    Rect hpOuter = new Rect(riskRect.xMax, riskRect.y, width - riskW, barH);
                    Widgets.DrawBoxSolid(hpOuter, new Color(0.07f, 0.08f, 0.11f, 0.95f));
                    GUI.color = Color.Lerp(new Color(0.32f, 0.36f, 0.42f), riskColor, 0.35f);
                    Widgets.DrawBox(hpOuter, 1);

                    Rect hpTrack = hpOuter.ContractedBy(1.5f);
                    Widgets.DrawBoxSolid(hpTrack, HpWellColor);
                    if (hpPct > 0f)
                    {
                        float fillW = Mathf.Max(2f, Mathf.Round(hpTrack.width * hpPct));
                        Widgets.DrawBoxSolid(new Rect(hpTrack.x, hpTrack.y, Mathf.Min(hpTrack.width, fillW), hpTrack.height), new Color(HpFillColor.r, HpFillColor.g, HpFillColor.b, hpAlpha));
                    }
                }
                else
                {
                    // Tier 1 (常规/近景原版脑叶底部条): 名称行 + 危险等级徽章与凹陷血量底
                    float scale = Mathf.Clamp(cellPx / 42f, 0.72f, 1.05f);
                    float totalW = Mathf.Round(Mathf.Clamp(156f * scale, 114f, 168f));
                    float nameH = Mathf.Round(Mathf.Clamp(19f * scale, 16f, 21f));
                    float barRowH = Mathf.Round(Mathf.Clamp(16f * scale, 13f, 17f));

                    Rect nameRect = new Rect(anchor.x - totalW * 0.5f, anchor.y, totalW, nameH);
                    Widgets.DrawBoxSolid(nameRect, new Color(0.07f, 0.09f, 0.12f, 0.92f));
                    GUI.color = new Color(0.30f, 0.35f, 0.42f, 0.95f);
                    Widgets.DrawBox(nameRect, 1);
                    Widgets.DrawBoxSolid(new Rect(nameRect.x + 1f, nameRect.y + 2f, 3f, Mathf.Max(6f, nameH - 4f)), riskColor);

                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = Color.Lerp(Color.white, riskColor, 0.16f);
                    Widgets.LabelFit(new Rect(nameRect.x + 6f, nameRect.y, nameRect.width - 10f, nameRect.height), pawn.LabelShortCap);

                    float riskW = !riskTag.NullOrEmpty() ? Mathf.Round(Mathf.Clamp(44f * scale, 36f, 48f)) : 0f;
                    if (!riskTag.NullOrEmpty())
                    {
                        Rect riskRect = new Rect(nameRect.x, nameRect.yMax - 1f, riskW, barRowH);
                        Widgets.DrawBoxSolid(riskRect, new Color(riskColor.r, riskColor.g, riskColor.b, 0.95f));
                        GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.95f);
                        Widgets.DrawBox(riskRect, 1);

                        bool darkText = riskTag == "ZAYIN" || riskTag == "TETH" || riskTag == "HE";
                        GUI.color = darkText ? new Color(0.06f, 0.07f, 0.09f) : new Color(0.98f, 0.98f, 1.0f);
                        Text.Font = GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.LabelFit(new Rect(riskRect.x + 1f, riskRect.y, riskRect.width - 2f, riskRect.height), riskTag);
                    }

                    Rect hpOuter = new Rect(nameRect.x + riskW, nameRect.yMax - 1f, totalW - riskW, barRowH);
                    Widgets.DrawBoxSolid(hpOuter, new Color(0.09f, 0.10f, 0.13f, 0.95f));
                    GUI.color = Color.Lerp(new Color(0.32f, 0.36f, 0.42f), riskColor, 0.35f);
                    Widgets.DrawBox(hpOuter, 1);

                    Rect hpTrack = hpOuter.ContractedBy(2f);
                    Widgets.DrawBoxSolid(hpTrack, HpWellColor);
                    if (hpPct > 0f)
                    {
                        float fillW = Mathf.Min(hpTrack.width, Mathf.Max(2f, Mathf.Round(hpTrack.width * hpPct)));
                        Rect fillRect = new Rect(hpTrack.x, hpTrack.y, fillW, hpTrack.height);
                        Widgets.DrawBoxSolid(fillRect, new Color(HpFillColor.r, HpFillColor.g, HpFillColor.b, hpAlpha));
                        Widgets.DrawBoxSolid(new Rect(fillRect.x, fillRect.y, fillRect.width, Mathf.Min(2f, fillRect.height)), new Color(HpSheenColor.r, HpSheenColor.g, HpSheenColor.b, 0.55f * hpAlpha));
                    }

                    Color tickColor = new Color(0f, 0f, 0f, 0.28f);
                    Widgets.DrawBoxSolid(new Rect(hpTrack.x + hpTrack.width * 0.25f, hpTrack.y, 1f, hpTrack.height), tickColor);
                    Widgets.DrawBoxSolid(new Rect(hpTrack.x + hpTrack.width * 0.50f, hpTrack.y, 1f, hpTrack.height), tickColor);
                    Widgets.DrawBoxSolid(new Rect(hpTrack.x + hpTrack.width * 0.75f, hpTrack.y, 1f, hpTrack.height), tickColor);

                    if (LCEntityHealthUtility.UsesUnifiedHealthPool(pawn) && hpTrack.width >= 56f && hpTrack.height >= 10f)
                    {
                        float curHp = LCEntityHealthUtility.GetCurrentHealth(pawn);
                        float maxHp = LCEntityHealthUtility.GetMaxHealth(pawn);
                        string hpText = $"{Mathf.CeilToInt(curHp)}/{Mathf.RoundToInt(maxHp)}";
                        Rect textRect = new Rect(hpTrack.x, hpTrack.y - 2f, hpTrack.width, hpTrack.height + 4f);
                        Text.Font = GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = new Color(0f, 0f, 0f, 0.85f);
                        Widgets.Label(new Rect(textRect.x + 1f, textRect.y + 1f, textRect.width, textRect.height), hpText);
                        GUI.color = new Color(0.98f, 0.96f, 0.96f, 0.96f);
                        Widgets.Label(textRect, hpText);
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
