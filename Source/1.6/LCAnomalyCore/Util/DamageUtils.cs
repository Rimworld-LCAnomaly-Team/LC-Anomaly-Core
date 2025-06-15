using System.ComponentModel;

namespace LCAnomalyCore.Util
{
    public static class DamageUtils
    {
        public static int LevelTag2Int(string tag)
        {
            switch (tag)
            {
                case "ZAYIN":
                    return 1;

                case "TETH":
                    return 2;

                case "HE":
                    return 3;

                case "WAW":
                    return 4;

                case "ALEPH":
                    return 5;

                default:
                    throw new InvalidEnumArgumentException($"未知的异想体等级：{tag}");
            }
        }

        public static float GetDamageLevelFactor(int attacker, int victim)
        {
            int delta = victim - attacker;

            if (delta == 0)
                return 1.0f;

            //减伤
            if (delta > 0)
            {
                switch (delta)
                {
                    case 1:
                        return 0.8f;

                    case 2:
                        return 0.7f;

                    case 3:
                        return 0.6f;

                    case 4:
                        return 0.4f;
                }
            }

            //增伤
            if (delta < 0)
            {
                switch (delta)
                {
                    case -1:
                        return 1.0f;

                    case -2:
                        return 1.2f;

                    case -3:
                        return 1.5f;

                    case -4:
                        return 2.0f;
                }
            }

            return 1.0f;
        }
    }
}