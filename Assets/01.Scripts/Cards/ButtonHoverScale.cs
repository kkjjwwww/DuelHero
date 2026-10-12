using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

namespace DuelHero.Cards
{
    [DisallowMultipleComponent]
    public sealed class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(1f)] private float hoverScale = 1.15f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.1f;
        [SerializeField] private Ease transitionEase = Ease.OutQuad;
        [Tooltip("비워두면 자기 자신을 확대합니다. 여러 대상을 지정하면 함께 확대합니다.")]
        [SerializeField] private Transform[] scaleTargets = new Transform[0];
        private Button button;
        private Transform[] targets;
        private Vector3[] baseScales;
        private Tween[] scaleTweens;

        private void Awake()
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
            if (CanAnimate()) AnimateTo(Mathf.Max(1f, hoverScale));
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (CanAnimate()) AnimateTo(1f);
            else ResetScale();
        }
        private bool CanAnimate() => isActiveAndEnabled &&
            (button == null || (button.IsActive() && button.IsInteractable()));
        // The direction UI calls this when it changes button availability.
        public void RefreshInteractability()
        {
            if (button != null && (!button.IsActive() || !button.IsInteractable())) ResetScale();
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
        private void OnDisable() => ResetScale();
    }
}
