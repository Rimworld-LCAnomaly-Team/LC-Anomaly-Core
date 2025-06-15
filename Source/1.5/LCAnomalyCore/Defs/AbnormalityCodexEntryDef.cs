using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Defs
{
    public class AbnormalityCodexEntryDef : Def
    {
        /// <summary>
        /// 别名列表（不同观察等级的不同名字）
        /// </summary>
        public List<string> aliasLabels;

        /// <summary>
        /// 分类等级
        /// </summary>
        public AbnormalityCategoryDef category;

        public int orderInCategory = 9999999;

        [NoTranslate]
        private string uiIconPath;

        public Texture2D icon;

        public Texture2D silhouette;

        public bool Visible;

        public bool Discovered;

        private const string SilhouetteTexPathSuffix = "_Silhouette";

        public override void PostLoad()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                icon = (uiIconPath.NullOrEmpty() ? BaseContent.BadTex : ContentFinder<Texture2D>.Get(uiIconPath));
                silhouette = (uiIconPath.NullOrEmpty() ? BaseContent.BadTex : ContentFinder<Texture2D>.Get(uiIconPath + "_Silhouette"));
            });
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string item in base.ConfigErrors())
            {
                yield return item;
            }

            if (category == null)
            {
                yield return "category is null.";
            }

            if (uiIconPath.NullOrEmpty())
            {
                yield return "missing icon.";
            }
        }
    }
}