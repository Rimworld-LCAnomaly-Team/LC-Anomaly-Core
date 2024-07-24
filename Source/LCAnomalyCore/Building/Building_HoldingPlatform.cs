using LCAnomalyCore.Comp;
using LCAnomalyLibrary.Setting;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    [StaticConstructorOnStartup]
    public class Building_HoldingPlatform : RimWorld.Building_HoldingPlatform
    {
        protected CompWorkableUI CompWorkable => compWorkable ?? (compWorkable = GetComp<CompWorkableUI>());
        private CompWorkableUI compWorkable;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            if (CompWorkable != null && CompWorkable.UIAllowed)
            {
                if(HeldPawn != null)
                {
                    var comp = HeldPawn.GetComp<CompStudiable>();
                    if (comp != null)
                    {
                        var studiable = !(comp.EverStudiable() && comp.TicksTilNextStudy > 0);
                        var graphic = studiable ? CompWorkable.AllowWorkGraphic : CompWorkable.NotAllowWorkGraphic;
                        graphic.Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
                    }
                }
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if(CompWorkable != null)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "CommandToggleShowWorkableLabel".Translate(),
                    defaultDesc = "CommandToggleShowWorkableDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/WorkableUI"),
                    isActive = (() => this.CompWorkable.UIAllowed),
                    toggleAction = delegate ()
                    {
                        this.CompWorkable.UIAllowed = !this.CompWorkable.UIAllowed;
                    }
                };
            }
        }
    }
}
