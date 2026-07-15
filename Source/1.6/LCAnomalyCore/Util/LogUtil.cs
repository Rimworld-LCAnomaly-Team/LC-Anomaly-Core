using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>表示 <c>LogUtil</c> 类型。</summary>
    public class LogUtil
    {
        /// <summary>执行 <c>Message</c> 定义的操作。</summary>
        public static void Message(string message)
        {
            if (DebugSettings.godMode)
                Log.Message(message);
        }

        /// <summary>执行 <c>Warning</c> 定义的操作。</summary>
        public static void Warning(string message)
        {
            if (DebugSettings.godMode)
                Log.Warning(message);
        }

        /// <summary>执行 <c>Error</c> 定义的操作。</summary>
        public static void Error(string message)
        {
            if (DebugSettings.godMode)
                Log.Error(message);
        }
    }
}