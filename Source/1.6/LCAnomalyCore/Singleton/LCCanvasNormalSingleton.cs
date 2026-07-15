using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Verse;

namespace LCAnomalyCore.Singleton
{
    /// <summary>表示 <c>LCCanvasNormalSingleton</c> 类型。</summary>
    public class LCCanvasNormalSingleton
    {
        /// <summary>表示 <c>Instance</c>。</summary>
        public static LCCanvasNormalSingleton Instance
        {
            get
            {
                if (instance == null)
                    instance = new LCCanvasNormalSingleton();
                return instance;
            }
        }

        private static LCCanvasNormalSingleton instance = null;

        /// <summary>表示 <c>GameObject</c>。</summary>
        public GameObject GameObject;
        /// <summary>表示 <c>SelfCanvas</c>。</summary>
        public Canvas SelfCanvas;
        /// <summary>表示 <c>SelfGraphicRaycaster</c>。</summary>
        public GraphicRaycaster SelfGraphicRaycaster;

        /// <summary>初始化 <c>LCCanvasNormalSingleton</c> 类的新实例。</summary>
        public LCCanvasNormalSingleton()
        {
            this.GameObject = new GameObject("LCCanvasNormal");

            this.SelfCanvas = this.GameObject.AddComponent<Canvas>();
            this.SelfCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            this.SelfGraphicRaycaster = this.GameObject.AddComponent<GraphicRaycaster>();
            this.SelfGraphicRaycaster.ignoreReversedGraphics = true;
            this.SelfGraphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            var module = this.GameObject.AddComponent<StandaloneInputModule>();
            var sys = this.GameObject.AddComponent<EventSystem>();
            
        }
    }

    /// <summary>表示 <c>LCMonoSingleton</c> 类型。</summary>
    public class LCMonoSingleton : MonoBehaviour
    {
        /// <summary>表示 <c>Instance</c>。</summary>
        public static LCMonoSingleton Instance
        {
            get
            {
                if (instance == null)
                    instance = new LCMonoSingleton();
                return instance;
            }
        }

        private static LCMonoSingleton instance = null;

        /// <summary>执行 <c>InitPrefab</c> 定义的操作。</summary>
        public void InitPrefab(GameObject prefab, Transform parent)
        {
            Instantiate(prefab, parent);
        }
    }
}
