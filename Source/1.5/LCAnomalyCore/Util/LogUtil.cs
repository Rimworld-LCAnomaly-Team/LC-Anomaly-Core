using Verse;

namespace LCAnomalyCore.Util
{
    public class LogUtil
    {
        public static void Message(string message)
        {
            if (DebugSettings.godMode)
                Log.Message(message);
        }

        public static void Warning(string message)
        {
            if (DebugSettings.godMode)
                Log.Warning(message);
        }

        public static void Error(string message)
        {
            if (DebugSettings.godMode)
                Log.Error(message);
        }
    }
}