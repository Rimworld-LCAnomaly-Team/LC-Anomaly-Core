using LCAnomalyCore.UI;
using Verse;

namespace LCAnomalyCore.Util
{
    public static class Gizmos
    {
        public static Verse.Gizmo Get_EntityCodex(Thing thing)
        {
            return new Command_Action
            {
                defaultLabel = "LC_EntityCodexGizmoLabel".Translate() + "...",
                defaultDesc = "LC_EntityCodexGizmoDesc".Translate(),
                icon = new CachedTexture("UI/Icons/LC_OpenCodex").Texture,
                action = delegate ()
                {
                    Find.WindowStack.Add(new Dialog_LC_EntityCodex((Defs.EntityCodexEntryDef)thing.def.entityCodexEntry));
                }
            };
        }
    }
}