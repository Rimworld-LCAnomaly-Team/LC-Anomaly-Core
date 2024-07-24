using LCAnomalyLibrary.Setting;
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

        public Graphic AllowWorkGraphic => allowWorkGraphic ?? 
            (allowWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            ("UI/Workable/Allow", ShaderDatabase.Transparent, Defs.ThingDefOf.LC_HoldingPlatform.graphicData.drawSize, Color.white));
        private Graphic allowWorkGraphic;

        public Graphic NotAllowWorkGraphic => notAllowWorkGraphic ?? 
            (notAllowWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            ("UI/Workable/NotAllow", ShaderDatabase.Transparent, Defs.ThingDefOf.LC_HoldingPlatform.graphicData.drawSize, Color.white));
        private Graphic notAllowWorkGraphic;

        public override void PostExposeData()
        {
            Scribe_Values.Look<bool>(ref UIAllowed, "UIAllowed", true, false);
        }
    }
}
