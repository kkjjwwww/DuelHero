using DG.Tweening;
using UnityEngine;

namespace DuelHero.Battle
{
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int standardImpactDamage = 30;
        [SerializeField, Min(1)] private int heavyImpactDamage = 50;
        [SerializeField] private Vector2 standardShakeStrength = new(.008f, .065f);
        [SerializeField] private Vector2 standardShakeDuration = new(.10f, .22f);
        [SerializeField] private Vector2 standardHitStop = new(.06f, .14f);
        [SerializeField, Min(0f)] private float lethalHitStop = .18f;
        [SerializeField, Min(0f)] private float heavyShakeStrength = .22f;
        [SerializeField, Min(0f)] private float heavyShakeDuration = .32f;
        [SerializeField, Min(0f)] private float heavyHitStop = .24f;

        private Camera targetCamera;
        private Vector3 basePosition, battlePosition;
        private Quaternion baseRotation;
        private float baseSize, baseFov;
        private float baseZoom = 1f, battleZoom = 1f, battlePitch;
        private float entryZoom = 1f;
        private Tween entryZoomMotion;
        private Tween impactShake;
        private float activeShakeStrength;
        private bool hitStopActive;
        private double hitStopUntil;
        private Vector2 impactOffset;
        internal Vector3 ImpactWorldOffset => transform.rotation * new Vector3(impactOffset.x, impactOffset.y, 0f);
        private Tween energyFocusMotion;
        private float energyFocusBlend;
        private float energyZoom = 1f;
        private System.Func<Vector3> energyFocus;

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

        internal void PlayRiftImpactShake() => PlayImpactShake(.09f, .2f);

        internal void PlayDamageImpact(int damage, bool lethal, bool statusDamage)
        {
            if (!isActiveAndEnabled || damage <= 0) return;
            float ratio = Mathf.InverseLerp(1f, Mathf.Max(2, standardImpactDamage), damage);
            bool heavy = damage >= heavyImpactDamage;
            PlayImpactShake(
                heavy ? heavyShakeStrength : Mathf.Lerp(standardShakeStrength.x, standardShakeStrength.y, ratio),
                heavy ? heavyShakeDuration : Mathf.Lerp(standardShakeDuration.x, standardShakeDuration.y, ratio));

            float duration = heavy ? heavyHitStop :
                statusDamage ? 0f : Mathf.Lerp(standardHitStop.x, standardHitStop.y, ratio);
            if (lethal) duration = Mathf.Max(duration, lethalHitStop);
            if (duration <= 0f) return;

            double deadline = Time.realtimeSinceStartupAsDouble + duration;
            if (!hitStopActive)
            {
                GameTimePause.Acquire();
                hitStopActive = true;
                hitStopUntil = deadline;
            }
            else hitStopUntil = System.Math.Max(hitStopUntil, deadline);
        }

        private void EndHitStop()
        {
            if (!hitStopActive) return;
            hitStopActive = false;
            GameTimePause.Release();
        }

        internal void PlayImpactShake(float strength, float duration)
        {
            if (!isActiveAndEnabled) return;
            if (strength <= 0f || duration <= 0f) return;
            if (impactShake != null && strength < activeShakeStrength) return;
            impactShake?.Kill();
            activeShakeStrength = strength;
            impactShake = DOVirtual.Float(0f, 1f, duration, progress =>
            {
                float envelope = 1f - progress;
                float phase = progress * Mathf.PI * 6f;
                impactOffset = new Vector2(Mathf.Sin(phase) * strength, Mathf.Sin(phase * .8f) * (strength * (.055f / .09f))) * envelope;
                ApplyView();
            }).SetEase(Ease.Linear).SetUpdate(true).OnKill(() =>
            {
                impactShake = null;
                impactOffset = Vector2.zero;
                ApplyView();
            });
        }

        internal void BeginEnergyFocus(System.Func<Vector3> focus, float zoom, float duration)
        {
            CancelEnergyFocus();
            if (!isActiveAndEnabled) return;
            energyFocus = focus;
            energyZoom = zoom;
            energyFocusMotion = DOVirtual.Float(0f, 1f, duration, value => energyFocusBlend = value)
                .SetEase(Ease.InOutSine);
        }

        internal void EndEnergyFocus(float duration)
        {
            if (energyFocus == null) return;
            energyFocusMotion?.Kill();
            energyFocusMotion = DOVirtual.Float(energyFocusBlend, 0f, duration, value => energyFocusBlend = value)
                .SetEase(Ease.InOutSine).OnComplete(() =>
                {
                    energyFocusMotion = null;
                    energyFocus = null;
                    ApplyView();
                });
        }

        internal void CancelEnergyFocus()
        {
            if (energyFocus != null) impactShake?.Kill();
            energyFocusMotion?.Kill();
            energyFocusMotion = null;
            energyFocus = null;
            energyFocusBlend = 0f;
            ApplyView();
        }

        private void LateUpdate()
        {
            if (hitStopActive && Time.realtimeSinceStartupAsDouble >= hitStopUntil) EndHitStop();
            if (energyFocus != null) ApplyView();
        }

        private void ApplyView()
        {
            if (targetCamera == null) return;
            var rotation = baseRotation * Quaternion.Euler(battlePitch, 0f, 0f);
            var shakePosition = rotation * new Vector3(impactOffset.x, impactOffset.y, 0f);
            var position = basePosition + battlePosition;
            if (energyFocus != null)
                position += Vector3.ProjectOnPlane(energyFocus() - position, rotation * Vector3.forward) * energyFocusBlend;
            transform.SetPositionAndRotation(position + shakePosition, rotation);
            float zoom = baseZoom * battleZoom * entryZoom * Mathf.Lerp(1f, energyZoom, energyFocusBlend);
            if (targetCamera.orthographic) targetCamera.orthographicSize = baseSize / zoom;
            else targetCamera.fieldOfView = baseFov / zoom;
        }

        private void OnDisable()
        {
            EndHitStop();
            CancelEnergyFocus();
            impactShake?.Kill();
            entryZoomMotion?.Kill();
            entryZoomMotion = null;
            entryZoom = 1f;
            ResetBattleView();
        }
    }
}
