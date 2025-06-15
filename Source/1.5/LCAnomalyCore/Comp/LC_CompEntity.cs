using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Settings;
using LCAnomalyCore.Util;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC基础实体Comp
    /// </summary>
    public class LC_CompEntity : ThingComp
    {
        #region 变量

        /// <summary>
        /// CompProperties
        /// </summary>
        public LC_CompProperties_Entity Props => (LC_CompProperties_Entity)props;

        /// <summary>
        /// 研究阶段解锁
        /// </summary>
        public CompStudyUnlocks StudyUnlocksComp
        {
            get
            {
                if (studyUnlocksComp == null)
                    return studyUnlocksComp = parent.GetComp<CompStudyUnlocks>();
                else
                    return studyUnlocksComp;
            }
        }

        protected CompStudyUnlocks studyUnlocksComp;

        /// <summary>
        /// 研究组件
        /// </summary>
        public LC_CompStudiable StudiableComp
        {
            get
            {
                if (studiableComp == null)
                    return studiableComp = parent.GetComp<LC_CompStudiable>();
                else
                    return studiableComp;
            }
        }

        protected LC_CompStudiable studiableComp;

        /// <summary>
        /// 首饰组件
        /// </summary>
        public CompAccessoryable AccessoryableComp
        {
            get
            {
                if (studyUnlocksComp == null)
                    return accessoryableComp = parent.GetComp<CompAccessoryable>();
                else
                    return accessoryableComp;
            }
        }

        protected CompAccessoryable accessoryableComp;

        /// <summary>
        /// PeBox组件
        /// </summary>
        public LC_CompPeBoxProduce PeBoxComp
        {
            get
            {
                if (peBoxComp == null)
                    return peBoxComp = parent.GetComp<LC_CompPeBoxProduce>();
                else
                    return peBoxComp;
            }
        }

        protected LC_CompPeBoxProduce peBoxComp;

        /// <summary>
        /// 收容控制组件
        /// </summary>
        public CompHoldingPlatformTarget HoldingPlaformTargetComp
        {
            get
            {
                if (holdingPlatformTargetComp == null)
                    return holdingPlatformTargetComp = parent.GetComp<CompHoldingPlatformTarget>();
                else
                    return holdingPlatformTargetComp;
            }
        }

        protected CompHoldingPlatformTarget holdingPlatformTargetComp;

        /// <summary>
        /// 生物特征
        /// </summary>
        public int biosignature;

        /// <summary>
        /// XML引用：逆卡巴拉计数器最大值
        /// </summary>
        public int QliphothCountMax => Props.qliphothCountMax;

        /// <summary>
        /// 逆卡巴拉机制是否启用
        /// </summary>
        public bool QliphothEnabled => QliphothCountMax > 0;

        /// <summary>
        /// XML引用：警报点数
        /// </summary>
        public int WarningPoints
        {
            get
            {
                //强制不提供警报点数就返回0
                if (!Props.ifProvideWarningPoints)
                {
                    //Log.Message($"警报点数：{parent.def.label.Translate()}不提供警报点数");
                    return 0;
                }

                //如果警报点数大于0就返回警报点数，否则返回等级对应的点数
                if (Props.customWarningPoints > 0)
                {
                    //Log.Message($"警报点数：{parent.def.label.Translate()}提供自定义警报点数{Props.customWarningPoints}点");
                    return Props.customWarningPoints;
                }
                else
                {
                    var points = MusicUtils.LevelTag2Points(parent.def.entityCodexEntry.category.defName);
                    //Log.Message($"警报点数：{parent.def.label.Translate()}提供根据等级的警报点数{points}点");

                    return points;
                }
            }
        }

        /// <summary>
        /// 逆卡巴拉计数器当前值
        /// </summary>
        public int QliphothCountCurrent
        {
            get
            {
                //无逆卡巴拉值的情况下只会返回-1
                if (!QliphothEnabled)
                    return -1;

                return qliphothCountCurrent;
            }
            set
            {
                //无逆卡巴拉值的情况下修改无效
                if (!QliphothEnabled)
                    return;

                //值相同则修改无效
                if (qliphothCountCurrent == value)
                    return;

                //小于0强制归零，大于最大值时若当前值已经异常就强制归最大，其他情况正常变化
                if (value <= 0)
                {
                    qliphothCountCurrent = 0;
                    //Log.Message($"逆卡巴拉计数：{parent.def.defName} 的逆卡巴拉计数器变化，变为：0");
                    QliphothMeltdown();
                }
                else if (value > Props.qliphothCountMax)
                {
                    qliphothCountCurrent = Props.qliphothCountMax;
                }
                else
                {
                    qliphothCountCurrent = value;
                    //Log.Message($"逆卡巴拉计数：{parent.def.defName} 的逆卡巴拉计数器变化，变为：{QliphothCountCurrent}");
                }
            }
        }

        private int qliphothCountCurrent;

        /// <summary>
        /// 生物特征名
        /// </summary>
        public string BiosignatureName => biosignatureName ?? (biosignatureName = AnomalyUtility.GetBiosignatureName(biosignature));

        private string biosignatureName;

        /// <summary>
        /// 是否已出逃
        /// </summary>
        public bool Escaped => escaped;

        protected bool escaped = false;

        protected int PeBoxProducedTemp = 0;
        protected int NeBoxProducedTemp = 0;

        #endregion 变量

        #region 触发事件

        /// <summary>
        /// PostPostMake初始化
        /// </summary>
        public override void PostPostMake()
        {
            base.PostPostMake();

            //初始化生物特征和逆卡巴拉计数
            biosignature = Rand.Int;
            QliphothCountCurrent = Props.qliphothCountMax;
        }

        /// <summary>
        /// 逃脱收容后执行的操作
        /// </summary>
        public virtual void Notify_Escaped()
        {
            //避免重复出逃
            if (escaped)
                return;
            else
                escaped = true;

            //如果出逃提醒就弹出逃信封和计算警报点数
            if (Props.shouldNotifyWhenEscape)
            {
                //TODO 这个写的不对，需要研究如何格式化
                Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter("LetterLabelEscapingFromHoldingPlatform".Translate(),
                    "LetterEscapingFromHoldingPlatform", LetterDefOf.ThreatBig));

                //如果未启用警报系统，就不更新警报点数
                if (Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarning)
                {
                    if (Components.LC != null)
                    {
                        Components.LC.CurWarningPoints += WarningPoints;
                    }
                    else
                    {
                        Log.Warning("GameComponent_LC is null");
                    }
                }
            }
        }

        /// <summary>
        /// 被研究后执行的操作
        /// </summary>
        public virtual void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            //优秀
            if (PeBoxProducedTemp > PeBoxComp.Props.amountProdueRangeNormal.max)
            {
                StudyEvent_Good(studier);
            }
            //差
            else if (PeBoxProducedTemp < PeBoxComp.Props.amountProdueRangeNormal.min)
            {
                StudyEvent_Bad(studier);
            }
            //良好
            else
            {
                StudyEvent_Normal(studier);
            }

            //清空缓存数量
            PeBoxProducedTemp = 0;
            NeBoxProducedTemp = 0;
        }

        /// <summary>
        /// 正在被研究时执行的操作
        /// </summary>
        public virtual void Notify_Studying(Pawn studier)
        {
        }

        public virtual bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            StudierExpCalculate(studier, workType);

            float rate = StudySuccessRateCalculate(studier, workType);
            if (Rand.Chance(rate))
            {
                LogUtil.Message($"Study success，final success rate：{rate}");
                PeBoxProducedTemp++;
                return true;
            }
            else
            {
                LogUtil.Warning($"Study failure，final success rate：{rate}");
                NeBoxProducedTemp++;
                return false;
            }
        }

        /// <summary>
        /// 绑到收容平台上的操作
        /// </summary>
        public virtual void Notify_Holded()
        {
        }

        /// <summary>
        /// 研究质量：优秀
        /// </summary>
        /// <param name="studier">研究者</param>
        protected virtual void StudyEvent_Good(Pawn studier)
        {
            PeBoxComp?.CheckSpawnPeBox(studier, PeBoxProducedTemp);
            StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Good);
            AccessoryableComp?.CheckGiveAccessory(studier);

            QliphothCountCurrent++;
        }

        /// <summary>
        /// 研究质量：良好
        /// </summary>
        /// <param name="studier">研究者</param>
        protected virtual void StudyEvent_Normal(Pawn studier)
        {
            PeBoxComp?.CheckSpawnPeBox(studier, PeBoxProducedTemp);
            StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Normal);
            AccessoryableComp?.CheckGiveAccessory(studier);
        }

        /// <summary>
        /// 研究质量：差
        /// </summary>
        /// <param name="studier">研究者</param>
        protected virtual void StudyEvent_Bad(Pawn studier)
        {
            PeBoxComp?.CheckSpawnPeBox(studier, PeBoxProducedTemp);
            StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Bad);
            AccessoryableComp?.CheckGiveAccessory(studier);

            QliphothCountCurrent--;
        }

        /// <summary>
        /// 逆卡巴拉熔毁事件
        /// </summary>
        protected virtual void QliphothMeltdown()
        {
            //Log.Message($"收容：{parent.def.label.Translate()} 的收容单元发生了熔毁");
            HoldingPlaformTargetComp.Escape(initiator: true);
        }

        #endregion 触发事件

        #region 生命周期

        /// <summary>
        /// 我不知道这是什么，好像和数据保存有关
        /// </summary>
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref biosignature, "biosignature", 0);
            Scribe_Values.Look(ref escaped, "escaped", defaultValue: false);
            Scribe_Values.Look(ref qliphothCountCurrent, "qliphothCountCurrent", defaultValue: QliphothCountMax);

            Scribe_Values.Look(ref PeBoxProducedTemp, "PeBoxProducedTemp");
            Scribe_Values.Look(ref NeBoxProducedTemp, "NeBoxProducedTemp");
        }

        #endregion 生命周期

        #region 工具功能

        /// <summary>
        /// 获取研究成功率
        /// </summary>
        /// <param name="studier">研究者</param>
        /// <param name="workType">工作类型</param>
        /// <returns>研究成功率</returns>
        protected virtual float StudySuccessRateCalculate(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            //默认基本成功率为 每点正义值x0.2%
            float baseRate = studier.GetPawnStatusLevel(EPawnStatus.Justice).Status * 0.002f;
            //观察等级解锁的成功率
            float unlockRate = StudiableComp.GetWorkSuccessRateOffset();

            return baseRate + unlockRate;
        }

        /// <summary>
        /// 工作后的数值经验增长
        /// </summary>
        /// <param name="studier"></param>
        /// <param name="workType"></param>
        protected virtual void StudierExpCalculate(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            float value = StudyUtil.GetPawnStatusIncreaseValue(studier, workType, parent.def.entityCodexEntry.category.defName);
            value *= 0.05f;

            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    studier.GetPawnStatusLevel(EPawnStatus.Fortitude).Exp += value;
                    break;

                case EAnomalyWorkType.Attachment:
                    studier.GetPawnStatusLevel(EPawnStatus.Temperance).Exp += value;
                    break;

                case EAnomalyWorkType.Insight:
                    studier.GetPawnStatusLevel(EPawnStatus.Prudence).Exp += value;
                    break;

                case EAnomalyWorkType.Repression:
                    studier.GetPawnStatusLevel(EPawnStatus.Justice).Exp += value;
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Unknown EAnomalyWorkType");
            }
        }

        /// <summary>
        /// 检查研究者研究成功率百分比
        /// </summary>
        /// <param name="studier">研究者</param>
        /// <param name="workType">工作类型</param>
        /// <returns></returns>
        public virtual float CheckStudierSkillRequire(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            return StudySuccessRateCalculate(studier, workType);
        }

        /// <summary>
        /// Debug：调试用逆卡巴拉强制熔毁
        /// </summary>
        protected void ForceQliphothMeltdown()
        {
            QliphothCountCurrent = 0;
        }

        #endregion 工具功能

        #region UI

        /// <summary>
        /// Gizmos
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Verse.Gizmo> CompGetGizmosExtra()
        {
            foreach (Verse.Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "kill",
                    action = delegate
                    {
                        parent.Kill(null);
                    }
                };

                //逆卡巴拉值有效情况
                if (QliphothEnabled)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "Force Meltdown",
                        action = delegate
                        {
                            LogUtil.Warning($"Dev：{parent.def.label.Translate()} 的收容单元发生了强制熔毁");
                            ForceQliphothMeltdown();
                        }
                    };

                    yield return new Command_Action
                    {
                        defaultLabel = "QliphothCount +1",
                        action = delegate
                        {
                            LogUtil.Warning($"Dev：{parent.def.label.Translate()} 的逆卡巴拉计数器上升了1点");
                            QliphothCountCurrent++;
                        }
                    };

                    yield return new Command_Action
                    {
                        defaultLabel = "QliphothCount -1",
                        action = delegate
                        {
                            LogUtil.Warning($"Dev：{parent.def.label.Translate()} 的逆卡巴拉计数器下降了1点");
                            QliphothCountCurrent--;
                        }
                    };
                }

                //yield return new Command_Action
                //{
                //    defaultLabel = "show text",
                //    action = delegate
                //    {
                //        Log.Warning($"Dev：Show screen text");
                //        LCCanvasSingleton.Instance.ShowText("s2as2s3sss32321ssssssssss1231ssssssssss1231sssssssssss");
                //    }
                //};
            }
        }

        #endregion UI
    }
}