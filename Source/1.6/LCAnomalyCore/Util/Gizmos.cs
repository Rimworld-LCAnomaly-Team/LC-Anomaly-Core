using LCAnomalyCore.Defs;
using LCAnomalyCore.UI;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>表示 <c>Gizmos</c> 类型。</summary>
    public static class Gizmos
    {
        /// <summary>执行 <c>Get_EntityCodex</c> 定义的操作。</summary>
        public static Gizmo Get_EntityCodex(Thing thing)
        {
            return new Command_Action
            {
                defaultLabel = "LC_EntityCodexGizmoLabel".Translate() + "...",
                defaultDesc = "LC_EntityCodexGizmoDesc".Translate(),
                icon = new CachedTexture("UI/Icons/LC_OpenCodex").Texture,
                action = delegate ()
                {
                    var abnormalDef = GenDefDatabase.GetDef(typeof(AbnormalityCodexEntryDef), thing.def.defName) as AbnormalityCodexEntryDef;
                    Find.WindowStack.Add(new Dialog_LC_EntityCodex(abnormalDef));
                }
            };
        }
    }
}