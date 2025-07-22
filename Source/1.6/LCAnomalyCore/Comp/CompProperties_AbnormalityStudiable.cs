using RimWorld;

namespace LCAnomalyCore.Comp
{
    public class CompProperties_AbnormalityStudiable : CompProperties_Studiable
    {
        public int studyTimesPeriod;

        public CompProperties_AbnormalityStudiable()
        {
            compClass = typeof(CompAbnormalityStudiable);
        }
    }
}