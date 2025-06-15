using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompWorkableUI : ThingComp
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public CompProperties_WorkableUI Props => (CompProperties_WorkableUI)props;

        public bool UIAllowed = true;

        private string path = "Things/Building/LC_HoldingPlatform/Workable/";

        public Graphic AllowWorkGraphic => allowWorkGraphic ??
            (allowWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            (path + "Allow", ShaderDatabase.Transparent, Defs.ThingDefOf.LC_HoldingPlatform.graphicData.drawSize, Color.white));

        private Graphic allowWorkGraphic;

        public Graphic NotAllowWorkGraphic => notAllowWorkGraphic ??
            (notAllowWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            (path + "NotAllow", ShaderDatabase.Transparent, Defs.ThingDefOf.LC_HoldingPlatform.graphicData.drawSize, Color.white));

        private Graphic notAllowWorkGraphic;

        public Graphic AutoWorkGraphic => autoWorkGraphic ??
            (autoWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            (path + "Auto", ShaderDatabase.Transparent, Defs.ThingDefOf.LC_HoldingPlatform.graphicData.drawSize, Color.white));

        private Graphic autoWorkGraphic;

        public override void PostExposeData()
        {
            Scribe_Values.Look<bool>(ref UIAllowed, "UIAllowed", true, false);
        }
    }
}