using LCAnomalyCore.GameComponent;
using Verse;

namespace LCAnomalyCore.Util
{
    public static class Components
    {
        public static GameComponent_LC LC => Current.Game.GetComponent<GameComponent_LC>();
    }
}