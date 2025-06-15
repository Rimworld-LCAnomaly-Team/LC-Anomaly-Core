using LCAnomalyCore.Settings;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// AB包工具类
    /// </summary>
    public static class AssetBundleUtil
    {
        /// <summary>
        /// 主AB包
        /// </summary>
        public static AssetBundle MainBundle
        {
            get
            {
                if (mainBundle == null)
                {
                    string text = "";

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        text = "StandaloneWindows64";
                    }

                    string bundlePath = Path.Combine(Setting_LCAnomalyCore_Main.ContentDir, "1.5\\Assets\\Bundles\\" + text + "\\lcanomalycore.asset");
                    AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                    mainBundle = bundle;

                    if (bundle == null)
                    {
                        Log.Error("Failed to load bundle at path: " + bundlePath);
                    }
                }

                return mainBundle;
            }
        }

        private static AssetBundle mainBundle;
    }
}
