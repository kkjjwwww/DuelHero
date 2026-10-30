using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace DuelHero.Units
{
    [DisallowMultipleComponent]
    public sealed class UnitAttackPresentation : MonoBehaviour
    {
        [SerializeField] private UnitSpritePresenter sprites;
        [SerializeField] private Transform motionRoot;
        [SerializeField] private SpriteRenderer facingRenderer;
        [SerializeField, Min(0f)] private float windupDuration = 0.15f;
        [SerializeField, Min(0f)] private float strikeDuration = 0.18f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.12f;
        [SerializeField, Min(0f)] private float lungeDistance;

        public bool IsPlaying { get; private set; }
        private Sequence motion;
        private Vector3 startPosition;
        private int version;

        public IEnumerator PlayAttack()
        {
            if (!isActiveAndEnabled || IsPlaying) yield break;
            int run = ++version;
            IsPlaying = true;
            if (motionRoot != null) startPosition = motionRoot.localPosition;
            try
            {
                if (sprites != null) sprites.SetPose(UnitPose.AttackBefore);
                if (windupDuration > 0f) yield return new WaitForSeconds(windupDuration);
                if (run != version || !isActiveAndEnabled) yield break;

                if (sprites != null) sprites.SetPose(UnitPose.AttackAfter);
                if (strikeDuration > 0f)
                {
                    if (motionRoot != null && lungeDistance > 0f)
                    {
                        float direction = facingRenderer != null && facingRenderer.flipX ? -1f : 1f;
                        motion = DOTween.Sequence();
                        motion.Append(motionRoot.DOLocalMove(startPosition + Vector3.right * (direction * lungeDistance),
                            strikeDuration * 0.5f).SetEase(Ease.OutQuad));
                        motion.Append(motionRoot.DOLocalMove(startPosition, strikeDuration * 0.5f).SetEase(Ease.InQuad));
                        yield return motion.WaitForCompletion(true);
                    }
                    else yield return new WaitForSeconds(strikeDuration);
                }
                if (run != version || !isActiveAndEnabled) yield break;

                if (sprites != null) sprites.ResetPose();
                if (recoveryDuration > 0f) yield return new WaitForSeconds(recoveryDuration);
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
            if (IsPlaying && motionRoot != null) motionRoot.localPosition = startPosition;
            IsPlaying = false;
            if (sprites != null) sprites.ResetPose();
        }

        private void OnDisable() => Cancel();
    }
}
