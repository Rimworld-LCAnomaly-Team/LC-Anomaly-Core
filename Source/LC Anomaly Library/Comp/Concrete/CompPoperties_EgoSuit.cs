using Verse;

namespace LCAnomalyLibrary.Comp.Concrete
{
    public class CompPoperties_EgoSuit : CompProperties
    {
        public string level = "ZAYIN";

        public float redResist = 0.0f;
        public float whiteResist = 0.0f;
        public float blackResist = 0.0f;
        public float paleResist = 0.0f;

        public CompPoperties_EgoSuit()
        {
            compClass = typeof(CompEgoSuit);
        }
    }
}