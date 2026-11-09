using DG.Tweening;
using UnityEngine;

namespace DuelHero.Battle
{
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraController : MonoBehaviour
    {
        private Camera targetCamera;
        private Vector3 basePosition, battlePosition;
        private Quaternion baseRotation;
        private float baseSize, baseFov;
        private float baseZoom = 1f, battleZoom = 1f, battlePitch;
        private float entryZoom = 1f;
        private Tween entryZoomMotion;
        private Tween impactShake;
        private Vector2 impactOffset;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            basePosition = transform.position;
            baseRotation = transform.rotation;
            baseSize = targetCamera.orthographicSize;
            baseFov = targetCamera.fieldOfView;
        }

        // Future input changes the base view without overwriting battle offsets.
        public void SetBaseView(Vector3 position, Quaternion rotation, float zoom)
        {
            basePosition = position;
            baseRotation = rotation;
            baseZoom = zoom;
            ApplyView();
        }

        public void SetBattleView(Vector3 positionOffset, float pitchOffset, float zoom)
        {
            battlePosition = positionOffset;
            battlePitch = pitchOffset;
            battleZoom = zoom;
            ApplyView();
        }

        public void ResetBattleView() => SetBattleView(Vector3.zero, 0f, 1f);

        internal void PlayEntryZoom()
        {
            if (!isActiveAndEnabled) return;
            entryZoomMotion?.Kill();
            entryZoom = 1.15f;
            ApplyView();
            entryZoomMotion = DOTween.To(() => entryZoom, zoom =>
            {
                entryZoom = zoom;
                ApplyView();
            }, 1f, .8f).SetEase(Ease.InOutSine).SetUpdate(true)
                .OnComplete(() => entryZoomMotion = null);
        }

        internal void PlayRiftImpactShake()
        {
            if (!isActiveAndEnabled) return;
            impactShake?.Kill();
            impactShake = DOVirtual.Float(0f, 1f, .2f, progress =>
            {
                float envelope = 1f - progress;
                float phase = progress * Mathf.PI * 6f;
                impactOffset = new Vector2(Mathf.Sin(phase) * .09f, Mathf.Sin(phase * .8f) * .055f) * envelope;
                ApplyView();
            }).SetEase(Ease.Linear).OnKill(() =>
            {
                impactShake = null;
                impactOffset = Vector2.zero;
                ApplyView();
            });
        }

        private void ApplyView()
        {
            if (targetCamera == null) return;
            var rotation = baseRotation * Quaternion.Euler(battlePitch, 0f, 0f);
            var shakePosition = rotation * new Vector3(impactOffset.x, impactOffset.y, 0f);
            transform.SetPositionAndRotation(basePosition + battlePosition + shakePosition, rotation);
            float zoom = baseZoom * battleZoom * entryZoom;
            if (targetCamera.orthographic) targetCamera.orthographicSize = baseSize / zoom;
            else targetCamera.fieldOfView = baseFov / zoom;
        }

        private void OnDisable()
        {
            impactShake?.Kill();
            entryZoomMotion?.Kill();
            entryZoomMotion = null;
            entryZoom = 1f;
            ResetBattleView();
        }
    }
}
