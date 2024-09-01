using RimWorld;

namespace LCAnomalyLibrary.Comp
{
    public class LC_CompProperties_Studiable : CompProperties_Studiable
    {
        public int studyTimesPeriod;

        public LC_CompProperties_Studiable()
        {
            compClass = typeof(LC_CompStudiable);
        }
    }
}
