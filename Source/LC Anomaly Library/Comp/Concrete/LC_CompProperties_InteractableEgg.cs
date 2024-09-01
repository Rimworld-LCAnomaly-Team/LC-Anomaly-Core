using RimWorld;

namespace LCAnomalyLibrary.Comp
{
    /// <summary>
    /// LC可交互蛋交互性基础CompProperties
    /// </summary>
    public class LC_CompProperties_InteractableEgg : CompProperties_Interactable
    {
        /// <summary>
        /// XML：是否保持生物特征
        /// </summary>
        public bool shouldTransferBioSignature;

        /// <summary>
        /// Comp
        /// </summary>
        public LC_CompProperties_InteractableEgg()
        {
            compClass = typeof(LC_CompEgg);
        }
    }
}