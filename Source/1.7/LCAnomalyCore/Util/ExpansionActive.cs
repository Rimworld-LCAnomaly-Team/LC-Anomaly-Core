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
        /// 异想体合集是否激活
        /// </summary>
        public static bool AbnormalitiesActive => ModsConfig.IsActive("DarthCY.LC.Abnormalities");

        /// <summary>
        /// 肉食提灯是否激活
        /// </summary>
        public static bool MeatLanternActive => AbnormalitiesActive || ModsConfig.IsActive("DarthCY.LC.MeatLantern");
    }
}