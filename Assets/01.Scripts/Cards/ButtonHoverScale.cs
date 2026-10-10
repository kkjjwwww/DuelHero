using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

namespace DuelHero.Cards
{
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(1f)] private float hoverScale = 1.15f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.1f;
        [SerializeField] private Ease transitionEase = Ease.OutQuad;
        private Button button;
        private Vector3 baseScale;
        private Tween scaleTween;

        private void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActiveAndEnabled && button.IsActive() && button.IsInteractable()) AnimateTo(Mathf.Max(1f, hoverScale));
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (isActiveAndEnabled && button.IsActive() && button.IsInteractable()) AnimateTo(1f);
            else ResetScale();
        }
        // The direction UI calls this when it changes button availability.
        public void RefreshInteractability()
        {
            if (button != null && (!button.IsActive() || !button.IsInteractable())) ResetScale();
        }
        private void AnimateTo(float multiplier)
        {
            StopTween();
            if (transitionDuration <= 0f) { transform.localScale = baseScale * multiplier; return; }
            scaleTween = transform.DOScale(baseScale * multiplier, transitionDuration)
                .SetEase(transitionEase)
                .SetUpdate(true)
                .OnComplete(() => scaleTween = null);
        }
        private void StopTween()
        {
            scaleTween?.Kill();
            scaleTween = null;
        }
        private void ResetScale()
        {
            StopTween();
            if (button != null) transform.localScale = baseScale;
        }
        private void OnDisable() => ResetScale();
    }
}
