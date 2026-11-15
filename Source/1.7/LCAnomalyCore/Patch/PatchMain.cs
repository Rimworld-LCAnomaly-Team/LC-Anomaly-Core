using HarmonyLib;
using System.Reflection;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 补丁主类
    /// </summary>
    [StaticConstructorOnStartup]
    public class PatchMain
    {
        /// <summary>
        /// 补丁实例
        /// </summary>
        public static Harmony instance;

        static PatchMain()
        {
            instance = new Harmony("LCAnomalyCore.Patch");
            //对所有特性标签的方法进行patch
            instance.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("LCAnomalyCore Patched");
        }
    }
}