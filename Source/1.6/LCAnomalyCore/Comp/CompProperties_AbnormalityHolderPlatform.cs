using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompProperties_AbnormalityHolderPlatform : CompProperties_AbnormalityHolder
    {
        [NoTranslate]
        public string untetheredGraphicTexPath;

        [NoTranslate]
        public string tilingChainTexPath;

        [NoTranslate]
        public string baseChainFastenerTexPath;

        [NoTranslate]
        public string targetChainFastenerTexPath;

        public SoundDef entityLungeSoundHi;

        public SoundDef entityLungeSoundLow;

        public CompProperties_AbnormalityHolderPlatform()
        {
            compClass = typeof(CompAbnormalityHolderPlatform);
        }
    }
}
