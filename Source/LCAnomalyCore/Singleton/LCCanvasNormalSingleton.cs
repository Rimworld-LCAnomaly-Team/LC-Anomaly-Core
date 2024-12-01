using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Verse;

namespace LCAnomalyCore.Singleton
{
    public class LCCanvasNormalSingleton
    {
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

        public GameObject GameObject;
        public Canvas SelfCanvas;
        public GraphicRaycaster SelfGraphicRaycaster;

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

    public class LCMonoSingleton : MonoBehaviour
    {
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

        public void InitPrefab(GameObject prefab, Transform parent)
        {
            Instantiate(prefab, parent);
        }
    }
}
