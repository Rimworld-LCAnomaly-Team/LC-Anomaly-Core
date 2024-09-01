using LCAnomalyLibrary.GameComponent;
using Verse;

namespace LCAnomalyLibrary.Util
{
    public static class Components
    {
        public static GameComponent_LC LC => Current.Game.GetComponent<GameComponent_LC>();
    }
}