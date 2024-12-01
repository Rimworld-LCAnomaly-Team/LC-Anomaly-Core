using Verse;

namespace LCAnomalyCore.Settings
{
    /// <summary>
    /// 游戏内配置文件
    /// </summary>
    public class Setting_LCAnomalyCore : ModSettings
    {
        #region 字段

        #region 警报相关

        /// <summary>
        /// 是否显示警报相关自定义选项
        /// </summary>
        public bool If_ShowWarningSettings = false;

        /// <summary>
        /// 是否启用LC警报系统
        /// </summary>
        public bool If_EnableLCWarning = true;

        /// <summary>
        /// 是否启用LC警报音乐
        /// </summary>
        public bool If_EnableLCWarningMusic = true;

        /// <summary>
        /// 是否启用自定义警报点数降低选项
        /// </summary>
        public bool If_EnableCustomWarningFadeOption = false;

        /// <summary>
        /// 警报点数降低间隔（Tick）
        /// </summary>
        public int PointsOfWarning_FadeDuration = 900;

        /// <summary>
        /// 每次降低的警报点数
        /// </summary>
        public int PointsOfWarning_FadeAmount = 1;

        /// <summary>
        /// 是否启用死亡警报点数计算
        /// </summary>
        public bool If_EnableLCWarningDeath = true;

        /// <summary>
        /// 玩家派系死亡提供的警报点数
        /// </summary>
        public int PointsOfWarning_PlayerFactionDie = 4;

        /// <summary>
        /// 友军派系死亡提供的警报点数
        /// </summary>
        public int PointsOfWarning_AllyFactionDie = 2;

        /// <summary>
        /// 中立派系死亡提供的警报点数
        /// </summary>
        public int PointsOfWarning_NeturalFactionDie = 0;

        /// <summary>
        /// 是否启用精神崩溃警报点数计算
        /// </summary>
        public bool If_EnableLCWarningMentalBreak = true;

        /// <summary>
        /// 玩家派系精神崩溃提供的警报点数
        /// </summary>
        public int PointsOfWarning_PlayerFactionMentalBreak = 2;

        /// <summary>
        /// 友军派系精神崩溃提供的警报点数
        /// </summary>
        public int PointsOfWarning_AllyFactionMentalBreak = 1;

        /// <summary>
        /// 中立派系精神崩溃提供的警报点数
        /// </summary>
        public int PointsOfWarning_NeturalFactionMentalBreak = 0;

        #endregion 警报相关

        #endregion 字段

        /// <summary>
        /// 保存相关
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            #region 警报相关

            //是否显示
            Scribe_Values.Look<bool>(ref this.If_ShowWarningSettings, "If_ShowWarningSettings", false, false);

            //是否开启
            Scribe_Values.Look<bool>(ref this.If_EnableLCWarning, "If_EnableLCWarning", true, false);
            Scribe_Values.Look<bool>(ref this.If_EnableLCWarningMusic, "If_EnableLCWarningMusic", true, false);

            //计算周期
            Scribe_Values.Look<bool>(ref this.If_EnableCustomWarningFadeOption, "If_EnableCustomWarningFadeOption", false, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_FadeDuration, "PointsOfWarning_FadeDuration", 900, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_FadeAmount, "PointsOfWarning_FadeAmount", 1, false);

            //死亡
            Scribe_Values.Look<bool>(ref this.If_EnableLCWarningDeath, "If_EnableLCWarningDeath", true, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_PlayerFactionDie, "PointsOfWarning_PlayerFactionDie", 4, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_AllyFactionDie, "PointsOfWarning_AllyFactionDie", 2, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_NeturalFactionDie, "PointsOfWarning_NeturalFactionDie", 0, false);

            //精神崩溃
            Scribe_Values.Look<bool>(ref this.If_EnableLCWarningMentalBreak, "If_EnableLCWarningMentalBreak", true, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_PlayerFactionMentalBreak, "PointsOfWarning_PlayerFactionMentalBreak", 2, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_AllyFactionMentalBreak, "PointsOfWarning_AllyFactionMentalBreak", 1, false);
            Scribe_Values.Look<int>(ref this.PointsOfWarning_NeturalFactionMentalBreak, "PointsOfWarning_NeturalFactionMentalBreak", 0, false);

            #endregion 警报相关
        }
    }
}
