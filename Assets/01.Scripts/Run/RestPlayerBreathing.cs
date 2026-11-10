using DG.Tweening;
using UnityEngine;

namespace DuelHero.Run
{
    public sealed class RestPlayerBreathing : MonoBehaviour
    {
        [SerializeField, Range(0f, .05f)] private float breathScale = .01f;
        [SerializeField, Min(.1f)] private float breathPeriod = 5f;
        [SerializeField] private float seatPivotHeight = .9633f;

        private Vector3 restPosition;
        private Vector3 restScale;
        private Tween breath;

        private void OnEnable()
        {
            restPosition = transform.localPosition;
            restScale = transform.localScale;
            breath = DOVirtual.Float(0f, breathScale, breathPeriod * .5f, ApplyBreath)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void ApplyBreath(float amount)
        {
            float heightChange = restScale.y * amount;
            transform.localScale = restScale + Vector3.up * heightChange;
            // Keep the seated contact point stationary while the sprite stretches.
            transform.localPosition = restPosition - transform.localRotation * Vector3.up * (seatPivotHeight * heightChange);
        }

        private void OnDisable()
        {
            breath?.Kill();
            transform.localPosition = restPosition;
            transform.localScale = restScale;
        }
    }
}
