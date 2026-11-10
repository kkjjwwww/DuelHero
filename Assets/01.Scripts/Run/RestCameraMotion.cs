using DG.Tweening;
using UnityEngine;

namespace DuelHero.Run
{
    [RequireComponent(typeof(Camera))]
    public sealed class RestCameraMotion : MonoBehaviour
    {
        [SerializeField] private Vector2 driftDistance = new(.012f, .0045f);
        [SerializeField, Min(.1f)] private float driftPeriod = 8f;

        private Camera sceneCamera;
        private float restFov;
        private Vector3 restPosition;
        private Tween motion;

        private void Awake()
        {
            sceneCamera = GetComponent<Camera>();
            restFov = sceneCamera.fieldOfView;
            restPosition = transform.localPosition;
        }

        internal void PlayEntryZoom()
        {
            if (!isActiveAndEnabled) return;
            motion?.Kill();
            transform.localPosition = restPosition;
            sceneCamera.fieldOfView = restFov / 1.15f;
            motion = DOVirtual.Float(1.15f, 1f, .8f, zoom => sceneCamera.fieldOfView = restFov / zoom)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .OnComplete(PlayDrift);
        }

        private void PlayDrift()
        {
            sceneCamera.fieldOfView = restFov;
            var cameraAxes = transform.localRotation;
            motion = DOVirtual.Float(0f, 2f * Mathf.PI, driftPeriod, phase =>
                {
                    var offset = new Vector3(
                        Mathf.Sin(phase) * driftDistance.x,
                        (1f - Mathf.Cos(phase)) * .5f * driftDistance.y,
                        0f);
                    transform.localPosition = restPosition + cameraAxes * offset;
                })
                .SetEase(Ease.Linear)
                .SetLoops(-1)
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            motion?.Kill();
            motion = null;
            transform.localPosition = restPosition;
            sceneCamera.fieldOfView = restFov;
        }
    }
}
