using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace DuelHero.Units
{
    [DisallowMultipleComponent]
    public sealed class UnitJumpPresentation : MonoBehaviour
    {
        [SerializeField] private UnitSpritePresenter sprites;
        [SerializeField] private Transform motionRoot;
        [SerializeField, Min(0f)] private float takeoffDuration = 0.1f;
        [SerializeField, Min(0.01f)] private float flightDuration = 0.3f;
        [SerializeField, Min(0f)] private float landingDuration = 0.1f;
        [SerializeField, Min(0f)] private float jumpHeight = 0.2f;

        public bool IsPlaying { get; private set; }
        public Vector3 MotionWorldPosition => motionRoot != null ? motionRoot.position : transform.position;
        private Tween motion;
        private Vector3 restPosition;
        private int version;

        // Logical movement is already committed. Rewind its world displacement visually,
        // then animate MotionRoot back to rest at the landing cell.
        public IEnumerator PlayJump(Vector3 movementDelta, System.Action onTakeoff = null)
        {
            if (!isActiveAndEnabled || IsPlaying) yield break;
            if (motionRoot == null || motionRoot.parent == null)
            {
                Debug.LogError("점프 연출용 MotionRoot와 부모 Transform을 연결해주세요.", this);
                yield break;
            }

            int run = ++version;
            IsPlaying = true;
            restPosition = motionRoot.localPosition;
            Vector3 offset = motionRoot.parent.InverseTransformVector(-movementDelta);
            Vector3 lift = motionRoot.parent.InverseTransformVector(Vector3.up * jumpHeight);
            try
            {
                motionRoot.localPosition = restPosition + offset;
                if (sprites != null) sprites.SetPose(UnitPose.JumpGround);
                if (takeoffDuration > 0f) yield return new WaitForSeconds(takeoffDuration);
                if (run != version || !isActiveAndEnabled) yield break;

                onTakeoff?.Invoke();
                if (sprites != null) sprites.SetPose(UnitPose.JumpAir);
                float progress = 0f;
                motion = DOTween.To(() => progress, value =>
                {
                    progress = value;
                    if (motionRoot != null)
                        motionRoot.localPosition = restPosition + offset * (1f - value) + lift * (4f * value * (1f - value));
                }, 1f, Mathf.Max(0.01f, flightDuration)).SetEase(Ease.Linear);
                yield return motion.WaitForCompletion(true);
                if (run != version || !isActiveAndEnabled) yield break;

                motionRoot.localPosition = restPosition;
                if (sprites != null) sprites.SetPose(UnitPose.JumpGround);
                if (landingDuration > 0f) yield return new WaitForSeconds(landingDuration);
            }
            finally
            {
                if (run == version) Cancel();
            }
        }

        public void Cancel()
        {
            version++;
            motion?.Pause();
            motion?.Kill();
            motion = null;
            if (IsPlaying && motionRoot != null) motionRoot.localPosition = restPosition;
            IsPlaying = false;
            if (sprites != null) sprites.ResetPose();
        }

        private void OnDisable() => Cancel();
    }
}
