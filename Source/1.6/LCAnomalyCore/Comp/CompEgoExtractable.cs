using LCAnomalyCore.Defs;
using LCAnomalyCore.GameComponent;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompEgoExtractable : ThingComp
    {
        #region 字段

        /// <summary>
        /// CompProperties
        /// </summary>
        public CompProperties_EgoExtractable Props => (CompProperties_EgoExtractable)props;

        /// <summary>
        /// CompEntity
        /// </summary>
        public CompAbnormality EntityComp => parent.GetComp<CompAbnormality>();

        /// <summary>
        /// 提取所需PeBox的Def
        /// </summary>
        protected CompPeBoxProduce PeBoxComp => parent.GetComp<CompPeBoxProduce>();

        /// <summary>
        /// CompHoldingPlatformTarget
        /// </summary>
        protected CompAbnormalityHoldingPlatformTarget HoldingTargetComp => parent.GetComp<CompAbnormalityHoldingPlatformTarget>();

        protected GameComponent_LC component => Current.Game.GetComponent<GameComponent_LC>();

        /// <summary>
        /// 当前已被提取的EGO武器数量
        /// </summary>
        public int CurAmountWeapon
        {
            get
            {
                component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                return saved.ExtractedEgoWeaponAmount;
            }
            set
            {
                component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                saved.ExtractedEgoWeaponAmount = value;
                component.AnomalyStatusSavedDict[parent.def] = saved;
            }
        }

        /// <summary>
        /// 当前已被提取的EGO护甲数量
        /// </summary>
        public int CurAmountArmor
        {
            get
            {
                component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                return saved.ExtractedEgoArmorAmount;
            }
            set
            {
                component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                saved.ExtractedEgoArmorAmount = value;
                component.AnomalyStatusSavedDict[parent.def] = saved;
            }
        }

        #endregion 字段

        #region 工具方法

        /// <summary>
        /// EGO提取的PeBox消耗
        /// </summary>
        /// <returns></returns>
        protected virtual bool ConsumePebox(EGO_TYPE type)
        {
            //如果没有PeBoxComp，就不能提取
            if (PeBoxComp == null)
            {
                //Log.Warning($"提取EGO：{parent.def.label.Translate()}没有PeBoxComp，无法提取");
                return false;
            }

            //如果PeBoxComp没有PeBoxDef，就不能提取
            var peBoxDef = PeBoxComp.Props.peBoxDef;
            if (peBoxDef == null)
            {
                //Log.Warning($"提取EGO：{PeBoxComp.ToString()}没有PeBoxDef，无法提取");
                return false;
            }

            //消耗独立PeBox
            int delta = type == EGO_TYPE.Weapon ? Props.weaponExtractedNeed : Props.armorExtractedNeed;
            if (delta <= PeBoxComp.CurAmountIndiPebox)
            {
                PeBoxComp.CurAmountIndiPebox -= delta;
                return true;
            }
            else
            {
                //Log.Warning($"提取EGO：{parent.Label.Translate()}的独立pebox数量不足，无法提取");
                return false;
            }
        }

        /// <summary>
        /// EGO提取检查
        /// </summary>
        /// <param name="type">EGO类型</param>
        /// <returns></returns>
        protected virtual bool CheckIfExtractEGO(EGO_TYPE type)
        {
            if (!HasReachedMaxExtractAmount(type))
            {
                return ConsumePebox(type);
            }

            return false;
        }

        /// <summary>
        /// 提取EGO
        /// </summary>
        /// <param name="type">EGO类型</param>
        protected virtual void ExtractEGO(EGO_TYPE type)
        {
            if (CheckIfExtractEGO(type))
            {
                ThingDef ego;

                switch (type)
                {
                    case EGO_TYPE.Weapon:
                        ego = Props.weaponExtracted;
                        CurAmountWeapon++;
                        break;

                    case EGO_TYPE.Armor:
                        ego = Props.armorExtracted;
                        CurAmountArmor++;
                        break;

                    default:
                        return;
                }

                Thing thing = ThingMaker.MakeThing(ego);
                thing.stackCount = 1;
                GenPlace.TryPlaceThing(thing, parent.PositionHeld, parent.MapHeld, ThingPlaceMode.Near);
                Messages.Message("EGOExtractSucceedText".Translate() + thing.def.label.Translate(), MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Messages.Message("EGOExtractFailedByPeBoxConsumeText".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>
        /// EGO提取是否已解锁
        /// </summary>
        /// <returns></returns>
        protected virtual bool CheckIfEGOExtractUnlocked()
        {
            //没有收容和研究组件固定不允许提取
            if (HoldingTargetComp == null || EntityComp.StudyUnlocksComp == null)
                return false;

            //逃离中不允许提取
            if (HoldingTargetComp.isEscaping)
                return false;

            //到达设定的解锁等级后才允许提取
            return EntityComp.StudyUnlocksComp.Progress >= Props.unlockLevel;
        }

        /// <summary>
        /// E.G.O是否已经达到提取最大量
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        protected virtual bool HasReachedMaxExtractAmount(EGO_TYPE type)
        {
            switch (type)
            {
                case EGO_TYPE.Weapon:
                    return CurAmountWeapon >= Props.amountMaxWeapon;

                case EGO_TYPE.Armor:
                    return CurAmountArmor >= Props.amountMaxArmor;

                default:
                    //Log.Error("提取EGO:EGO类型不存在");
                    return true;
            }
        }

        #endregion 工具方法

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

            //在收容平台上才能提取
            if (HoldingTargetComp != null && HoldingTargetComp.HeldPlatform != null)
            {
                //科技研究完成后才能提取
                if (CheckIfEGOExtractUnlocked())
                {
                    component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);

                    //提取EGO武器
                    yield return new Command_Action
                    {
                        icon = ContentFinder<UnityEngine.Texture2D>.Get(Props.weaponIconPath),
                        defaultLabel = "ExtractEGOWeaponCommandText".Translate(),
                        defaultDesc = $"{"ExtractEGOCommandText".Translate()}{CurAmountWeapon}/{Props.amountMaxWeapon}"
                        + $"\n{"ExtractEGONeedPeboxAmountCommandText".Translate()}{Props.weaponExtractedNeed}"
                        + $"\n{"IndiPeBoxAmountCommandText".Translate()}{saved.IndiPeBoxAmount}",
                        Disabled = HasReachedMaxExtractAmount(EGO_TYPE.Weapon),
                        disabledReason = "ExtractEGODisabledReasonText".Translate(),
                        action = delegate
                        {
                            ExtractEGO(EGO_TYPE.Weapon);
                        }
                    };

                    //提取EGO装备
                    yield return new Command_Action
                    {
                        icon = ContentFinder<UnityEngine.Texture2D>.Get(Props.armorIconPath),
                        defaultLabel = "ExtractEGOArmorCommandText".Translate(),
                        defaultDesc = $"{"ExtractEGOCommandText".Translate()}{CurAmountArmor}/{Props.amountMaxArmor}"
                        + $"\n{"ExtractEGONeedPeboxAmountCommandText".Translate()}{Props.armorExtractedNeed}"
                        + $"\n{"IndiPeBoxAmountCommandText".Translate()}{saved.IndiPeBoxAmount}",
                        Disabled = HasReachedMaxExtractAmount(EGO_TYPE.Armor),
                        disabledReason = "ExtractEGODisabledReasonText".Translate(),
                        action = delegate
                        {
                            ExtractEGO(EGO_TYPE.Armor);
                        }
                    };
                }
            }
        }

        #endregion UI
    }
}