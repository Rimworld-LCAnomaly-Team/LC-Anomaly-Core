using LCAnomalyCore.Buildings;
using LCAnomalyCore.Settings;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.GameComponent
{
    public delegate void LCGameComponentTick();

    /// <summary>
    /// LC游戏组件
    /// </summary>
    public class GameComponent_LC : Verse.GameComponent
    {
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

        protected Dictionary<ThingDef, AnomalyStatusSaved> anomalyStatusSavedDict;

        public GameComponent_LC(Game game)
        {
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
        }

        /// <summary>
        /// Tick
        /// </summary>
        public override void GameComponentTick()
        {
            LCGameComponentTickEvent?.Invoke();

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

        public override void ExposeData()
        {
            Scribe_Values.Look(ref curWarningPoints, "curWarningPoints", 0);
            Scribe_Values.Look(ref waringPointsCounter, "waringPointsCounter", 0);
            Scribe_Collections.Look(ref anomalyStatusSavedDict, "anomalyStatusSavedDict", LookMode.Def, LookMode.Deep);
        }

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

        public AnomalyStatusSaved(int extractedEgoWeaponAmount, int extractedEgoArmorAmount, int indiPeBoxAmount, int studyProgress)
        {
            ExtractedEgoWeaponAmount = extractedEgoWeaponAmount;
            ExtractedEgoArmorAmount = extractedEgoArmorAmount;
            IndiPeBoxAmount = indiPeBoxAmount;
            StudyProgress = studyProgress;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref extractedEgoWeaponAmount, "extractedEgoWeaponAmount", 0);
            Scribe_Values.Look(ref extractedEgoArmorAmount, "extractedEgoArmorAmount", 0);
            Scribe_Values.Look(ref indiPeBoxAmount, "indiPeBoxAmount", 0);
            Scribe_Values.Look(ref studyProgress, "studyProgress", 0);
        }
    }
}