using System.Collections.Generic;
using LCAnomalyCore.Damage;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Settings
{
    /// <summary>表示 <c>Setting_LCAnomalyCore_Main</c> 类型。</summary>
    public class Setting_LCAnomalyCore_Main : Mod
    {
        private enum SettingsTab
        {
            Combat,
            WarningAndDepartment,
            ApparelOverrides,
            WeaponOverrides,
            RaceOverrides
        }

        /// <summary>
        /// 设置文件对象
        /// </summary>
        public static Setting_LCAnomalyCore Settings;

        /// <summary>表示 <c>ContentDir</c>。</summary>
        public static string ContentDir;

        private SettingsTab activeTab = SettingsTab.Combat;
        private Vector2 warningScroll;
        private float warningScrollHeight = 620f;

        private string PointsOfWarning_FadeDurationEditBuffer;
        private string PointsOfWarning_FadeAmountEditBuffer;

        private string PointsOfWarning_PlayerFactionDieEditBuffer;
        private string PointsOfWarning_AllyFactionDieEditBuffer;
        private string PointsOfWarning_NeturalFactionDieEditBuffer;

        private string PointsOfWarning_PlayerFactionMentalBreakEditBuffer;
        private string PointsOfWarning_AllyFactionMentalBreakEditBuffer;
        private string PointsOfWarning_NeturalFactionMentalBreakEditBuffer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="content"></param>
        public Setting_LCAnomalyCore_Main(ModContentPack content) : base(content)
        {
            Setting_LCAnomalyCore_Main.Settings = base.GetSettings<Setting_LCAnomalyCore>();
            ContentDir = base.Content.RootDir;
        }

        /// <summary>
        /// mod配置文件列表名称
        /// </summary>
        /// <returns></returns>
        public override string SettingsCategory()
        {
            return Translator.Translate("Setting_LCAnomalyCore_Label");
        }

        /// <summary>
        /// 界面绘制
        /// </summary>
        /// <param name="inRect"></param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Settings == null)
            {
                return;
            }

            Rect sectionRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 36f);
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("LC_Tab_CombatCompat".Translate(), () => SelectTab(SettingsTab.Combat), activeTab == SettingsTab.Combat),
                new TabRecord("LC_Tab_WarningDept".Translate(), () => SelectTab(SettingsTab.WarningAndDepartment), activeTab == SettingsTab.WarningAndDepartment),
                new TabRecord("LC_Tab_ApparelOverrides".Translate(), () => SelectTab(SettingsTab.ApparelOverrides), activeTab == SettingsTab.ApparelOverrides),
                new TabRecord("LC_Tab_WeaponOverrides".Translate(), () => SelectTab(SettingsTab.WeaponOverrides), activeTab == SettingsTab.WeaponOverrides),
                new TabRecord("LC_Tab_RaceOverrides".Translate(), () => SelectTab(SettingsTab.RaceOverrides), activeTab == SettingsTab.RaceOverrides),
            };

            Widgets.DrawMenuSection(sectionRect);
            TabDrawer.DrawTabs(sectionRect, tabs);

            Rect contentRect = sectionRect.ContractedBy(10f);
            switch (activeTab)
            {
                case SettingsTab.Combat:
                    DrawCombatTab(contentRect);
                    break;
                case SettingsTab.WarningAndDepartment:
                    DrawWarningAndDepartmentTab(contentRect);
                    break;
                case SettingsTab.ApparelOverrides:
                    FourColorSettingsPages.DrawApparelPage(contentRect);
                    break;
                case SettingsTab.WeaponOverrides:
                    FourColorSettingsPages.DrawWeaponPage(contentRect);
                    break;
                case SettingsTab.RaceOverrides:
                    FourColorSettingsPages.DrawRacePage(contentRect);
                    break;
            }

            base.DoSettingsWindowContents(inRect);
        }

        private void SelectTab(SettingsTab tab)
        {
            if (activeTab != tab)
            {
                activeTab = tab;
                FourColorSettingsPages.ClearBuffers();
            }
        }

        private void DrawCombatTab(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            try
            {
                Text.Font = GameFont.Medium;
                listing.Label("LC_Settings_CombatHeader".Translate());
                Text.Font = GameFont.Small;
                listing.GapLine(6f);

                listing.CheckboxLabeled(
                    "LC_Settings_ShowFourColorHitIcons".Translate(),
                    ref Settings.If_ShowFourColorHitIcons,
                    "LC_Settings_ShowFourColorHitIconsDesc".Translate());
                listing.CheckboxLabeled(
                    "LC_Settings_ShowFourColorDamageNumbers".Translate(),
                    ref Settings.If_ShowFourColorDamageNumbers,
                    "LC_Settings_ShowFourColorDamageNumbersDesc".Translate());
                listing.CheckboxLabeled(
                    "LC_Settings_GuaranteedFourColorHit".Translate(),
                    ref Settings.If_GuaranteedFourColorHit,
                    "LC_Settings_GuaranteedFourColorHitDesc".Translate());

                string scaleLabel = "LC_Settings_GlobalDamageScale".Translate(Settings.FourColorGlobalDamageScale.ToString("0.00"));
                Settings.FourColorGlobalDamageScale = Mathf.Clamp(
                    listing.SliderLabeled(scaleLabel, Settings.FourColorGlobalDamageScale, 0.25f, 3.0f,
                        tooltip: "LC_Settings_GlobalDamageScaleDesc".Translate()),
                    0.25f, 3.0f);
                Settings.FourColorGlobalDamageScale = Mathf.Round(Settings.FourColorGlobalDamageScale * 20f) / 20f;

                listing.Gap(12f);
                Text.Font = GameFont.Medium;
                listing.Label("LC_Settings_CompatHeader".Translate());
                Text.Font = GameFont.Small;
                listing.GapLine(6f);

                bool oldDefaults = Settings.If_EnableFourColorDefaults;
                bool oldAutoInfer = Settings.If_AutoInferUnlistedApparel;

                listing.CheckboxLabeled(
                    "LC_Settings_EnableFourColorDefaults".Translate(),
                    ref Settings.If_EnableFourColorDefaults,
                    "LC_Settings_EnableFourColorDefaultsDesc".Translate());
                listing.CheckboxLabeled(
                    "LC_Settings_AutoInferApparel".Translate(),
                    ref Settings.If_AutoInferUnlistedApparel,
                    "LC_Settings_AutoInferApparelDesc".Translate());
                listing.CheckboxLabeled(
                    "LC_Settings_EnableQualityScaling".Translate(),
                    ref Settings.If_EnableQualityScaling,
                    "LC_Settings_EnableQualityScalingDesc".Translate());

                if (Settings.If_EnableQualityScaling)
                {
                    listing.CheckboxLabeled(
                        "LC_Settings_ApplyQualityToEGO".Translate(),
                        ref Settings.If_ApplyQualityScalingToEGO,
                        "LC_Settings_ApplyQualityToEGODesc".Translate(),
                        height: 0f,
                        labelPct: 1f);
                }

                if (oldDefaults != Settings.If_EnableFourColorDefaults || oldAutoInfer != Settings.If_AutoInferUnlistedApparel)
                {
                    FourColorOverrides.RebuildCache();
                    FourColorSettingsPages.ClearBuffers();
                }

                listing.Gap(16f);
                if (listing.ButtonText("LC_Settings_ResetCombatDefaults".Translate(), widthPct: 0.3f))
                {
                    Settings.ResetCombatDefaults();
                    FourColorSettingsPages.ClearBuffers();
                }
            }
            finally
            {
                listing.End();
            }
        }

        private void DrawWarningAndDepartmentTab(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, Mathf.Max(warningScrollHeight, rect.height));
            Widgets.BeginScrollView(rect, ref warningScroll, viewRect);
            try
            {
                float y = 0f;

                //警报选项
                Widgets.Checkbox(0f, y, ref Settings.If_ShowWarningSettings, 25f, false, false, null, null);
                Widgets.Label(new Rect(35f, y + 1f, viewRect.width - 50f, 25f), Translator.Translate("If_ShowWarningSettingsText"));
                ShowWarningSettings(Settings.If_ShowWarningSettings, ref y, viewRect);

                //部门选项
                y += 40f;
                Widgets.Checkbox(0f, y, ref Settings.If_ShowDepartmentSettings, 25f, false, false, null, null);
                Widgets.Label(new Rect(35f, y + 1f, viewRect.width - 50f, 25f), Translator.Translate("If_ShowDepartmentSettingsText"));
                ShowDepartmentSettings(Settings.If_ShowDepartmentSettings, ref y, viewRect);

                //逆卡巴拉熔毁设置
                y += 45f;
                ShowMeltdownSettings(ref y, viewRect);

                warningScrollHeight = y + 60f;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>
        /// 警报相关菜单
        /// </summary>
        /// <param name="state"></param>
        /// <param name="y"></param>
        /// <param name="inRect"></param>
        private void ShowWarningSettings(bool state, ref float y, Rect inRect)
        {
            if (!state)
                return;

            y += 40;
            Widgets.Checkbox(35f, y, ref Settings.If_EnableLCWarning, 25f, false, false, null, null);
            Widgets.Label(new Rect(70f, y + 1f, inRect.width - 50f, 25f), Translator.Translate("If_EnableLCWarningText"));
            if (Settings.If_EnableLCWarning)
            {
                //启用警报
                y += 40;
                Widgets.Checkbox(70f, y, ref Settings.If_EnableLCWarningMusic, 25f, false, false, null, null);
                Widgets.Label(new Rect(105f, y + 1f, inRect.width - 50f, 25f), Translator.Translate("If_EnableLCWarningMusicText"));

                //警报点数降低
                y += 40;
                Widgets.Checkbox(70f, y, ref Settings.If_EnableCustomWarningFadeOption, 25f, false, false, null, null);
                Widgets.Label(new Rect(105f, y + 1f, inRect.width - 50f, 25f), Translator.Translate("If_EnableCustomWarningFadeOptionText"));
                if (Settings.If_EnableCustomWarningFadeOption)
                {
                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_FadeDurationText")
                        , ref Settings.PointsOfWarning_FadeDuration
                        , ref this.PointsOfWarning_FadeDurationEditBuffer);

                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_FadeAmountText")
                        , ref Settings.PointsOfWarning_FadeAmount
                        , ref this.PointsOfWarning_FadeAmountEditBuffer);
                }

                //死亡
                y += 40;
                Widgets.Checkbox(70f, y, ref Settings.If_EnableLCWarningDeath, 25f, false, false, null, null);
                Widgets.Label(new Rect(105f, y + 1f, inRect.width - 50f, 25f), Translator.Translate("If_EnableLCWarningDeathText"));
                if (Settings.If_EnableLCWarningDeath)
                {
                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_PlayerFactionText")
                        , ref Settings.PointsOfWarning_PlayerFactionDie
                        , ref this.PointsOfWarning_PlayerFactionDieEditBuffer);

                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_AllyFactionText")
                        , ref Settings.PointsOfWarning_AllyFactionDie
                        , ref this.PointsOfWarning_AllyFactionDieEditBuffer);

                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_NeturalFactionText")
                        , ref Settings.PointsOfWarning_NeturalFactionDie
                        , ref this.PointsOfWarning_NeturalFactionDieEditBuffer);
                }

                //心理崩溃
                y += 40;
                Widgets.Checkbox(70f, y, ref Settings.If_EnableLCWarningMentalBreak, 25f, false, false, null, null);
                Widgets.Label(new Rect(105f, y + 1f, inRect.width - 50f, 25f), Translator.Translate("If_EnableLCWarningMentalBreakText"));
                if (Settings.If_EnableLCWarningMentalBreak)
                {
                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_PlayerFactionText")
                        , ref Settings.PointsOfWarning_PlayerFactionMentalBreak
                        , ref this.PointsOfWarning_PlayerFactionMentalBreakEditBuffer);

                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_AllyFactionText")
                        , ref Settings.PointsOfWarning_AllyFactionMentalBreak
                        , ref this.PointsOfWarning_AllyFactionMentalBreakEditBuffer);

                    y += 40;
                    Widgets.TextFieldNumericLabeled(new Rect(0f, y, inRect.width * 0.5f, 25f)
                        , Translator.Translate("PointsOfWarning_NeturalFactionText")
                        , ref Settings.PointsOfWarning_NeturalFactionMentalBreak
                        , ref this.PointsOfWarning_NeturalFactionMentalBreakEditBuffer);
                }
            }
        }

        /// <summary>
        /// 部门相关菜单
        /// </summary>
        /// <param name="state"></param>
        /// <param name="y"></param>
        /// <param name="inRect"></param>
        private void ShowDepartmentSettings(bool state, ref float y, Rect inRect)
        {
            if (!state)
                return;

            y += 40;
            Widgets.Checkbox(35f, y, ref Settings.If_ShowDepartmentLabel_ColonistBar, 25f, false, false, null, null);
            Widgets.Label(new Rect(70f, y + 1f, inRect.width - 50f, 25f), Translator.Translate("If_ShowDepartmentLabel_ColonistBarText"));
            if (Settings.If_ShowDepartmentLabel_ColonistBar)
            {
                y += 40;
                Widgets.HorizontalSlider(new Rect(70f, y, inRect.width * 0.5f, 25f)
                    , ref Settings.DepartmentLabel_ColonistBar_VerticalOffset
                    , new FloatRange(-5, 5)
                    , Translator.Translate("DepartmentLabel_ColonistBar_VerticalOffsetText"));

                y += 40;
                Widgets.HorizontalSlider(new Rect(70f, y, inRect.width * 0.5f, 25f)
                    , ref Settings.DepartmentLabel_ColonistBar_VerticalOffsetPerLine
                    , new FloatRange(-5, 5)
                    , Translator.Translate("DepartmentLabel_ColonistBar_VerticalOffsetPerLineText"));
            }
        }

        private void ShowMeltdownSettings(ref float y, Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 28f), "LC_Settings_MeltdownHeader".Translate());
            Text.Font = GameFont.Small;
            y += 32f;

            Widgets.Checkbox(0f, y, ref Settings.If_ShowManagerBulletHud, 25f, false, false, null, null);
            Widgets.Label(new Rect(35f, y + 1f, inRect.width - 50f, 25f), "LC_Settings_ShowManagerBulletHud".Translate());

            y += 34f;
            Widgets.Checkbox(0f, y, ref Settings.If_EnableQliphothMeltdown, 25f, false, false, null, null);
            Widgets.Label(new Rect(35f, y + 1f, inRect.width - 50f, 25f), "LC_Settings_EnableQliphothMeltdown".Translate());

            if (!Settings.If_EnableQliphothMeltdown)
            {
                return;
            }

            y += 34f;
            Widgets.Checkbox(35f, y, ref Settings.If_ShowQliphothMeltdownHud, 25f, false, false, null, null);
            Widgets.Label(new Rect(70f, y + 1f, inRect.width - 80f, 25f), "LC_Settings_ShowQliphothMeltdownHud".Translate());

            y += 34f;
            Widgets.Checkbox(35f, y, ref Settings.If_EnableMeltdownOrdealTrigger, 25f, false, false, null, null);
            Widgets.Label(new Rect(70f, y + 1f, inRect.width - 80f, 25f), "LC_Settings_EnableMeltdownOrdealTrigger".Translate());

            y += 38f;
            float worksFloat = Settings.QliphothMeltdown_WorksPerWave;
            worksFloat = Widgets.HorizontalSlider(
                new Rect(35f, y, inRect.width * 0.65f, 28f),
                worksFloat,
                2f,
                20f,
                label: "LC_Settings_MeltdownWorksPerWave".Translate(Settings.QliphothMeltdown_WorksPerWave),
                roundTo: 1f);
            Settings.QliphothMeltdown_WorksPerWave = Mathf.Clamp(Mathf.RoundToInt(worksFloat), 2, 20);

            y += 40f;
            float secFloat = Settings.QliphothMeltdown_CountdownSeconds;
            secFloat = Widgets.HorizontalSlider(
                new Rect(35f, y, inRect.width * 0.65f, 28f),
                secFloat,
                20f,
                180f,
                label: "LC_Settings_MeltdownCountdownSeconds".Translate(Settings.QliphothMeltdown_CountdownSeconds),
                roundTo: 5f);
            Settings.QliphothMeltdown_CountdownSeconds = Mathf.Clamp(Mathf.RoundToInt(secFloat), 20, 180);
        }
    }
}
