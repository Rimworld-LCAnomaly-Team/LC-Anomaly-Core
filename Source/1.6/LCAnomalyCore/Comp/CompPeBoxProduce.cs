using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Util;
using System;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC可产PeBox的基类Comp
    /// </summary>
    public class CompPeBoxProduce : ThingComp
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public CompProperties_PeBoxProduce Props => (CompProperties_PeBoxProduce)props;

        protected GameComponent_LC component => Components.LC;

        /// <summary>
        /// 当前独立PeBox数量
        /// </summary>
        public int CurAmountIndiPebox
        {
            get
            {
                component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                return saved.IndiPeBoxAmount;
            }
            set
            {
                component.TryGetAnomalyStatusSaved(parent.def, out AnomalyStatusSaved saved);
                saved.IndiPeBoxAmount = value;
                component.AnomalyStatusSavedDict[parent.def] = saved;

                OnContentsChanged?.Invoke();
            }
        }

        /// <summary>
        /// 检查生成Pebox
        /// </summary>
        /// <param name="studier">研究者</param>
        /// <param name="amount">生产量</param>
        public virtual void CheckSpawnPeBox(Pawn studier, int amount)
        {
            if (studier != null)
            {
                if (amount <= 0) return;

                if (Props.peBoxDef != null)
                {
                    CurAmountIndiPebox += amount;
                    //Log.Message($"工作：{parent.def.label.Translate()}生成了{amount}单位的独立PeBox");

                    Thing thing = ThingMaker.MakeThing(Props.peBoxDef);
                    thing.stackCount = amount;
                    GenSpawn.Spawn(thing, studier.Position, studier.Map);
                    LogUtil.Message($"Study: {parent.def.label.Translate()} spawned {amount} PeBox");
                }
            }
        }

        /// <summary>
        /// 值发生变化时调用
        /// </summary>
        public event Action OnContentsChanged;

        public override IEnumerable<Verse.Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "IndiPebox + 10",
                    action = delegate
                    {
                        LogUtil.Warning($"Dev：{parent.def.label.Translate()}'s Indi-PeBox +20");
                        CurAmountIndiPebox += 10;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "IndiPebox - 10",
                    action = delegate
                    {
                        LogUtil.Warning($"Dev：{parent.def.label.Translate()}'s Indi-PeBox -20");
                        CurAmountIndiPebox -= 10;
                    }
                };
            }
        }
    }
}