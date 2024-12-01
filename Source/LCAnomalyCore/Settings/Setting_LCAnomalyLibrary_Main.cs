using UnityEngine;
using Verse;

namespace LCAnomalyCore.Settings
{
    public class Setting_LCAnomalyCore_Main : Mod
    {
        /// <summary>
        /// 设置文件对象
        /// </summary>
        public static Setting_LCAnomalyCore Settings;

        public static string ContentDir;

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
            float y = 0;

            y += 40;
            Widgets.Checkbox(0f, y, ref Settings.If_ShowWarningSettings, 25f, false, false, null, null);
            Widgets.Label(new Rect(35f, y + 1f, inRect.width - 50f, 25f), Translator.Translate("If_ShowWarningSettingsText"));
            ShowWarningSettings(Settings.If_ShowWarningSettings, ref y, inRect);

            base.DoSettingsWindowContents(inRect);
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
    }
}
