using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

namespace DuelHero.Cards
{
    [DisallowMultipleComponent]
    public sealed class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(1f)] private float hoverScale = 1.15f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.1f;
        [SerializeField] private Ease transitionEase = Ease.OutQuad;
        [SerializeField] private bool shrinkOnPress;
        [SerializeField, Range(.1f, 1f)] private float pressedScale = .95f;
        [Tooltip("비워두면 자기 자신을 확대합니다. 여러 대상을 지정하면 함께 확대합니다.")]
        [SerializeField] private Transform[] scaleTargets = new Transform[0];
        private Button button;
        private Transform[] targets;
        private Vector3[] baseScales;
        private Tween[] scaleTweens;
        private bool selected;
        private bool hovered;
        private bool pressed;

        private void Awake()
        {
            InitializeTargets();
        }
        public void ConfigureTargets(params Transform[] values)
        {
            ResetScale();
            scaleTargets = values ?? new Transform[0];
            InitializeTargets();
        }
        private void InitializeTargets()
        {
            button = GetComponent<Button>();
            var uniqueTargets = new System.Collections.Generic.List<Transform>();
            foreach (var target in scaleTargets)
                if (target != null && !uniqueTargets.Contains(target)) uniqueTargets.Add(target);
            if (uniqueTargets.Count == 0) uniqueTargets.Add(transform);
            targets = uniqueTargets.ToArray();
            baseScales = new Vector3[targets.Length];
            scaleTweens = new Tween[targets.Length];
            for (int i = 0; i < targets.Length; i++) baseScales[i] = targets[i].localScale;
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (CanAnimate()) UpdateScale();
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
            if (selected && isActiveAndEnabled) AnimateTo(Mathf.Max(1f, hoverScale));
            else if (CanAnimate()) AnimateTo(1f);
            else ResetScale();
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!shrinkOnPress || eventData.button != PointerEventData.InputButton.Left || !CanAnimate()) return;
            pressed = true;
            UpdateScale();
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed || eventData.button != PointerEventData.InputButton.Left) return;
            pressed = false;
            UpdateScale();
        }
        private void UpdateScale() => AnimateTo(pressed && CanAnimate() ? pressedScale
            : selected || (hovered && CanAnimate()) ? Mathf.Max(1f, hoverScale) : 1f);
        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            if (targets == null || !isActiveAndEnabled) return;
            UpdateScale();
        }
        private bool CanAnimate() => isActiveAndEnabled &&
            (button == null || (button.IsActive() && button.IsInteractable()));
        // The direction UI calls this when it changes button availability.
        public void RefreshInteractability()
        {
            if (button != null && (!button.IsActive() || !button.IsInteractable()))
            {
                hovered = false;
                pressed = false;
                if (selected && isActiveAndEnabled) AnimateTo(Mathf.Max(1f, hoverScale));
                else ResetScale();
            }
        }
        private void AnimateTo(float multiplier)
        {
            StopTween();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                var scale = baseScales[i] * multiplier;
                if (transitionDuration <= 0f) targets[i].localScale = scale;
                else scaleTweens[i] = targets[i].DOScale(scale, transitionDuration)
                    .SetEase(transitionEase).SetUpdate(true);
            }
        }
        private void StopTween()
        {
            if (scaleTweens == null) return;
            for (int i = 0; i < scaleTweens.Length; i++)
            {
                scaleTweens[i]?.Kill();
                scaleTweens[i] = null;
            }
        }
        private void ResetScale()
        {
            StopTween();
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].localScale = baseScales[i];
        }
        private void OnDisable()
        {
            selected = hovered = pressed = false;
            ResetScale();
        }
    }
}
