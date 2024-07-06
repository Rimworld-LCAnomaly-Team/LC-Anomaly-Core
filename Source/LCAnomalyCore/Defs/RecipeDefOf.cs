using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该mod的所有RecipeDef
    /// </summary>
    [DefOf]
    public static class RecipeDefOf
    {
        /// <summary>
        /// 提取Cogito
        /// </summary>
        public static RecipeDef ExtractCogito;

        static RecipeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RecipeDefOf));
        }
    }
}