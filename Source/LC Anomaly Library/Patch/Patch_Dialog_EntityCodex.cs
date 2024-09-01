using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace LCAnomalyLibrary.Patch
{
    /// <summary>
    /// 关于Dialog_EntityCodex的补丁（将脑叶异常从原版图鉴列表中移除）
    /// </summary>
    [HarmonyPatch(typeof(Dialog_EntityCodex), MethodType.Constructor, new Type[] { typeof(EntityCodexEntryDef) })]
    public class Patch_Dialog_EntityCodex
    {
        private static void Postfix(Dialog_EntityCodex __instance, EntityCodexEntryDef selectedEntry = null)
        {
            BindingFlags privateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

            var fieldInfo0 = typeof(Dialog_EntityCodex).GetField("categoriesInOrder", privateFlags);
            var fieldInfo1 = typeof(Dialog_EntityCodex).GetField("entriesByCategory", privateFlags);
            var fieldInfo2 = typeof(Dialog_EntityCodex).GetField("categoryRectSizes", privateFlags);
            var fieldInfo3 = typeof(Dialog_EntityCodex).GetField("selectedEntry", privateFlags);

            __instance.doCloseX = true;
            __instance.doCloseButton = true;
            __instance.forcePause = true;

            var temp_categoriesInOrder = (from x in DefDatabase<EntityCategoryDef>.AllDefsListForReading
                                          where DefDatabase<EntityCodexEntryDef>.AllDefs
                                          .Any((EntityCodexEntryDef y) => !(y is Defs.EntityCodexEntryDef) && y.category == x && y.Visible)
                                          orderby x.listOrder
                                          select x).ToList();

            fieldInfo0.SetValue(__instance, temp_categoriesInOrder);

            var temp_entriesByCategory = new Dictionary<EntityCategoryDef, List<EntityCodexEntryDef>>();
            var temp_categoryRectSizes = new Dictionary<EntityCategoryDef, float>();

            foreach (EntityCategoryDef item in temp_categoriesInOrder)
            {
                temp_entriesByCategory.Add(item, new List<EntityCodexEntryDef>());
                temp_categoryRectSizes.Add(item, 0f);
            }
            fieldInfo2.SetValue(__instance, temp_categoryRectSizes);

            foreach (EntityCodexEntryDef item2 in DefDatabase<EntityCodexEntryDef>.AllDefsListForReading)
            {
                if (item2.Visible && !(item2 is Defs.EntityCodexEntryDef))
                {
                    temp_entriesByCategory[item2.category].Add(item2);
                }
            }

            foreach (KeyValuePair<EntityCategoryDef, List<EntityCodexEntryDef>> item3 in temp_entriesByCategory)
            {
                item3.Deconstruct(out var _, out var value);
                value.SortBy((EntityCodexEntryDef e) => e.orderInCategory, (EntityCodexEntryDef e) => e.label);
            }
            fieldInfo1.SetValue(__instance, temp_entriesByCategory);

            var temp_selectedEntry = selectedEntry ?? DefDatabase<EntityCodexEntryDef>.AllDefs.OrderBy((EntityCodexEntryDef x) => x.label).FirstOrDefault((EntityCodexEntryDef x) => x.Discovered);
            fieldInfo3.SetValue(__instance, temp_selectedEntry);
        }
    }
}