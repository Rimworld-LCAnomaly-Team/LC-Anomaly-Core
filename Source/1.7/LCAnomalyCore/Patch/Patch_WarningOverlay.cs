using HarmonyLib;
using LCAnomalyCore.GameComponent;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 将脑叶警报覆盖层（WarningOverlay）绘制在所有非窗口 HUD（主按钮栏、警报列表、殖民者栏、消息、Tooltips 等）之上。
    /// 注：Waylith 1.7 会在游戏进入前缓存 Root.OnGUI 的委托，因此改为挂载进入游戏后才实例化的 UIRoot_Play.UIRootOnGUI。
    /// </summary>
    [HarmonyPatch(typeof(RimWorld.UIRoot_Play), nameof(RimWorld.UIRoot_Play.UIRootOnGUI))]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_UIRoot_Play_UIRootOnGUI_WarningOverlay
    {
        public static void Postfix()
        {
            if (Current.ProgramState == ProgramState.Playing
                && Current.Game != null
                && !LongEventHandler.ShouldWaitForEvent
                && Event.current != null
                && Event.current.type == EventType.Repaint)
            {
                GameComponent_LC.Instance?.WarningOverlay?.DrawAt(Vector2.zero);
            }
        }
    }

    /// <summary>
    /// 在各 Window 自身绘制尾部补绘相交的警报角标区域，使警报图层同样位于独立窗口（如对话窗/面板）之上且不拦截输入。
    /// </summary>
    [HarmonyPatch(typeof(Window), "InnerWindowOnGUI")]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_Window_InnerWindowOnGUI_WarningOverlay
    {
        public static void Postfix(Window __instance)
        {
            if (__instance != null
                && Current.ProgramState == ProgramState.Playing
                && Current.Game != null
                && Event.current != null
                && Event.current.type == EventType.Repaint)
            {
                GameComponent_LC.Instance?.WarningOverlay?.DrawInWindow(__instance.windowRect);
            }
        }
    }
}
