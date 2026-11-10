using DG.Tweening;
using UnityEngine;

namespace DuelHero.Shop
{
    public sealed class ShopAmbientMotion : MonoBehaviour
    {
        [SerializeField] private Transform merchant;
        [SerializeField] private Transform merchantHead;
        [SerializeField] private Transform merchantEyes;
        [SerializeField] private Transform lantern;
        [SerializeField] private Transform shopCamera;
        [SerializeField, Min(0f)] private float breathScale = .01f;
        [SerializeField, Min(.1f)] private float breathPeriod = 4.6f;
        [SerializeField] private float nodAngle = -2.5f;
        [SerializeField, Min(0f)] private float nodPause = 3.5f;
        [SerializeField, Min(.1f)] private float nodDuration = 1.7f;
        [SerializeField, Min(.02f)] private float blinkDuration = .15f;
        [SerializeField, Min(0f)] private float swayAngle = 2.5f;
        [SerializeField, Min(.1f)] private float swayPeriod = 5.6f;
        [SerializeField] private Vector2 cameraDriftDistance = new(.009f, .0035f);
        [SerializeField, Min(.1f)] private float cameraDriftPeriod = 8f;

        private Vector3 merchantRestScale;
        private Quaternion headRestRotation;
        private Vector3 eyesRestScale;
        private Quaternion lanternRestRotation;
        private Vector3 cameraRestPosition;
        private Tween breath;
        private Sequence nod;
        private Sequence blink;
        private Tween sway;
        private Tween cameraDrift;
        private readonly System.Random blinkTiming = new();

        private void OnEnable()
        {
            if (merchant != null)
            {
                merchantRestScale = merchant.localScale;
                breath = merchant.DOScaleY(merchantRestScale.y * (1f + breathScale), breathPeriod * .5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            if (merchantHead != null)
            {
                headRestRotation = merchantHead.localRotation;
                nod = DOTween.Sequence()
                    .AppendInterval(nodPause)
                    .Append(DOVirtual.Float(0f, nodAngle, nodDuration * .5f,
                            angle => merchantHead.localRotation = headRestRotation * Quaternion.AngleAxis(angle, Vector3.forward))
                        .SetEase(Ease.InOutSine)
                        .SetLoops(2, LoopType.Yoyo))
                    .SetLoops(-1);
            }

            if (merchantEyes != null)
            {
                eyesRestScale = merchantEyes.localScale;
                ScheduleBlink();
            }

            if (lantern != null)
            {
                lanternRestRotation = lantern.localRotation;
                sway = DOVirtual.Float(-swayAngle, swayAngle, swayPeriod * .5f,
                        angle => lantern.localRotation = lanternRestRotation * Quaternion.AngleAxis(angle, Vector3.forward))
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
                // Start halfway between the extremes, at the authored rest angle.
                sway.Goto(swayPeriod * .25f, andPlay: true);
            }

            if (shopCamera != null)
            {
                cameraRestPosition = shopCamera.localPosition;
                var cameraAxes = shopCamera.localRotation;
                cameraDrift = DOVirtual.Float(0f, 2f * Mathf.PI, cameraDriftPeriod, phase =>
                    {
                        var offset = new Vector3(
                            Mathf.Sin(phase) * cameraDriftDistance.x,
                            (1f - Mathf.Cos(phase)) * .5f * cameraDriftDistance.y,
                            0f);
                        shopCamera.localPosition = cameraRestPosition + cameraAxes * offset;
                    })
                    .SetEase(Ease.Linear)
                    .SetLoops(-1);
            }
        }

        private void ScheduleBlink()
        {
            var delay = 4f + 3f * (float)blinkTiming.NextDouble();
            blink = DOTween.Sequence()
                .AppendInterval(delay)
                .Append(merchantEyes.DOScaleY(0f, blinkDuration * .5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(2, LoopType.Yoyo))
                .OnComplete(ScheduleBlink);
        }

        private void OnDisable()
        {
            breath?.Kill();
            nod?.Kill();
            blink?.Kill();
            sway?.Kill();
            cameraDrift?.Kill();
            if (merchant != null) merchant.localScale = merchantRestScale;
            if (merchantHead != null) merchantHead.localRotation = headRestRotation;
            if (merchantEyes != null) merchantEyes.localScale = eyesRestScale;
            if (lantern != null) lantern.localRotation = lanternRestRotation;
            if (shopCamera != null) shopCamera.localPosition = cameraRestPosition;
        }
    }
}
