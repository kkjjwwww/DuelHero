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
        [SerializeField, Min(0f)] private float windupDuration = 0.3f;
        [SerializeField, Min(0f)] private float strikeDuration = 0.3f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.2f;
        [SerializeField, Min(0f)] private float lungeDistance;
        [SerializeField, Min(.01f)] private float dashDuration = .2f;
        public Vector3 MotionWorldPosition => motionRoot != null ? motionRoot.position : transform.position;

        private DuelHero.Battle.BattlePreviewRenderer attackRange;
        public void ShowRange(GridMovement unit, System.Collections.Generic.IEnumerable<Vector2Int> cells)
        {
            if (!isActiveAndEnabled) return;
            if (attackRange == null)
            {
                var go = new GameObject("ExecutingAttackRange");
                go.transform.SetParent(transform, false);
                attackRange = go.AddComponent<DuelHero.Battle.BattlePreviewRenderer>();
            }
            attackRange.ShowExecuted(new DuelHero.Battle.ExecutedCardPreview(new[] { unit.GridPosition }, cells), unit, null);
        }
        public void HideRange() => attackRange?.Clear();

        public bool IsPlaying { get; private set; }
        private Sequence motion;
        private Vector3 startPosition;
        private int version;

        public IEnumerator PlayAttack(string style = null)
        {
            if (!isActiveAndEnabled || IsPlaying) yield break;
            int run = ++version;
            IsPlaying = true;
            if (motionRoot != null) startPosition = motionRoot.localPosition;
            try
            {
                if (sprites != null) sprites.SetAttackPose(style, false);
                if (windupDuration > 0f) yield return new WaitForSeconds(windupDuration);
                if (run != version || !isActiveAndEnabled) yield break;

                if (sprites != null) sprites.SetAttackPose(style, true);
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

        public IEnumerator PlayDash(Vector3 displacement)
        {
            if (!isActiveAndEnabled || IsPlaying) yield break;
            if (motionRoot == null || motionRoot.parent == null)
                throw new System.InvalidOperationException("돌진 연출용 MotionRoot를 연결해주세요.");
            int run = ++version;
            IsPlaying = true;
            startPosition = motionRoot.localPosition;
            bool completed = false;
            try
            {
                motionRoot.localPosition = startPosition + motionRoot.parent.InverseTransformVector(-displacement);
                if (sprites != null) sprites.SetPose(UnitPose.AttackBefore);
                motion = DOTween.Sequence();
                motion.Append(motionRoot.DOLocalMove(startPosition, dashDuration).SetEase(Ease.Linear));
                yield return motion.WaitForCompletion(true);
                if (run != version || !isActiveAndEnabled) yield break;
                motionRoot.localPosition = startPosition;
                if (sprites != null) sprites.SetPose(UnitPose.AttackAfter);
                completed = true; // Keep the pose until the shared damage batch is applied.
            }
            finally { if (!completed && run == version) Cancel(); }
        }

        public void Cancel()
        {
            HideRange();
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
