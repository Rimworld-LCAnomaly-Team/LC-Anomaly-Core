using RimWorld;

namespace LCAnomalyCore.Comp
{
    public class CompProperties_TriggerPawnStatusAndDestroy : CompProperties_UseEffectDestroySelf
    {
        public CompProperties_TriggerPawnStatusAndDestroy()
        {
            compClass = typeof(CompUseEffect_TriggerPawnStatusAndDestroy);
        }
    }
}
