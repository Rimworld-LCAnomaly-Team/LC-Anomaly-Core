using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompPoperties_EgoSuit : CompProperties
    {
        public string level = "ZAYIN";

        public float redResist = 1.0f;
        public float whiteResist = 1.0f;
        public float blackResist = 1.0f;
        public float paleResist = 1.0f;

        public CompPoperties_EgoSuit()
        {
            compClass = typeof(CompEgoSuit);
        }
    }

    /// <summary>
    /// Correctly-spelled XML alias. The old class name remains for existing Defs.
    /// </summary>
    public class CompProperties_EgoSuit : CompPoperties_EgoSuit
    {
    }
}
