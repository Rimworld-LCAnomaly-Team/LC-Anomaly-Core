using UnityEngine;
using UnityEngine.EventSystems;

namespace LCAnomalyUGUI
{
    public class SelectCreatureController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public Animator DarkEffectAnimator;
        public Animator TextBGAnimator;
        private Animator _selfAnimator;

        private void Awake()
        {
            _selfAnimator = GetComponent<Animator>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.LogWarning("按下");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.LogWarning("进入");

            _selfAnimator?.SetBool("Selected", true);
            DarkEffectAnimator?.SetBool("Selected", true);
            TextBGAnimator?.SetBool("Selected", true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.LogWarning("离开");
            _selfAnimator?.SetBool("Selected", false);
            DarkEffectAnimator?.SetBool("Selected", false);
            TextBGAnimator?.SetBool("Selected", false);
        }
    }
}
