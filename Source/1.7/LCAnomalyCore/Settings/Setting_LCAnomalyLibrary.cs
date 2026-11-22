using System.Collections.Generic;
using LCAnomalyCore.Damage;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Settings
{
    /// <summary>
    /// 游戏内配置文件
    /// </summary>
    public class Setting_LCAnomalyCore : ModSettings
    {
        public const int CurrentSettingsVersion = 1;

        private int settingsVersion;

        #region 字段

        #region 战斗与四色生态兼容

        public bool If_ShowFourColorHitIcons = true;
        public bool If_ShowFourColorDamageNumbers = true;
        public bool If_EnableFourColorDefaults = true;
        public bool If_AutoInferUnlistedApparel = true;
        public bool If_EnableQualityScaling = true;
        public bool If_ApplyQualityScalingToEGO = false;
        public float FourColorGlobalDamageScale = 1.0f;

        public List<FourColorApparelOverride> apparelOverrides = new List<FourColorApparelOverride>();
        public List<FourColorWeaponOverride> weaponOverrides = new List<FourColorWeaponOverride>();
        public List<FourColorRaceOverride> raceOverrides = new List<FourColorRaceOverride>();

        #endregion 战斗与四色生态兼容

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

        #region 部门分配显示

        /// <summary>
        /// 是否显示部门分配列表
        /// </summary>
        public bool If_ShowDepartmentSettings = false;

        /// <summary>
        /// 是否在殖民者栏显示
        /// </summary>
        public bool If_ShowDepartmentLabel_ColonistBar = true;

        /// <summary>表示 <c>DepartmentLabel_ColonistBar_VerticalOffset</c>。</summary>
        public float DepartmentLabel_ColonistBar_VerticalOffset = -1f;
        /// <summary>表示 <c>DepartmentLabel_ColonistBar_VerticalOffsetPerLine</c>。</summary>
        public float DepartmentLabel_ColonistBar_VerticalOffsetPerLine = 0.1f;

        #endregion

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

            #region 部门分配显示

            //是否显示分配列表
            Scribe_Values.Look<bool>(ref this.If_ShowDepartmentSettings, "If_ShowDepartmentSettings", false, false);

            //是否在殖民者栏显示
            Scribe_Values.Look<bool>(ref this.If_ShowDepartmentLabel_ColonistBar, "If_ShowDepartmentLabel_ColonistBar", true, false);

            //殖民者栏垂直偏移量
            Scribe_Values.Look<float>(ref this.DepartmentLabel_ColonistBar_VerticalOffset, "DepartmentLabel_ColonistBar_VerticalOffset", -1f, false);
            //殖民者栏每行垂直偏移量
            Scribe_Values.Look<float>(ref this.DepartmentLabel_ColonistBar_VerticalOffsetPerLine, "DepartmentLabel_ColonistBar_VerticalOffsetPerLine", 0.1f, false);

            #endregion

            #region 战斗与四色生态兼容

            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
            Scribe_Values.Look(ref If_ShowFourColorHitIcons, "If_ShowFourColorHitIcons", true);
            Scribe_Values.Look(ref If_ShowFourColorDamageNumbers, "If_ShowFourColorDamageNumbers", true);
            Scribe_Values.Look(ref If_EnableFourColorDefaults, "If_EnableFourColorDefaults", true);
            Scribe_Values.Look(ref If_AutoInferUnlistedApparel, "If_AutoInferUnlistedApparel", true);
            Scribe_Values.Look(ref If_EnableQualityScaling, "If_EnableQualityScaling", true);
            Scribe_Values.Look(ref If_ApplyQualityScalingToEGO, "If_ApplyQualityScalingToEGO", false);
            Scribe_Values.Look(ref FourColorGlobalDamageScale, "FourColorGlobalDamageScale", 1.0f);

            Scribe_Collections.Look(ref apparelOverrides, "apparelOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref weaponOverrides, "weaponOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref raceOverrides, "raceOverrides", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                apparelOverrides ??= new List<FourColorApparelOverride>();
                weaponOverrides ??= new List<FourColorWeaponOverride>();
                raceOverrides ??= new List<FourColorRaceOverride>();

                apparelOverrides.RemoveAll(e => e == null || e.defName.NullOrEmpty());
                weaponOverrides.RemoveAll(e => e == null || e.defName.NullOrEmpty());
                raceOverrides.RemoveAll(e => e == null || e.defName.NullOrEmpty());

                FourColorGlobalDamageScale = Mathf.Clamp(FourColorGlobalDamageScale, 0.25f, 3.0f);

                if (settingsVersion < CurrentSettingsVersion)
                {
                    ApplyMigrations();
                }

                if (FourColorOverrides.IsInitialized)
                {
                    FourColorOverrides.RebuildCache();
                }
            }

            #endregion
        }

        private void ApplyMigrations()
        {
            if (settingsVersion < 1)
            {
                settingsVersion = 1;
            }
        }

        public FourColorApparelOverride GetOrCreateApparelOverride(string defName)
        {
            apparelOverrides ??= new List<FourColorApparelOverride>();
            for (int i = 0; i < apparelOverrides.Count; i++)
            {
                if (apparelOverrides[i] != null && apparelOverrides[i].defName == defName)
                {
                    return apparelOverrides[i];
                }
            }

            var entry = new FourColorApparelOverride { defName = defName };
            apparelOverrides.Add(entry);
            return entry;
        }

        public void RemoveApparelOverride(string defName)
        {
            apparelOverrides?.RemoveAll(e => e != null && e.defName == defName);
        }

        public FourColorWeaponOverride GetOrCreateWeaponOverride(string defName)
        {
            weaponOverrides ??= new List<FourColorWeaponOverride>();
            for (int i = 0; i < weaponOverrides.Count; i++)
            {
                if (weaponOverrides[i] != null && weaponOverrides[i].defName == defName)
                {
                    return weaponOverrides[i];
                }
            }

            var entry = new FourColorWeaponOverride { defName = defName };
            weaponOverrides.Add(entry);
            return entry;
        }

        public void RemoveWeaponOverride(string defName)
        {
            weaponOverrides?.RemoveAll(e => e != null && e.defName == defName);
        }

        public FourColorRaceOverride GetOrCreateRaceOverride(string defName)
        {
            raceOverrides ??= new List<FourColorRaceOverride>();
            for (int i = 0; i < raceOverrides.Count; i++)
            {
                if (raceOverrides[i] != null && raceOverrides[i].defName == defName)
                {
                    return raceOverrides[i];
                }
            }

            var entry = new FourColorRaceOverride { defName = defName };
            raceOverrides.Add(entry);
            return entry;
        }

        public void RemoveRaceOverride(string defName)
        {
            raceOverrides?.RemoveAll(e => e != null && e.defName == defName);
        }

        public void ResetCombatDefaults()
        {
            If_ShowFourColorHitIcons = true;
            If_ShowFourColorDamageNumbers = true;
            If_EnableFourColorDefaults = true;
            If_AutoInferUnlistedApparel = true;
            If_EnableQualityScaling = true;
            If_ApplyQualityScalingToEGO = false;
            FourColorGlobalDamageScale = 1.0f;

            FourColorOverrides.RebuildCache();
        }
    }
}
