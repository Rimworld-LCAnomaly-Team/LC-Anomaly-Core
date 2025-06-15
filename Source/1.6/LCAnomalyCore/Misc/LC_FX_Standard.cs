using System;
using Verse;

namespace LCAnomalyCore.Misc
{
    /// <summary>
    /// LC标准基本效果类
    /// </summary>
    public abstract class LC_FX_Standard : Thing
    {
        #region 字段

        /// <summary>
        /// 生物特征
        /// </summary>
        protected int bioSignature;

        /// <summary>
        /// 完成所需Tick
        /// </summary>
        protected int completeTick;

        /// <summary>
        /// 是否是逃脱效果
        /// </summary>
        protected bool isEscaping;

        /// <summary>
        /// 是否已经初始化过
        /// </summary>
        protected bool hasInited;

        #endregion 字段

        #region 生命周期

        /// <summary>
        /// 每Tick执行
        /// </summary>
        protected override void Tick()
        {
            //动画播完就执行操作
            if (Find.TickManager.TicksGame >= completeTick)
            {
                Complete();
            }
        }

        /// <summary>
        /// 倒计时结束后执行
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public virtual void Complete()
        {
            if (hasInited)
                throw new NotImplementedException("LC_FX_Standard:Complete():::Not implemented yet");
        }

        #endregion 生命周期

        /// <summary>
        /// 对对应的Pawn初始化效果
        /// </summary>
        /// <param name="targetPawn"></param>
        public virtual void InitWith(Pawn targetPawn)
        {
            hasInited = true;
            throw new NotImplementedException();
        }

        /// <summary>
        /// 和保存有关
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref completeTick, "completeTick", 0);
            Scribe_Values.Look(ref bioSignature, "bioSignature", 0);
        }
    }
}