using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 类型转换工具
    /// </summary>
    public static class Types
    {
        /// <summary>
        /// 检查是否是LC实体
        /// </summary>
        /// <param name="thing">实体</param>
        /// <returns>是则返回true，反之false</returns>
        public static bool CheckIfLCEntity(Thing thing)
        {
            if (thing == null)
                return false;

            return thing.def.entityCodexEntry is Defs.EntityCodexEntryDef;
        }
    }

    public enum EAnomalyWorkType
    {
        /// <summary>
        /// 未知
        /// </summary>
        Unknown,

        /// <summary>
        /// 本能
        /// </summary>
        Instinct,

        /// <summary>
        /// 沟通
        /// </summary>
        Attachment,

        /// <summary>
        /// 洞察
        /// </summary>
        Insight,

        /// <summary>
        /// 压迫
        /// </summary>
        Repression
    }

    /// <summary>
    /// 异想体等级
    /// </summary>
    public enum EAbnormalLevelType
    {
        /// <summary>
        /// Z
        /// </summary>
        ZAYIN = 0,

        /// <summary>
        /// T
        /// </summary>
        TETH,

        /// <summary>
        /// H
        /// </summary>
        HE,

        /// <summary>
        /// W
        /// </summary>
        WAW,

        /// <summary>
        /// A
        /// </summary>
        ALEPH
    }
}