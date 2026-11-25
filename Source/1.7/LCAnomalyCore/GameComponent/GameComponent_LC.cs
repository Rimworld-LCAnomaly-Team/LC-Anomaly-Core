using LCAnomalyCore.Buildings;
using LCAnomalyCore.Settings;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.GameComponent
{
    /// <summary>表示 <c>LCGameComponentTick</c> 类型。</summary>
    public delegate void LCGameComponentTick();

    /// <summary>
    /// LC游戏组件
    /// </summary>
    public class GameComponent_LC : Verse.GameComponent
    {
        public Singleton.LCCanvasSingleton WarningOverlay { get; } = new Singleton.LCCanvasSingleton();

        public override void GameComponentOnGUI()
        {
            WarningOverlay.Draw();
        }

        /// <summary>
        /// 当前的警报点数
        /// </summary>
        public int CurWarningPoints
        {
            get => curWarningPoints;
            set
            {
                //如果未启用警报系统，就不处理
                if (!Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarning) return;

                if (value == curWarningPoints) return;
                if (value < 0) return;
                if (value > 100) return;

                curWarningPoints = value;
                //Log.Message($"警报点数：警报点数更新，当前警报点数为：{curWarningPoints}");

                Util.MusicUtils.PlayMusic_LC(curWarningPoints);
            }
        }

        private int curWarningPoints;

        private int waringPointsCounter;

        public const string CoreSuppression_Malkuth = "CoreSuppression_Malkuth";
        public const string CoreSuppression_Yesod = "CoreSuppression_Yesod";
        public const string CoreSuppression_Hod = "CoreSuppression_Hod";
        public const string CoreSuppression_Netzach = "CoreSuppression_Netzach";
        public const string CoreSuppression_Gebura = "CoreSuppression_Gebura";
        public const string CoreSuppression_Binah = "CoreSuppression_Binah";

        private HashSet<string> discoveredAbnormalities = new HashSet<string>();

        private Dictionary<int, float> blessedWeaponBonusDict = new Dictionary<int, float>();

        private HashSet<int> autoCastEgoWeaponIds = new HashSet<int>();

        private HashSet<string> completedCoreSuppressions = new HashSet<string>();
        private bool suppressionHealLocked;

        private int managerBulletCharges;
        private int managerBulletRechargeCounter;
        private bool managerBulletsInitialized;

        public static GameComponent_LC Instance => Current.Game?.GetComponent<GameComponent_LC>();

        /// <summary>当前是否处于安保部核心抑制的再生器封锁期间。</summary>
        public bool SuppressionHealLocked
        {
            get => suppressionHealLocked;
            set => suppressionHealLocked = value;
        }

        /// <summary>检查指定的核心抑制永久强化是否已完成。</summary>
        public bool HasCoreSuppression(string suppressionId)
        {
            return !string.IsNullOrEmpty(suppressionId)
                && completedCoreSuppressions != null
                && completedCoreSuppressions.Contains(suppressionId);
        }

        /// <summary>永久激活指定的核心抑制强化并刷新所有在线员工的部门增益。</summary>
        public bool CompleteCoreSuppression(string suppressionId)
        {
            if (string.IsNullOrEmpty(suppressionId))
            {
                return false;
            }

            completedCoreSuppressions ??= new HashSet<string>();
            completedCoreSuppressions.Add(suppressionId);

            if (Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                    {
                        Hediff h = pawn.health?.hediffSet?.GetFirstHediffOfDef(Defs.HediffDefOf.LC_PawnStatus);
                        (h as Hediffs.HediffComp_PawnStatus)?.RefreshStatus();
                    }
                }
            }

            return true;
        }

        /// <summary>获取指定地图当前可用的主管战术子弹充能次数。</summary>
        public int GetManagerBulletCharges(Map map)
        {
            int max = Util.ManagerBulletUtility.GetMaxBulletCharges(map);
            if (max <= 0)
            {
                return 0;
            }

            if (!managerBulletsInitialized)
            {
                managerBulletCharges = max;
                managerBulletsInitialized = true;
            }

            if (managerBulletCharges > max)
            {
                managerBulletCharges = max;
            }

            return managerBulletCharges;
        }

        /// <summary>尝试消耗 1 发主管战术子弹。</summary>
        public bool TryConsumeManagerBullet(Map map)
        {
            int cur = GetManagerBulletCharges(map);
            if (cur <= 0)
            {
                return false;
            }

            managerBulletCharges = cur - 1;
            return true;
        }

        /// <summary>将主管战术子弹充能补满至当前地图上限。</summary>
        public void RefillManagerBullets(Map map)
        {
            int max = Util.ManagerBulletUtility.GetMaxBulletCharges(map);
            managerBulletCharges = max;
            managerBulletsInitialized = max > 0;
            managerBulletRechargeCounter = 0;
        }

        public bool IsWeaponBlessed(Thing weapon)
        {
            return weapon != null
                && blessedWeaponBonusDict != null
                && blessedWeaponBonusDict.TryGetValue(weapon.thingIDNumber, out float mult)
                && mult > 1.0001f;
        }

        public float GetWeaponBlessingMultiplier(Thing weapon)
        {
            if (weapon != null
                && blessedWeaponBonusDict != null
                && blessedWeaponBonusDict.TryGetValue(weapon.thingIDNumber, out float mult)
                && mult > 1f)
            {
                return mult;
            }

            return 1f;
        }

        public void SetWeaponBlessing(Thing weapon, float multiplier)
        {
            if (weapon == null) return;
            blessedWeaponBonusDict ??= new Dictionary<int, float>();
            if (multiplier <= 1.0001f)
            {
                blessedWeaponBonusDict.Remove(weapon.thingIDNumber);
            }
            else
            {
                blessedWeaponBonusDict[weapon.thingIDNumber] = Mathf.Clamp(multiplier, 1f, 3f);
            }
        }

        public void RemoveWeaponBlessing(Thing weapon)
        {
            if (weapon == null || blessedWeaponBonusDict == null) return;
            blessedWeaponBonusDict.Remove(weapon.thingIDNumber);
        }

        public bool IsEgoWeaponAutoCastEnabled(Thing weapon)
        {
            return weapon != null
                && autoCastEgoWeaponIds != null
                && autoCastEgoWeaponIds.Contains(weapon.thingIDNumber);
        }

        public void SetEgoWeaponAutoCast(Thing weapon, bool enabled)
        {
            if (weapon == null)
            {
                return;
            }

            autoCastEgoWeaponIds ??= new HashSet<int>();
            if (enabled)
            {
                autoCastEgoWeaponIds.Add(weapon.thingIDNumber);
            }
            else
            {
                autoCastEgoWeaponIds.Remove(weapon.thingIDNumber);
            }
        }

        public bool ToggleEgoWeaponAutoCast(Thing weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            bool nextState = !IsEgoWeaponAutoCastEnabled(weapon);
            SetEgoWeaponAutoCast(weapon, nextState);
            return nextState;
        }

        public void DiscoverAbnormality(ThingDef thingDef)
        {
            if (thingDef == null) return;
            foreach (var entry in DefDatabase<Defs.AbnormalityCodexEntryDef>.AllDefsListForReading)
                if (entry.defName == thingDef.defName || entry.linkedThings.Contains(thingDef))
                    discoveredAbnormalities.Add(entry.defName);
        }

        public bool IsAbnormalityDiscovered(Defs.AbnormalityCodexEntryDef entry)
        {
            return entry != null && discoveredAbnormalities.Contains(entry.defName);
        }

        public void SetAbnormalityDiscovered(Defs.AbnormalityCodexEntryDef entry, bool discovered)
        {
            if (entry == null) return;
            if (discovered) discoveredAbnormalities.Add(entry.defName);
            else discoveredAbnormalities.Remove(entry.defName);
        }

        /// <summary>表示 <c>LCGameComponentTickEvent</c>。</summary>
        public LCGameComponentTick LCGameComponentTickEvent;

        /// <summary>
        /// 异想体独立数据字典
        /// </summary>
        public Dictionary<ThingDef, AnomalyStatusSaved> AnomalyStatusSavedDict
        {
            get
            {
                anomalyStatusSavedDict ??= new Dictionary<ThingDef, AnomalyStatusSaved>();
                return anomalyStatusSavedDict;
            }
        }

        /// <summary>表示 <c>anomalyStatusSavedDict</c>。</summary>
        protected Dictionary<ThingDef, AnomalyStatusSaved> anomalyStatusSavedDict;

        /// <summary>初始化 <c>GameComponent_LC</c> 类的新实例。</summary>
        public GameComponent_LC(Game game)
        {
        }

        /// <inheritdoc />
        public override void StartedNewGame()
        {
            base.StartedNewGame();
            Render.Weapon.EgoWeaponAnimationManager.ClearAll();
        }

        /// <inheritdoc />
        public override void LoadedGame()
        {
            base.LoadedGame();
            Render.Weapon.EgoWeaponAnimationManager.ClearAll();
        }

        /// <summary>
        /// Tick
        /// </summary>
        public override void GameComponentTick()
        {
            Render.Weapon.EgoWeaponAnimationManager.TickAll();
            WarningOverlay.Tick();
            LCGameComponentTickEvent?.Invoke();

            TickManagerBulletRecharge();

            //如果启用警报系统，就自动更新警报点数
            if (Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarning)
            {
                //警报点数大于0时，才能降低点数
                if (CurWarningPoints > 0)
                {
                    //每xTick降低y点警报点数
                    waringPointsCounter++;
                    if (waringPointsCounter >= Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_FadeDuration)
                    {
                        CurWarningPoints -= Setting_LCAnomalyCore_Main.Settings.PointsOfWarning_FadeAmount;
                        waringPointsCounter = 0;
                    }
                    //计时器值不得小于0
                    else if (waringPointsCounter < 0)
                    {
                        waringPointsCounter = 0;
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void ExposeData()
        {
            Scribe_Values.Look(ref curWarningPoints, "curWarningPoints", 0);
            Scribe_Values.Look(ref waringPointsCounter, "waringPointsCounter", 0);
            Scribe_Collections.Look(ref anomalyStatusSavedDict, "anomalyStatusSavedDict", LookMode.Def, LookMode.Deep);
            Scribe_Collections.Look(ref discoveredAbnormalities, "lcDiscoveredAbnormalities", LookMode.Value);
            Scribe_Collections.Look(ref blessedWeaponBonusDict, "lcBlessedWeaponBonusDict", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref autoCastEgoWeaponIds, "lcAutoCastEgoWeaponIds", LookMode.Value);
            Scribe_Collections.Look(ref completedCoreSuppressions, "lcCompletedCoreSuppressions", LookMode.Value);
            Scribe_Values.Look(ref suppressionHealLocked, "lcSuppressionHealLocked", false);
            Scribe_Values.Look(ref managerBulletCharges, "lcManagerBulletCharges", 0);
            Scribe_Values.Look(ref managerBulletRechargeCounter, "lcManagerBulletRechargeCounter", 0);
            Scribe_Values.Look(ref managerBulletsInitialized, "lcManagerBulletsInitialized", false);
            discoveredAbnormalities ??= new HashSet<string>();
            blessedWeaponBonusDict ??= new Dictionary<int, float>();
            autoCastEgoWeaponIds ??= new HashSet<int>();
            completedCoreSuppressions ??= new HashSet<string>();
        }

        private void TickManagerBulletRecharge()
        {
            if (Find.Maps == null || Find.Maps.Count == 0)
            {
                return;
            }

            int maxAcrossColony = 0;
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                int mapMax = Util.ManagerBulletUtility.GetMaxBulletCharges(Find.Maps[i]);
                if (mapMax > maxAcrossColony)
                {
                    maxAcrossColony = mapMax;
                }
            }

            if (maxAcrossColony <= 0)
            {
                return;
            }

            if (!managerBulletsInitialized)
            {
                managerBulletCharges = maxAcrossColony;
                managerBulletsInitialized = true;
                return;
            }

            if (managerBulletCharges >= maxAcrossColony)
            {
                managerBulletCharges = maxAcrossColony;
                managerBulletRechargeCounter = 0;
                return;
            }

            int rechargeInterval = HasCoreSuppression(CoreSuppression_Binah)
                ? Util.ManagerBulletUtility.BinahBulletRechargeIntervalTicks
                : Util.ManagerBulletUtility.BulletRechargeIntervalTicks;
            managerBulletRechargeCounter++;
            if (managerBulletRechargeCounter >= rechargeInterval)
            {
                managerBulletRechargeCounter = 0;
                managerBulletCharges++;
            }
        }

        /// <summary>执行 <c>TryGetAnomalyStatusSaved</c> 定义的操作。</summary>
        public void TryGetAnomalyStatusSaved(ThingDef key, out AnomalyStatusSaved saved)
        {
            if (!AnomalyStatusSavedDict.ContainsKey(key))
            {
                //Log.Warning($"LC组件：AnomalyStatusSavedDict中未找到对应的ThingDef Key：{key.defName}，将新建一个");
                AnomalyStatusSavedDict.Add(key, new AnomalyStatusSaved(0, 0, 0, 0));
            }

            saved = AnomalyStatusSavedDict[key];
        }
    }

    /// <summary>
    /// 异想体独立数据
    /// </summary>
    public struct AnomalyStatusSaved : IExposable
    {
        /// <summary>
        /// 已提取EGO武器数量
        /// </summary>
        public int ExtractedEgoWeaponAmount
        {
            get => extractedEgoWeaponAmount;
            set
            {
                if (value < 0)
                    return;

                extractedEgoWeaponAmount = value;
            }
        }

        private int extractedEgoWeaponAmount = 0;

        /// <summary>
        /// 已提取EGO护甲数量
        /// </summary>
        public int ExtractedEgoArmorAmount
        {
            get => extractedEgoArmorAmount;
            set
            {
                if (value < 0)
                    return;

                extractedEgoArmorAmount = value;
            }
        }

        private int extractedEgoArmorAmount = 0;

        /// <summary>
        /// 已提取独立PeBox数量
        /// </summary>
        public int IndiPeBoxAmount
        {
            get => indiPeBoxAmount;
            set
            {
                if (value < 0)
                    return;

                indiPeBoxAmount = value;
            }
        }

        private int indiPeBoxAmount = 0;

        /// <summary>
        /// 研究进度
        /// </summary>
        public int StudyProgress
        {
            get => studyProgress;
            set
            {
                if (value < 0)
                    return;

                studyProgress = value;
            }
        }

        private int studyProgress = 0;

        /// <summary>初始化 <c>AnomalyStatusSaved</c> 类的新实例。</summary>
        public AnomalyStatusSaved(int extractedEgoWeaponAmount, int extractedEgoArmorAmount, int indiPeBoxAmount, int studyProgress)
        {
            ExtractedEgoWeaponAmount = extractedEgoWeaponAmount;
            ExtractedEgoArmorAmount = extractedEgoArmorAmount;
            IndiPeBoxAmount = indiPeBoxAmount;
            StudyProgress = studyProgress;
        }

        /// <summary>执行 <c>ExposeData</c> 定义的操作。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref extractedEgoWeaponAmount, "extractedEgoWeaponAmount", 0);
            Scribe_Values.Look(ref extractedEgoArmorAmount, "extractedEgoArmorAmount", 0);
            Scribe_Values.Look(ref indiPeBoxAmount, "indiPeBoxAmount", 0);
            Scribe_Values.Look(ref studyProgress, "studyProgress", 0);
        }
    }
}
