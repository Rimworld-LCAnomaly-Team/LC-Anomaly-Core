using UnityEngine;

namespace LCAnomalyCore.Util
{
    public static class MaterialDatabase
    {
        public static Material SilhouetteMat = AssetBundleUtil.MainBundle.LoadAsset<Material>("SilhouetteMat");
    }
}
