using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 关于Dialog_EntityCodex的补丁（将脑叶异常从原版图鉴列表中移除）
    /// </summary>
    [HarmonyPatch(typeof(Dialog_EntityCodex), MethodType.Constructor, new Type[] { typeof(AbnormalityCodexEntryDef) })]
    public class Patch_Dialog_EntityCodex
    {
        private static void Postfix(Dialog_EntityCodex __instance, AbnormalityCodexEntryDef selectedEntry = null)
        {
            BindingFlags privateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

            var fieldInfo0 = typeof(Dialog_EntityCodex).GetField("categoriesInOrder", privateFlags);
            var fieldInfo1 = typeof(Dialog_EntityCodex).GetField("entriesByCategory", privateFlags);
            var fieldInfo2 = typeof(Dialog_EntityCodex).GetField("categoryRectSizes", privateFlags);
            var fieldInfo3 = typeof(Dialog_EntityCodex).GetField("selectedEntry", privateFlags);

            __instance.doCloseX = true;
            __instance.doCloseButton = true;
            __instance.forcePause = true;

            var temp_categoriesInOrder = (from x in DefDatabase<AbnormalityCategoryDef>.AllDefsListForReading
                                          where DefDatabase<AbnormalityCodexEntryDef>.AllDefs
                                          .Any((AbnormalityCodexEntryDef y) => !(y is Defs.AbnormalityCodexEntryDef) && y.category == x && y.Visible)
                                          orderby x.listOrder
                                          select x).ToList();

            fieldInfo0.SetValue(__instance, temp_categoriesInOrder);

            var temp_entriesByCategory = new Dictionary<AbnormalityCategoryDef, List<AbnormalityCodexEntryDef>>();
            var temp_categoryRectSizes = new Dictionary<AbnormalityCategoryDef, float>();

            foreach (AbnormalityCategoryDef item in temp_categoriesInOrder)
            {
                temp_entriesByCategory.Add(item, new List<AbnormalityCodexEntryDef>());
                temp_categoryRectSizes.Add(item, 0f);
            }
            fieldInfo2.SetValue(__instance, temp_categoryRectSizes);

            foreach (AbnormalityCodexEntryDef item2 in DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading)
            {
                if (item2.Visible && !(item2 is Defs.AbnormalityCodexEntryDef))
                {
                    temp_entriesByCategory[item2.category].Add(item2);
                }
            }

            foreach (KeyValuePair<AbnormalityCategoryDef, List<AbnormalityCodexEntryDef>> item3 in temp_entriesByCategory)
            {
                item3.Deconstruct(out var _, out var value);
                value.SortBy((AbnormalityCodexEntryDef e) => e.orderInCategory, (AbnormalityCodexEntryDef e) => e.label);
            }
            fieldInfo1.SetValue(__instance, temp_entriesByCategory);

            var temp_selectedEntry = selectedEntry ?? DefDatabase<AbnormalityCodexEntryDef>.AllDefs.OrderBy((AbnormalityCodexEntryDef x) => x.label).FirstOrDefault((AbnormalityCodexEntryDef x) => x.Discovered);
            fieldInfo3.SetValue(__instance, temp_selectedEntry);
        }
    }
}