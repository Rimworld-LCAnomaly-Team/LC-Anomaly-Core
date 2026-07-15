using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>表示 <c>AbnormalityCodexEntryDef</c> 类型。</summary>
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

        /// <summary>表示 <c>orderInCategory</c>。</summary>
        public int orderInCategory = 9999999;

        [NoTranslate]
        private string uiIconPath = null;

        /// <summary>表示 <c>icon</c>。</summary>
        public Texture2D icon;

        /// <summary>表示 <c>silhouette</c>。</summary>
        public Texture2D silhouette;

        /// <summary>表示 <c>Visible</c>。</summary>
        public bool Visible;

        /// <summary>表示 <c>Discovered</c>。</summary>
        public bool Discovered;

        private const string SilhouetteTexPathSuffix = "_Silhouette";

        /// <inheritdoc />
        public override void PostLoad()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                icon = (uiIconPath.NullOrEmpty() ? BaseContent.BadTex : ContentFinder<Texture2D>.Get(uiIconPath));
                silhouette = (uiIconPath.NullOrEmpty() ? BaseContent.BadTex : ContentFinder<Texture2D>.Get(uiIconPath + "_Silhouette"));
            });
        }

        /// <inheritdoc />
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
