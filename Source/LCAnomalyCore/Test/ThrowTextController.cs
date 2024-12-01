using UnityEngine;

namespace LCAnomalyCore.Test
{
    public class ThrowTextController : MonoBehaviour
    {
        public Canvas ThrowTextCanvas;
        public CanvasGroup ThrowTextCanvasGroup;

        public bool shouldShow = false;
        public bool shouldFade = false;

        private string array;
        private float timer = 0;
        private int curPos = 0;
        private float charPerSecond = 0.2f;

        private string tmpStr = string.Empty;
        private bool reachedStartOfRichText = false;

        private UnityEngine.UI.Text SelfText;

        public ThrowTextController()
        {
            this.ThrowTextCanvas = gameObject.AddComponent<Canvas>();
            this.ThrowTextCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            this.ThrowTextCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            this.ThrowTextCanvasGroup.interactable = false;
            this.ThrowTextCanvasGroup.blocksRaycasts = false;
            this.ThrowTextCanvasGroup.ignoreParentGroups = false;
            this.ThrowTextCanvasGroup.alpha = 0f;

            GameObject go = new GameObject("tadada");
            go.transform.SetParent(transform, false);

            SelfText = go.AddComponent<UnityEngine.UI.Text>();
            SelfText.text = "";

            SelfText.font = (Font)Resources.Load("Fonts/Arial_medium");
            SelfText.fontSize = 32;
            SelfText.fontStyle = FontStyle.Bold;
            SelfText.color = new Color32(255, 128, 0, 200);
            SelfText.material.color = Color.white;
            SelfText.supportRichText = true;
            SelfText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void Update()
        {
            if (shouldShow)
            {
                timer += Time.deltaTime;
                if (timer >= charPerSecond)
                {
                    timer = 0;

                    if (curPos + 1 <= array.Length)
                    {
                        if (array[curPos] == '<' && array[curPos + 1] == 's')
                        {
                            reachedStartOfRichText = true;
                        }
                    }

                    if (curPos + 1 <= array.Length)
                    {
                        if (reachedStartOfRichText)
                        {
                            if (array[curPos] == 'e' && array[curPos + 1] == '>')
                            {
                                reachedStartOfRichText = false;
                            }
                        }
                    }

                    curPos++;

                    if (!reachedStartOfRichText)
                        SelfText.text = array.Substring(0, curPos);

                    if (curPos >= array.Length)
                    {
                        shouldShow = false;
                        shouldFade = true;
                        timer = 0;
                        curPos = 0;
                    }
                }
            }

            if (shouldFade)
            {
                ThrowTextCanvasGroup.alpha -= 0.005f;
                if (ThrowTextCanvasGroup.alpha <= 0f)
                {
                    shouldFade = false;
                }
            }
        }

        public void Trigger(string text)
        {
            SelfText.transform.Rotate(0, 0, 30);
            this.array = text;
            shouldShow = true;
            ThrowTextCanvasGroup.alpha = 1f;
        }
    }
}