using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 拓展Mod激活状态
    /// </summary>
    public static class ExpansionActive
    {
        /// <summary>
        /// 大罪生物是否激活
        /// </summary>
        public static bool SevenSinEntityActive => ModsConfig.IsActive("DarthCY.LC.SevenSinEntity");

        /// <summary>
        /// 肉食提灯是否激活
        /// </summary>
        public static bool MeatLanternActive => ModsConfig.IsActive("DarthCY.LC.MeatLantern");
    }
}