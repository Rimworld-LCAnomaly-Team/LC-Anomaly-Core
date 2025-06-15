using UnityEngine;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 材质库
    /// </summary>
    public static class MaterialDatabase
    {
        /// <summary>
        /// 剪影材质
        /// </summary>
        public static Material SilhouetteMat = AssetBundleUtil.MainBundle.LoadAsset<Material>("SilhouetteMat");
    }
}
