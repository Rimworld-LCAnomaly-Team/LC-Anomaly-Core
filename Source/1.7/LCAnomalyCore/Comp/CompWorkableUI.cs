using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompWorkableUI</c> 类型。</summary>
    public class CompWorkableUI : ThingComp
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public CompProperties_WorkableUI Props => (CompProperties_WorkableUI)props;

        /// <summary>表示 <c>UIAllowed</c>。</summary>
        public bool UIAllowed = true;

        private string path = "Things/Building/LC_HoldingPlatform/Workable/";

        private static readonly Vector2 DefaultWorkableDrawSize = new Vector2(3f, 3f);

        /// <summary>获取 <c>AllowWorkGraphic</c>。</summary>
        public Graphic AllowWorkGraphic => allowWorkGraphic ??
            (allowWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            (path + "Allow", ShaderDatabase.Transparent, DefaultWorkableDrawSize, Color.white));

        private Graphic allowWorkGraphic;

        /// <summary>获取 <c>NotAllowWorkGraphic</c>。</summary>
        public Graphic NotAllowWorkGraphic => notAllowWorkGraphic ??
            (notAllowWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            (path + "NotAllow", ShaderDatabase.Transparent, DefaultWorkableDrawSize, Color.white));

        private Graphic notAllowWorkGraphic;

        /// <summary>获取 <c>AutoWorkGraphic</c>。</summary>
        public Graphic AutoWorkGraphic => autoWorkGraphic ??
            (autoWorkGraphic = GraphicDatabase.Get<Graphic_Single>
            (path + "Auto", ShaderDatabase.Transparent, DefaultWorkableDrawSize, Color.white));

        private Graphic autoWorkGraphic;

        /// <inheritdoc />
        public override void PostExposeData()
        {
            Scribe_Values.Look<bool>(ref UIAllowed, "UIAllowed", true, false);
        }
    }
}