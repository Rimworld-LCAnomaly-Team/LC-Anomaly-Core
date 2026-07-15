using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Test;
using UnityEngine;
using UnityEngine.UI;
using Verse;
using static LCAnomalyCore.Util.MusicUtils;

namespace LCAnomalyCore.Singleton
{
    /// <summary>表示 <c>LCCanvasSingleton</c> 类型。</summary>
    public class LCCanvasSingleton
    {
        /// <summary>表示 <c>Instance</c>。</summary>
        public static LCCanvasSingleton Instance
        {
            get
            {
                if (instance == null)
                    instance = new LCCanvasSingleton();
                return instance;
            }
        }

        private static LCCanvasSingleton instance = null;

        /// <summary>表示 <c>GameObject</c>。</summary>
        public GameObject GameObject;
        /// <summary>表示 <c>CanvasWarningUI</c>。</summary>
        public Canvas CanvasWarningUI;
        /// <summary>表示 <c>Image</c>。</summary>
        public Image Image;
        /// <summary>表示 <c>CanvasGroupWarningUI</c>。</summary>
        public CanvasGroup CanvasGroupWarningUI;

        /// <summary>表示 <c>FirstWarningSprite</c>。</summary>
        public Sprite FirstWarningSprite = Sprite.Create(ContentFinder<Texture2D>.Get("UI/WarningUI/WarningUI_First"), new Rect(0, 0, 4096, 2160), new Vector2(0.5f, 0.5f));
        /// <summary>表示 <c>SecondWarningSprite</c>。</summary>
        public Sprite SecondWarningSprite = Sprite.Create(ContentFinder<Texture2D>.Get("UI/WarningUI/WarningUI_Second"), new Rect(0, 0, 4096, 2160), new Vector2(0.5f, 0.5f));
        /// <summary>表示 <c>ThirdWarningSprite</c>。</summary>
        public Sprite ThirdWarningSprite = Sprite.Create(ContentFinder<Texture2D>.Get("UI/WarningUI/WarningUI_Third"), new Rect(0, 0, 4096, 2160), new Vector2(0.5f, 0.5f));

        private int tickCounter;
        private bool shouldShowWarningUI;
        private bool shouldDecreaseAlpha;

        /// <summary>初始化 <c>LCCanvasSingleton</c> 类的新实例。</summary>
        public LCCanvasSingleton()
        {
            this.GameObject = new GameObject("LCAnomalyCanvas");

            this.CanvasWarningUI = this.GameObject.AddComponent<Canvas>();
            this.CanvasWarningUI.renderMode = RenderMode.ScreenSpaceOverlay;

            this.Image = this.GameObject.AddComponent<Image>();
            this.Image.sprite = FirstWarningSprite;
            this.Image.material.color = Color.red;

            this.CanvasGroupWarningUI = this.GameObject.AddComponent<CanvasGroup>();
            this.CanvasGroupWarningUI.interactable = false;
            this.CanvasGroupWarningUI.blocksRaycasts = false;
            this.CanvasGroupWarningUI.ignoreParentGroups = false;
            this.CanvasGroupWarningUI.alpha = 0f;

            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Current.Game.GetComponent<GameComponent_LC>().LCGameComponentTickEvent += Tick;
        }

        /// <summary>执行 <c>RemoveEvents</c> 定义的操作。</summary>
        public void RemoveEvents()
        {
            Current.Game.GetComponent<GameComponent_LC>().LCGameComponentTickEvent -= Tick;
        }

        /// <summary>执行 <c>Tick</c> 定义的操作。</summary>
        protected void Tick()
        {
            if (shouldShowWarningUI)
            {
                if (shouldDecreaseAlpha)
                {
                    this.CanvasGroupWarningUI.alpha -= 0.01f;
                }
                else
                {
                    this.CanvasGroupWarningUI.alpha += 0.01f;
                }

                if (this.CanvasGroupWarningUI.alpha >= 1f)
                {
                    shouldDecreaseAlpha = true;
                }
                else if (this.CanvasGroupWarningUI.alpha <= 0f)
                {
                    shouldDecreaseAlpha = false;
                }
            }

            tickCounter++;
            //30s关闭
            if (tickCounter >= 1800)
            {
                shouldShowWarningUI = false;
                this.CanvasGroupWarningUI.alpha = 0f;
                tickCounter = 0;
            }
        }

        /// <summary>执行 <c>ShowWarningUI</c> 定义的操作。</summary>
        public void ShowWarningUI(EWarningLevel level)
        {
            if (level == EWarningLevel.First)
            {
                this.Image.sprite = FirstWarningSprite;
                this.Image.material.color = Color.yellow;
            }
            else if (level == EWarningLevel.Second)
            {
                this.Image.sprite = SecondWarningSprite;
                this.Image.material.color = Color.red + Color.yellow;
            }
            else if (level == EWarningLevel.Third)
            {
                this.Image.sprite = ThirdWarningSprite;
                this.Image.material.color = Color.red;
            }

            tickCounter = 0;
            shouldShowWarningUI = true;
        }

        /// <summary>执行 <c>ShowText</c> 定义的操作。</summary>
        public void ShowText(string text)
        {
            GameObject go = new GameObject("TestText");
            var controller = go.AddComponent<ThrowTextController>();

            controller.Trigger("主<size=36>管</size>，<size=34>这</size>就是你所<size=38>谓</size>的 “<size=34>无法控制</size>的<size=38>局面</size>”。");
        }
    }
}