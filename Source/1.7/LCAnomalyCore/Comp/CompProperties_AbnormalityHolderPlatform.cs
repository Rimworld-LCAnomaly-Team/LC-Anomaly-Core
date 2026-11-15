using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompProperties_AbnormalityHolderPlatform</c> 类型。</summary>
    public class CompProperties_AbnormalityHolderPlatform : CompProperties_AbnormalityHolder
    {
        /// <summary>表示 <c>untetheredGraphicTexPath</c>。</summary>
        [NoTranslate]
        public string untetheredGraphicTexPath;

        /// <summary>表示 <c>tilingChainTexPath</c>。</summary>
        [NoTranslate]
        public string tilingChainTexPath;

        /// <summary>表示 <c>baseChainFastenerTexPath</c>。</summary>
        [NoTranslate]
        public string baseChainFastenerTexPath;

        /// <summary>表示 <c>targetChainFastenerTexPath</c>。</summary>
        [NoTranslate]
        public string targetChainFastenerTexPath;

        /// <summary>表示 <c>entityLungeSoundHi</c>。</summary>
        public SoundDef entityLungeSoundHi;

        /// <summary>表示 <c>entityLungeSoundLow</c>。</summary>
        public SoundDef entityLungeSoundLow;

        /// <summary>初始化 <c>CompProperties_AbnormalityHolderPlatform</c> 类的新实例。</summary>
        public CompProperties_AbnormalityHolderPlatform()
        {
            compClass = typeof(CompAbnormalityHolderPlatform);
        }
    }
}
