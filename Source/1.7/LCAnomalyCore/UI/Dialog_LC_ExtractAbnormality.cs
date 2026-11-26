using System;
using LCAnomalyCore.Defs;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.UI
{
    /// <summary>
    /// 脑叶公司原版风格“三选一”异想体提取弹窗。
    /// 还原红菱多边形背景、三轨黑匣悬挂、二次提取机关按钮、选中放大轮廓高亮与摇晃震动动画、
    /// 以及仅在悬停/选中时显现的底部黑墨水红字语录栏。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Dialog_LC_ExtractAbnormality : Window
    {
        private static readonly Texture2D TexBgRed = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_RedPolyBg", false);
        private static readonly Texture2D TexFrameOverlay = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_FrameOverlay", false);
        private static readonly Texture2D TexUnitHighlight = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_Unit_Highlight", false);
        private static readonly Texture2D TexUnitChassis = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_Unit_Chassis", false);
        private static readonly Texture2D TexDonorHighlight = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_Unit_DonorHighlight", false);
        private static readonly Texture2D TexDonorChassis = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_Unit_DonorChassis", false);
        private static readonly Texture2D TexUnitRedLamp = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_Unit_RedLamp", false);
        private static readonly Texture2D TexUnitEkg = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_Unit_EkgLine", false);
        private static readonly Texture2D TexQuoteBanner = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_QuoteBanner", false);
        private static readonly Texture2D TexReExtractBtn = ContentFinder<Texture2D>.Get("UI/Extraction/Extract_ReExtractBtn", false);

        private static readonly Color GoldAccent = new Color(0.96f, 0.82f, 0.36f);
        private static readonly Color CrimsonCard = new Color(0.11f, 0.05f, 0.06f, 0.90f);
        private static readonly Color CrimsonSelected = new Color(0.19f, 0.08f, 0.08f, 0.96f);
        private static readonly Color QuoteCrimsonText = new Color(0.86f, 0.24f, 0.24f);
        private static readonly Color ReExtractRed = new Color(0.92f, 0.28f, 0.22f);
        private static readonly Color CodeInkBlack = new Color(0.06f, 0.05f, 0.05f, 1f);

        private readonly Map map;
        private readonly Building anchorBuilding;
        private LCExtractionTier selectedTier = LCExtractionTier.Standard;
        private string transientError;

        // 基于 Time.realtimeSinceStartup 的实时动画状态（窗口暂停期间依然流畅运行）
        private float lastFrameTime = -1f;
        private float dropInStartTime = -1f;
        private float selectPulseTime = -10f;
        private int lastHoveredIndex = -1;
        private int lastFocusedIndex = -1;
        private float focusFadeStartTime = -10f;
        private readonly float[] slotScale = { 1f, 1f, 1f };

        private bool isRerolling;
        private float rerollStartTime = -1f;
        private bool rerollApplied;

        private bool isConfirming;
        private float confirmStartTime = -1f;
        private bool startedExtractionBgm;

        /// <summary>
        /// 打开异想体三选一提取界面。
        /// </summary>
        /// <param name="map">目标地图。</param>
        /// <param name="anchorBuilding">触发界面的终端建筑（可为空，生成时会定位地图上的可用终端）。</param>
        public Dialog_LC_ExtractAbnormality(Map map, Building anchorBuilding)
        {
            this.map = map ?? Find.CurrentMap;
            this.anchorBuilding = anchorBuilding;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
            dropInStartTime = Time.realtimeSinceStartup;
        }

        /// <inheritdoc />
        public override void PreOpen()
        {
            base.PreOpen();
            if (MapComponent_LCExtraction.GetFor(map)?.HasPendingDraw == true)
            {
                EnsureExtractionBgmPlaying();
                (Defs.SoundDefOf.LC_Sound_Extraction_DropIn ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
            }
        }

        /// <inheritdoc />
        public override void PostClose()
        {
            base.PostClose();
            if (startedExtractionBgm
                && Find.MusicManagerPlay != null
                && Defs.SongDefOf.Anomaly_Extract != null
                && Find.MusicManagerPlay.CurrentSong == Defs.SongDefOf.Anomaly_Extract)
            {
                Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(1.5f, 1.8f);
            }
            startedExtractionBgm = false;
        }

        /// <inheritdoc />
        public override Vector2 InitialSize => new Vector2(1040f, 660f);

        /// <inheritdoc />
        protected override float Margin => 0f;

        /// <inheritdoc />
        public override void DoWindowContents(Rect inRect)
        {
            MapComponent_LCExtraction mgr = MapComponent_LCExtraction.GetFor(map);
            if (mgr == null)
            {
                Widgets.Label(inRect.ContractedBy(24f), "LC_Extract_NoMap".Translate());
                return;
            }

            float now = Time.realtimeSinceStartup;
            float dt = lastFrameTime < 0f ? 0.016f : Mathf.Clamp(now - lastFrameTime, 0.001f, 0.05f);
            lastFrameTime = now;

            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWrap = Text.WordWrap;
            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;

            try
            {
                if (mgr.HasPendingDraw)
                {
                    DrawExtractionChamber(inRect, mgr, now, dt);
                }
                else
                {
                    DrawTierPreparationView(inRect, mgr, now);
                }
            }
            finally
            {
                GUI.matrix = prevMatrix;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                Text.WordWrap = prevWrap;
                GUI.color = prevColor;
            }
        }

        #region Mode A: Depth Tier Preparation

        private void DrawTierPreparationView(Rect inRect, MapComponent_LCExtraction mgr, float now)
        {
            DrawFullBackdrop(inRect, drawRails: false, dimFactor: 0.36f);

            Rect content = inRect.ContractedBy(28f);
            Rect headerRect = new Rect(content.x, content.y, content.width, 62f);
            DrawPrepHeader(headerRect, mgr);

            Rect bodyRect = new Rect(content.x, headerRect.yMax + 10f, content.width, content.height - 62f - 58f - 18f);
            Rect footerRect = new Rect(content.x, content.yMax - 54f, content.width, 54f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.92f, 0.90f, 0.86f);
            Widgets.Label(new Rect(bodyRect.x + 6f, bodyRect.y, bodyRect.width - 12f, 26f), "LC_Extract_SelectTierPrompt".Translate());
            GUI.color = Color.white;

            LCExtractionTier[] tiers =
            {
                LCExtractionTier.Standard,
                LCExtractionTier.Shallow,
                LCExtractionTier.Mid,
                LCExtractionTier.Deep,
                LCExtractionTier.Abyss
            };

            float startY = bodyRect.y + 32f;
            float rowHeight = 76f;
            float gap = 8f;

            for (int i = 0; i < tiers.Length; i++)
            {
                Rect row = new Rect(bodyRect.x + 8f, startY + i * (rowHeight + gap), bodyRect.width - 16f, rowHeight);
                DrawTierRow(row, tiers[i], mgr);
            }

            // 底部控制栏
            Widgets.DrawBoxSolid(footerRect, new Color(0.08f, 0.03f, 0.03f, 0.90f));
            GUI.color = new Color(0.55f, 0.18f, 0.18f, 0.78f);
            Widgets.DrawBox(footerRect, 1);
            GUI.color = Color.white;

            bool canStart = mgr.CanStartExtraction(selectedTier, out string failReason);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect msgRect = new Rect(footerRect.x + 14f, footerRect.y + 6f, footerRect.width - 260f, footerRect.height - 12f);
            if (!transientError.NullOrEmpty())
            {
                GUI.color = new Color(1f, 0.45f, 0.40f);
                Widgets.Label(msgRect, transientError);
            }
            else if (!canStart)
            {
                GUI.color = new Color(1f, 0.62f, 0.35f);
                Widgets.Label(msgRect, failReason);
            }
            else
            {
                GUI.color = new Color(0.84f, 0.86f, 0.82f);
                Widgets.Label(msgRect, "LC_Extract_ReadyHint".Translate());
            }
            GUI.color = Color.white;

            Rect startBtn = new Rect(footerRect.xMax - 230f, footerRect.y + 8f, 214f, 38f);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = canStart;
            if (Widgets.ButtonText(startBtn, "LC_Extract_StartBtn".Translate()))
            {
                transientError = null;
                if (mgr.TryBeginExtraction(selectedTier, out string err))
                {
                    ResetChamberAnimationState(now);
                    EnsureExtractionBgmPlaying();
                    (Defs.SoundDefOf.LC_Sound_Extraction_DropIn
                     ?? Defs.SoundDefOf.LC_Sound_Extraction_Start
                     ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
                }
                else
                {
                    transientError = err;
                }
            }
            GUI.enabled = prevEnabled;
            if (!canStart && !failReason.NullOrEmpty())
            {
                TooltipHandler.TipRegion(startBtn, failReason);
            }
        }

        private void DrawPrepHeader(Rect rect, MapComponent_LCExtraction mgr)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.03f, 0.03f, 0.86f));
            GUI.color = new Color(0.55f, 0.18f, 0.18f, 0.80f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = GoldAccent;
            Widgets.Label(new Rect(rect.x + 14f, rect.y + 6f, 440f, 32f), "LC_Extract_WindowTitle".Translate());
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.82f, 0.80f, 0.78f);
            Widgets.Label(new Rect(rect.x + 14f, rect.y + 36f, 480f, 20f), "LC_Extract_SubtitlePrep".Translate());

            int cogitoCount = mgr.CountAvailableResource(Defs.ThingDefOf.Cogito);
            int enkCount = mgr.CountAvailableResource(Defs.ThingDefOf.EnkephalinBox);

            float badgeX = rect.xMax - 14f;
            float badgeY = rect.y + 16f;

            badgeX -= 156f;
            DrawStatusChip(new Rect(badgeX, badgeY, 156f, 28f), "LC_Extract_EnkStock".Translate(enkCount), new Color(0.32f, 0.85f, 0.92f));

            badgeX -= 142f;
            DrawStatusChip(new Rect(badgeX, badgeY, 134f, 28f), "LC_Extract_CogitoStock".Translate(cogitoCount), GoldAccent);

            if (mgr.IsFreeCharterAvailable)
            {
                badgeX -= 146f;
                DrawStatusChip(new Rect(badgeX, badgeY, 138f, 28f), "LC_Extract_FreeCharterChip".Translate(), new Color(0.45f, 0.94f, 0.45f));
            }
            else if (mgr.CooldownTicksRemaining > 0)
            {
                badgeX -= 156f;
                DrawStatusChip(new Rect(badgeX, badgeY, 148f, 28f),
                    "LC_Extract_CooldownChip".Translate(mgr.CooldownTicksRemaining.ToStringTicksToPeriod()),
                    new Color(0.98f, 0.55f, 0.30f));
            }

            GUI.color = Color.white;
        }

        private void DrawTierRow(Rect rect, LCExtractionTier tier, MapComponent_LCExtraction mgr)
        {
            bool isSelected = selectedTier == tier;
            bool isFree = mgr.IsFreeForTier(tier);
            bool affordable = mgr.CanStartExtraction(tier, out _);

            Widgets.DrawBoxSolid(rect, isSelected ? CrimsonSelected : CrimsonCard);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.06f));
            }

            GUI.color = isSelected ? GoldAccent : new Color(0.45f, 0.18f, 0.18f, 0.85f);
            Widgets.DrawBox(rect, isSelected ? 2 : 1);
            GUI.color = Color.white;

            Widgets.DrawBoxSolid(new Rect(rect.x + 2f, rect.y + 2f, 7f, rect.height - 4f), GetTierAccent(tier));

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = isSelected ? GoldAccent : Color.white;
            Widgets.Label(new Rect(rect.x + 20f, rect.y + 8f, 360f, 24f), GetTierTitle(tier));

            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            GUI.color = new Color(0.84f, 0.82f, 0.80f);
            Widgets.Label(new Rect(rect.x + 20f, rect.y + 34f, rect.width - 260f, 36f), GetTierDesc(tier));

            MapComponent_LCExtraction.GetTierCost(tier, out int cogCost, out int enkCost);
            Rect costRect = new Rect(rect.xMax - 224f, rect.y + 18f, 208f, 40f);
            if (isFree)
            {
                DrawStatusChip(costRect, "LC_Extract_FreeCost".Translate(), new Color(0.45f, 0.94f, 0.45f));
            }
            else
            {
                Color costColor = affordable ? GoldAccent : new Color(0.88f, 0.42f, 0.40f);
                DrawStatusChip(costRect, "LC_Extract_TierCost".Translate(cogCost, enkCost), costColor);
            }

            if (Widgets.ButtonInvisible(rect))
            {
                selectedTier = tier;
                transientError = null;
                (Defs.SoundDefOf.LC_Sound_Extraction_Select ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
            }

            GUI.color = Color.white;
        }

        private static void DrawStatusChip(Rect rect, string label, Color accent)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.10f, 0.05f, 0.05f, 0.92f));
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.65f);
            Widgets.DrawBox(rect, 1);
            GUI.color = accent;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
        }

        #endregion

        #region Mode B: Authentic LobCorp Extraction Chamber

        private void DrawExtractionChamber(Rect inRect, MapComponent_LCExtraction mgr, float now, float dt)
        {
            UpdateChamberTransitions(mgr, now);
            if (!isConfirming)
            {
                EnsureExtractionBgmPlaying();
            }

            // 1. 全铺红菱低聚面背景 + 3条纵向黑轨（严格对齐左/中/右箱体中心）
            DrawFullBackdrop(inRect, drawRails: true, dimFactor: 0.06f);

            int candidateCount = Mathf.Min(3, mgr.PendingCandidates.Count);
            float[] centerXs =
            {
                inRect.x + inRect.width * 0.21f,
                inRect.x + inRect.width * 0.50f,
                inRect.x + inRect.width * 0.79f
            };
            float baseW = 248f;
            float baseH = 310f; // 保持 512:640 严格比例 (4:5)
            float baseCenterY = inRect.y + inRect.height * 0.47f + ComputeChamberVerticalOffset(now);

            // 计算未旋转命中框
            Rect[] hitRects = new Rect[candidateCount];
            int hoveredIndex = -1;
            bool inputInteractive = !isRerolling && !isConfirming;

            for (int i = 0; i < candidateCount; i++)
            {
                hitRects[i] = new Rect(
                    centerXs[i] - baseW * 0.44f,
                    baseCenterY - baseH * 0.40f,
                    baseW * 0.88f,
                    baseH * 0.80f);

                if (inputInteractive && Mouse.IsOver(hitRects[i]))
                {
                    hoveredIndex = i;
                }
            }

            if (inputInteractive && hoveredIndex != lastHoveredIndex)
            {
                if (hoveredIndex >= 0 && mgr.SelectedIndex < 0)
                {
                    (Defs.SoundDefOf.LC_Sound_Extraction_Hover
                     ?? Defs.SoundDefOf.LC_Sound_Extraction_Select
                     ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
                }
                lastHoveredIndex = hoveredIndex;
            }

            int focusedIndex = -1;
            if (isConfirming && mgr.SelectedIndex >= 0 && mgr.SelectedIndex < candidateCount)
            {
                focusedIndex = mgr.SelectedIndex;
            }
            else if (!isRerolling)
            {
                if (mgr.SelectedIndex >= 0 && mgr.SelectedIndex < candidateCount)
                {
                    focusedIndex = mgr.SelectedIndex;
                }
                else if (hoveredIndex >= 0)
                {
                    focusedIndex = hoveredIndex;
                }
            }

            if (focusedIndex != lastFocusedIndex)
            {
                if (focusedIndex >= 0)
                {
                    focusFadeStartTime = now;
                }
                lastFocusedIndex = focusedIndex;
            }

            // 平滑更新各箱体缩放
            for (int i = 0; i < candidateCount; i++)
            {
                float targetScale = 1.0f;
                if (i == focusedIndex)
                {
                    bool lockedSelected = mgr.SelectedIndex == i;
                    targetScale = lockedSelected ? 1.18f : 1.13f;
                    if (isConfirming)
                    {
                        targetScale = 1.24f;
                    }
                    else if (lockedSelected && now - selectPulseTime < 0.14f)
                    {
                        float p = (now - selectPulseTime) / 0.14f;
                        targetScale += Mathf.Sin(p * Mathf.PI) * 0.05f;
                    }
                }
                else if (focusedIndex >= 0)
                {
                    targetScale = 0.96f;
                }

                slotScale[i] = Mathf.Lerp(slotScale[i], targetScale, Mathf.Clamp01(dt * 14f));
            }

            float focusAlpha = focusedIndex >= 0 ? Mathf.Clamp01((now - focusFadeStartTime) / 0.14f) : 0f;

            // 2. 先绘制所有非聚焦箱体（处于背面层）
            for (int i = 0; i < candidateCount; i++)
            {
                if (i == focusedIndex)
                {
                    continue;
                }

                float confirmDropOffset = 0f;
                if (isConfirming)
                {
                    float cp = Mathf.Clamp01((now - confirmStartTime) / 0.32f);
                    confirmDropOffset = cp * cp * 520f;
                }

                DrawExtractionUnit(
                    centerXs[i],
                    baseCenterY + confirmDropOffset,
                    baseW * slotScale[i],
                    baseH * slotScale[i],
                    mgr.PendingCandidates[i],
                    isFocused: false,
                    isLockedSelection: false,
                    isHovered: false,
                    now: now,
                    slotIndex: i);
            }

            // 3. 若有悬停/选中焦点：绘制压暗遮罩将背景与其余两箱体压暗（与原版实机一致）
            if (focusedIndex >= 0)
            {
                Widgets.DrawBoxSolid(inRect, new Color(0f, 0f, 0f, 0.50f * focusAlpha));
            }

            // 4. 在压暗遮罩之上绘制聚焦箱体（最前层，完整轮廓高亮 + 摇晃/震动动画 + 亮红灯）
            if (focusedIndex >= 0 && focusedIndex < candidateCount)
            {
                DrawExtractionUnit(
                    centerXs[focusedIndex],
                    baseCenterY,
                    baseW * slotScale[focusedIndex],
                    baseH * slotScale[focusedIndex],
                    mgr.PendingCandidates[focusedIndex],
                    isFocused: true,
                    isLockedSelection: mgr.SelectedIndex == focusedIndex,
                    isHovered: hoveredIndex == focusedIndex,
                    now: now,
                    slotIndex: focusedIndex);
            }

            // 5. 工业机械黑边框叠于箱体边缘外围
            if (TexFrameOverlay != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(inRect, TexFrameOverlay, ScaleMode.StretchToFill);
            }

            // 6. 顶部中央“二次提取”双管机关按钮（仅在尚未使用重抽或刚触发收回动画时绘制）
            Rect reExtractRect = default;
            if (!mgr.UsedReroll || isRerolling)
            {
                reExtractRect = DrawTopReExtractButton(inRect, mgr, now);
            }

            // 7. 仅在选中或悬停时显示底部黑墨水红字语录条（空闲时不显示！）
            Rect quoteBannerRect = default;
            Rect confirmBtnRect = default;
            if (focusedIndex >= 0 && focusedIndex < candidateCount && !isRerolling)
            {
                DrawBottomQuoteAndConfirm(
                    inRect,
                    mgr,
                    mgr.PendingCandidates[focusedIndex],
                    focusAlpha,
                    out quoteBannerRect,
                    out confirmBtnRect);
            }

            // 8. 角落微弱状态信息（不影响原版视觉）
            DrawCornerTelemetry(inRect, mgr);

            // 9. 处理箱体点击（首次点击锁定选中+开启摇晃；再次点击已选中箱体直接确认；点击空白取消选中）
            if (inputInteractive && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Vector2 mousePos = Event.current.mousePosition;
                bool clickedBox = false;
                for (int i = 0; i < candidateCount; i++)
                {
                    if (hitRects[i].Contains(mousePos))
                    {
                        clickedBox = true;
                        if (mgr.SelectedIndex == i)
                        {
                            StartConfirmSequence();
                        }
                        else
                        {
                            mgr.SelectedIndex = i;
                            selectPulseTime = now;
                            transientError = null;
                            (Defs.SoundDefOf.LC_Sound_Extraction_Select ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
                        }
                        Event.current.Use();
                        break;
                    }
                }

                if (!clickedBox
                    && mgr.SelectedIndex >= 0
                    && !reExtractRect.Contains(mousePos)
                    && !confirmBtnRect.Contains(mousePos)
                    && !quoteBannerRect.Contains(mousePos))
                {
                    mgr.SelectedIndex = -1;
                    transientError = null;
                    Event.current.Use();
                }
            }
        }

        private void ResetChamberAnimationState(float now)
        {
            dropInStartTime = now;
            selectPulseTime = -10f;
            lastHoveredIndex = -1;
            lastFocusedIndex = -1;
            isRerolling = false;
            rerollApplied = false;
            isConfirming = false;
            for (int i = 0; i < slotScale.Length; i++)
            {
                slotScale[i] = 1f;
            }
        }

        private float ComputeChamberVerticalOffset(float now)
        {
            // 二次提取序列（共 0.58s）：0..0.24s 落出画面，0.26..0.58s 从顶部垂降回弹
            if (isRerolling)
            {
                float t = now - rerollStartTime;
                if (t < 0.25f)
                {
                    float p = Mathf.Clamp01(t / 0.25f);
                    return p * p * 560f;
                }

                float inT = Mathf.Clamp01((t - 0.26f) / 0.32f);
                return -520f * Mathf.Pow(1f - inT, 3f);
            }

            // 初始进入时三箱从上方沿着轨道垂降到位
            if (dropInStartTime > 0f)
            {
                float age = now - dropInStartTime;
                if (age < 0.36f)
                {
                    float p = Mathf.Clamp01(age / 0.36f);
                    float inv = 1f - p;
                    return -460f * inv * inv * inv;
                }
            }

            return 0f;
        }

        private void UpdateChamberTransitions(MapComponent_LCExtraction mgr, float now)
        {
            if (isRerolling)
            {
                float t = now - rerollStartTime;
                if (!rerollApplied && t >= 0.25f)
                {
                    rerollApplied = true;
                    if (mgr.TryRerollCandidates(out string err))
                    {
                        (Defs.SoundDefOf.LC_Sound_Extraction_DropIn ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
                    }
                    else
                    {
                        transientError = err;
                        isRerolling = false;
                    }
                }

                if (t >= 0.58f)
                {
                    isRerolling = false;
                }
            }

            if (isConfirming)
            {
                float t = now - confirmStartTime;
                if (t >= 0.34f)
                {
                    isConfirming = false;
                    if (mgr.TryConfirmExtraction(anchorBuilding, out _, out string confirmErr))
                    {
                        Close(doCloseSound: false);
                    }
                    else
                    {
                        transientError = confirmErr;
                    }
                }
            }
        }

        private void EnsureExtractionBgmPlaying()
        {
            MusicManagerPlay music = Find.MusicManagerPlay;
            SongDef extractSong = Defs.SongDefOf.Anomaly_Extract;
            if (music == null || extractSong == null)
            {
                return;
            }

            SongDef cur = music.CurrentSong;
            if (cur == Defs.SongDefOf.FirstWarning
                || cur == Defs.SongDefOf.SecondWarning
                || cur == Defs.SongDefOf.ThirdWarning)
            {
                return;
            }

            if (cur != extractSong || !music.IsPlaying)
            {
                music.ForcePlaySong(extractSong, false);
                startedExtractionBgm = true;
            }
        }

        private void StartConfirmSequence()
        {
            if (isConfirming || isRerolling)
            {
                return;
            }

            isConfirming = true;
            confirmStartTime = Time.realtimeSinceStartup;
            (Defs.SoundDefOf.LC_Sound_Extraction_Confirm ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
        }

        private void DrawFullBackdrop(Rect rect, bool drawRails, float dimFactor)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.04f, 0.01f, 0.01f, 1f));
            if (TexBgRed != null)
            {
                GUI.color = new Color(0.95f, 0.92f, 0.92f, 1f);
                GUI.DrawTexture(rect, TexBgRed, ScaleMode.ScaleAndCrop);
            }

            if (dimFactor > 0f)
            {
                Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, dimFactor));
            }

            if (drawRails)
            {
                float[] xs =
                {
                    rect.x + rect.width * 0.21f,
                    rect.x + rect.width * 0.50f,
                    rect.x + rect.width * 0.79f
                };
                for (int i = 0; i < xs.Length; i++)
                {
                    Rect outerRail = new Rect(xs[i] - 4f, rect.y, 8f, rect.height);
                    Rect edgeShine = new Rect(xs[i] - 4f, rect.y, 1.5f, rect.height);
                    Widgets.DrawBoxSolid(outerRail, new Color(0.03f, 0.03f, 0.03f, 0.98f));
                    Widgets.DrawBoxSolid(edgeShine, new Color(0.22f, 0.20f, 0.20f, 0.40f));
                }
            }

            GUI.color = Color.white;
        }

        private Rect DrawTopReExtractButton(Rect inRect, MapComponent_LCExtraction mgr, float now)
        {
            float retractY = 0f;
            if (isRerolling)
            {
                float p = Mathf.Clamp01((now - rerollStartTime) / 0.22f);
                retractY = -96f * p;
            }

            float btnW = 340f;
            float btnH = 68f;
            Rect btnRect = new Rect(inRect.center.x - btnW * 0.5f, inRect.y - 2f + retractY, btnW, btnH);
            if (retractY < -btnH)
            {
                return Rect.zero;
            }

            bool canReroll = mgr.CanRerollCandidates(out string rerollFail);
            bool over = Mouse.IsOver(btnRect) && !isRerolling && !isConfirming;

            if (TexReExtractBtn != null)
            {
                GUI.color = !canReroll
                    ? new Color(0.55f, 0.52f, 0.52f, 0.85f)
                    : (over ? new Color(1.12f, 1.05f, 1.05f, 1f) : Color.white);
                GUI.DrawTexture(btnRect, TexReExtractBtn, ScaleMode.ScaleToFit);
            }

            Rect textRect = new Rect(btnRect.x + 114f, btnRect.y + 18f, 160f, 36f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = !canReroll
                ? new Color(0.55f, 0.28f, 0.26f)
                : (over ? new Color(1f, 0.45f, 0.35f) : ReExtractRed);
            Widgets.Label(textRect, "LC_Extract_ReExtractShort".Translate());
            GUI.color = Color.white;

            string tip = canReroll
                ? "LC_Extract_RerollBtn".Translate(MapComponent_LCExtraction.RerollCogitoCost, MapComponent_LCExtraction.RerollEnkephalinCost).ToString()
                : rerollFail;
            TooltipHandler.TipRegion(btnRect, tip);

            if (!isRerolling && !isConfirming && Widgets.ButtonInvisible(btnRect))
            {
                if (canReroll)
                {
                    transientError = null;
                    isRerolling = true;
                    rerollApplied = false;
                    rerollStartTime = now;
                    mgr.SelectedIndex = -1;
                    (Defs.SoundDefOf.LC_Sound_Extraction_Reroll ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
                }
                else
                {
                    transientError = rerollFail;
                    Messages.Message(rerollFail, MessageTypeDefOf.RejectInput, false);
                }
            }

            return btnRect;
        }

        private void DrawExtractionUnit(
            float cx,
            float cy,
            float w,
            float h,
            ThingDef candidateDef,
            bool isFocused,
            bool isLockedSelection,
            bool isHovered,
            float now,
            int slotIndex)
        {
            AbnormalityCodexEntryDef codex = candidateDef != null ? AbnormalityUtility.ResolveCodexEntry(candidateDef) : null;
            string codeText = ResolveClassificationCode(codex, candidateDef);
            bool isDonorBox = codeText.StartsWith("D-", StringComparison.OrdinalIgnoreCase);

            Texture2D texHighlight = isDonorBox ? (TexDonorHighlight ?? TexUnitHighlight) : TexUnitHighlight;
            Texture2D texChassis = isDonorBox ? (TexDonorChassis ?? TexUnitChassis) : TexUnitChassis;

            // 计算脑叶风格摇晃动画（悬停轻微晃动 + 选中后持续机械摇摆与间歇挣扎震动 + 确认剧烈震颤）
            float angleDeg = 0f;
            float jx = 0f;
            float jy = 0f;
            if (isFocused)
            {
                float riskMult = GetRiskShakeMultiplier(codex?.category?.defName);
                float t = now + slotIndex * 1.37f;

                if (isConfirming && isLockedSelection)
                {
                    float cp = Mathf.Clamp01((now - confirmStartTime) / 0.34f);
                    angleDeg = Mathf.Sin(now * 58f) * (3.5f + cp * 4.0f);
                    jx = Mathf.Cos(now * 72f) * (2.5f + cp * 3.0f);
                    jy = Mathf.Sin(now * 64f) * (2.5f + cp * 3.0f);
                }
                else if (isLockedSelection)
                {
                    // 持续晃动波 + 每 1.35 秒一次挣扎突发震动
                    float age = now - selectPulseTime;
                    float clickKick = Mathf.Max(0f, 1f - age / 0.24f);
                    float cycle = t % 1.35f;
                    float burstEnv = cycle < 0.42f ? Mathf.Sin((cycle / 0.42f) * Mathf.PI) : 0f;

                    float sway = Mathf.Sin(t * 9.0f) * 2.5f;
                    float rattle = Mathf.Sin(t * 50f) * (2.2f * burstEnv + 3.2f * clickKick);
                    angleDeg = (sway + rattle) * riskMult;
                    jx = Mathf.Cos(t * 43f) * (1.8f * burstEnv + 2.8f * clickKick) * riskMult;
                    jy = Mathf.Sin(t * 37f) * (1.4f * burstEnv + 2.2f * clickKick) * riskMult;
                }
                else if (isHovered)
                {
                    angleDeg = (Mathf.Sin(t * 7.2f) * 1.4f + Mathf.Sin(t * 22f) * 0.45f) * riskMult;
                    jx = Mathf.Cos(t * 26f) * 0.7f;
                    jy = Mathf.Sin(t * 21f) * 0.5f;
                }
            }

            Rect boxRect = new Rect(cx - w * 0.5f + jx, cy - h * 0.5f + jy, w, h);
            Vector2 pivot = new Vector2(cx + jx, cy + jy);

            Matrix4x4 savedMatrix = GUI.matrix;
            try
            {
                if (Mathf.Abs(angleDeg) > 0.01f)
                {
                    GUIUtility.RotateAroundPivot(angleDeg, pivot);
                }

                // 1. 悬停/选中时最底层乳黄色不规则外轮廓光晕
                if (isFocused && texHighlight != null)
                {
                    float pulse = isLockedSelection ? (0.94f + 0.06f * Mathf.Sin(now * 6.5f)) : 0.96f;
                    GUI.color = new Color(1f, 1f, 0.96f, pulse);
                    GUI.DrawTexture(boxRect, texHighlight, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }

                // 2. 机匣底板与齿轮架
                if (texChassis != null)
                {
                    GUI.color = isFocused ? Color.white : new Color(0.86f, 0.85f, 0.85f, 1f);
                    GUI.DrawTexture(boxRect, texChassis, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }

                // 3. 心电波纹红线
                if (TexUnitEkg != null)
                {
                    float ekgAlpha = isFocused ? (0.85f + 0.15f * Mathf.Sin(now * (isLockedSelection ? 14f : 7f))) : 0.45f;
                    GUI.color = new Color(1f, 1f, 1f, ekgAlpha);
                    GUI.DrawTexture(boxRect, TexUnitEkg, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }

                // 4. 圆形红灯指示镜
                if (TexUnitRedLamp != null)
                {
                    GUI.color = isFocused
                        ? new Color(1f, 0.96f, 0.96f, 1f)
                        : new Color(0.62f, 0.52f, 0.52f, 0.92f);
                    GUI.DrawTexture(boxRect, TexUnitRedLamp, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }

                // 5. 若已在图鉴解锁且被选中，在下方监视黑窗区域隐约浮现剪影（未解锁绝不剧透）
                if (isFocused && codex != null && codex.Discovered && codex.silhouette != null)
                {
                    Rect silRect = new Rect(
                        boxRect.x + boxRect.width * 0.37f,
                        boxRect.y + boxRect.height * 0.415f,
                        boxRect.width * 0.41f,
                        boxRect.height * 0.31f);
                    GUI.color = new Color(0.95f, 0.85f, 0.65f, isLockedSelection ? 0.34f : 0.22f);
                    Widgets.DrawTextureFitted(silRect, codex.silhouette, 0.90f);
                    GUI.color = Color.white;
                }

                // 6. 顶栏倾斜米黄色铭牌内的黑色居中异想体分类编号（严格落在铭牌矩形中心）
                Rect plateRect = new Rect(
                    boxRect.x + boxRect.width * 0.14f,
                    boxRect.y + boxRect.height * 0.220f,
                    boxRect.width * 0.72f,
                    boxRect.height * 0.12f);
                Vector2 plateCenter = plateRect.center;

                Matrix4x4 beforeTextMatrix = GUI.matrix;
                try
                {
                    GUIUtility.RotateAroundPivot(-2.1f, plateCenter);
                    Text.Font = isFocused ? GameFont.Medium : GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = CodeInkBlack;
                    Widgets.Label(plateRect, codeText);
                }
                finally
                {
                    GUI.matrix = beforeTextMatrix;
                    GUI.color = Color.white;
                }
            }
            finally
            {
                GUI.matrix = savedMatrix;
            }
        }

        private void DrawBottomQuoteAndConfirm(
            Rect inRect,
            MapComponent_LCExtraction mgr,
            ThingDef candidateDef,
            float focusAlpha,
            out Rect quoteBannerRect,
            out Rect confirmBtnRect)
        {
            AbnormalityCodexEntryDef codex = candidateDef != null ? AbnormalityUtility.ResolveCodexEntry(candidateDef) : null;
            bool discovered = codex != null && codex.Discovered;
            string quote = ResolveSelectionQuote(codex, candidateDef);

            // 严格固定在正中下方（居中于窗口下方，对齐中间槽位）
            float bannerW = 490f;
            float bannerH = 104f;
            quoteBannerRect = new Rect(
                inRect.center.x - bannerW * 0.5f,
                inRect.y + inRect.height * 0.755f,
                bannerW,
                bannerH);

            if (TexQuoteBanner != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.96f * focusAlpha);
                GUI.DrawTexture(quoteBannerRect, TexQuoteBanner, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(quoteBannerRect, new Color(0.02f, 0.02f, 0.02f, 0.94f * focusAlpha));
            }

            Rect quoteTextRect = quoteBannerRect.ContractedBy(26f, 20f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            GUI.color = new Color(QuoteCrimsonText.r, QuoteCrimsonText.g, QuoteCrimsonText.b, focusAlpha);
            Widgets.Label(quoteTextRect, quote);

            // 若已在图鉴发现，在黑条右下角用极简小字附注名称与等级
            if (discovered)
            {
                string risk = codex.category?.defName ?? "UNKNOWN";
                Color riskCol = GetRiskColor(risk);
                GUI.color = new Color(riskCol.r, riskCol.g, riskCol.b, 0.92f * focusAlpha);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.LowerRight;
                Widgets.Label(new Rect(quoteTextRect.x, quoteTextRect.y, quoteTextRect.width, quoteTextRect.height + 6f),
                    $"[{risk}] {codex.LabelCap}");
            }

            GUI.color = Color.white;

            // 确认操作条（仅当点击选中其中一项后显现，再次点击箱体或点此按钮均可完成提取）
            confirmBtnRect = Rect.zero;
            if (mgr.SelectedIndex >= 0)
            {
                float btnW = 228f;
                float btnH = 30f;
                confirmBtnRect = new Rect(
                    inRect.center.x - btnW * 0.5f,
                    quoteBannerRect.yMax + 4f,
                    btnW,
                    btnH);

                bool overConfirm = Mouse.IsOver(confirmBtnRect);
                Widgets.DrawBoxSolid(confirmBtnRect, overConfirm
                    ? new Color(0.32f, 0.07f, 0.07f, 0.96f)
                    : new Color(0.11f, 0.03f, 0.03f, 0.94f));
                GUI.color = overConfirm ? GoldAccent : new Color(0.86f, 0.72f, 0.36f, 0.90f);
                Widgets.DrawBox(confirmBtnRect, 1);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(confirmBtnRect, "LC_Extract_ConfirmBtn".Translate());
                GUI.color = Color.white;

                TooltipHandler.TipRegion(confirmBtnRect, "LC_Extract_ClickAgainConfirm".Translate());

                if (Widgets.ButtonInvisible(confirmBtnRect))
                {
                    StartConfirmSequence();
                }
            }
        }

        private static void DrawCornerTelemetry(Rect inRect, MapComponent_LCExtraction mgr)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.75f, 0.72f, 0.62f, 0.85f);

            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(inRect.x + 14f, inRect.y + 8f, 300f, 20f),
                "LC_Extract_ActiveTierHeader".Translate(GetTierTitle(mgr.ActiveTier)));

            int cogitoCount = mgr.CountAvailableResource(Defs.ThingDefOf.Cogito);
            int enkCount = mgr.CountAvailableResource(Defs.ThingDefOf.EnkephalinBox);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inRect.xMax - 336f, inRect.y + 8f, 290f, 20f),
                $"{"LC_Extract_CogitoStock".Translate(cogitoCount)}  |  {"LC_Extract_EnkStock".Translate(enkCount)}");

            GUI.color = Color.white;
        }

        #endregion

        #region Helpers

        private static string ResolveClassificationCode(AbnormalityCodexEntryDef codex, ThingDef candidateDef)
        {
            string key = codex != null ? $"LC_Extract_Code_{codex.defName}" : null;
            if (!key.NullOrEmpty() && LanguageDatabase.activeLanguage != null && key.CanTranslate())
            {
                return key.Translate().ToString();
            }

            string defKey = candidateDef != null ? $"LC_Extract_Code_{candidateDef.defName}" : null;
            if (!defKey.NullOrEmpty() && defKey.CanTranslate())
            {
                return defKey.Translate().ToString();
            }

            return "LC_Extract_CodeUnknown".Translate().ToString();
        }

        private static string ResolveSelectionQuote(AbnormalityCodexEntryDef codex, ThingDef candidateDef)
        {
            string key = codex != null ? $"LC_Extract_Quote_{codex.defName}" : null;
            if (!key.NullOrEmpty() && LanguageDatabase.activeLanguage != null && key.CanTranslate())
            {
                return key.Translate().ToString();
            }

            string defKey = candidateDef != null ? $"LC_Extract_Quote_{candidateDef.defName}" : null;
            if (!defKey.NullOrEmpty() && defKey.CanTranslate())
            {
                return defKey.Translate().ToString();
            }

            return "LC_Extract_QuoteDefault".Translate().ToString();
        }

        private static float GetRiskShakeMultiplier(string risk)
        {
            switch (risk)
            {
                case "ZAYIN": return 0.72f;
                case "TETH": return 0.90f;
                case "HE": return 1.05f;
                case "WAW": return 1.25f;
                case "ALEPH": return 1.45f;
                default: return 1.0f;
            }
        }

        private static string GetTierTitle(LCExtractionTier tier)
        {
            return $"LC_Extract_Tier_{tier}_Label".Translate().ToString();
        }

        private static string GetTierDesc(LCExtractionTier tier)
        {
            return $"LC_Extract_Tier_{tier}_Desc".Translate().ToString();
        }

        private static Color GetTierAccent(LCExtractionTier tier)
        {
            switch (tier)
            {
                case LCExtractionTier.Shallow: return new Color(0.28f, 0.85f, 0.38f);
                case LCExtractionTier.Mid: return new Color(0.95f, 0.78f, 0.22f);
                case LCExtractionTier.Deep: return new Color(0.68f, 0.36f, 0.92f);
                case LCExtractionTier.Abyss: return new Color(0.90f, 0.16f, 0.20f);
                case LCExtractionTier.Standard:
                default: return new Color(0.25f, 0.65f, 0.98f);
            }
        }

        private static Color GetRiskColor(string risk)
        {
            switch (risk)
            {
                case "ZAYIN": return new Color(0.32f, 0.90f, 0.42f);
                case "TETH": return new Color(0.32f, 0.68f, 0.98f);
                case "HE": return new Color(0.96f, 0.84f, 0.28f);
                case "WAW": return new Color(0.72f, 0.38f, 0.95f);
                case "ALEPH": return new Color(0.96f, 0.22f, 0.24f);
                default: return Color.white;
            }
        }

        #endregion
    }
}
