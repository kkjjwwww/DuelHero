using System;
using System.Collections;
using DG.Tweening;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Units
{
    public enum UnitPose { Idle, AttackBefore, AttackAfter, Block, Heal, JumpGround, JumpAir }

    [DisallowMultipleComponent]
    public sealed class UnitSpritePresenter : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private UnitInitializer encounterSource;
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite attackBefore;
        [SerializeField] private Sprite attackAfter;
        [SerializeField] private Sprite thrustBefore, thrustAfter, slashBefore, slashAfter, punchBefore, punchAfter;
        [SerializeField] private Sprite block;
        [SerializeField] private Sprite heal;
        [SerializeField] private Sprite jumpGround;
        [SerializeField] private Sprite jumpAir;
        [SerializeField, Min(0f)] private float supportPoseDuration = 0.4f;
        [SerializeField, Range(0f, 0.1f)] private float idleBreathAmount = 0.02f;
        private CardReservationQueue battleQueue;
        private UnitStats stats;
        private Tween idleBreath;
        private Vector3 restScale;
        private float breathProgress;
        private int supportPoseVersion;
        private bool supportPosePlaying;

        public UnitPose CurrentPose { get; private set; } = UnitPose.Idle;

        private void Reset() => targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        private void Awake()
        {
            battleQueue = FindFirstObjectByType<CardReservationQueue>();
            stats = GetComponentInParent<UnitStats>();
        }

        private void OnEnable() => ResetPose();
        private void OnDisable()
        {
            CancelSupportPose();
            StopIdleBreath();
        }

        private void LateUpdate()
        {
            if (CurrentPose != UnitPose.Idle || idleBreathAmount <= 0f ||
                targetRenderer == null || targetRenderer.sprite == null ||
                (battleQueue != null && battleQueue.IsExecuting) ||
                (stats != null && stats.IsInitialized && stats.Health <= 0))
            {
                StopIdleBreath();
                return;
            }

            if (idleBreath == null)
            {
                restScale = targetRenderer.transform.localScale;
                breathProgress = 0f;
                idleBreath = DOTween.To(() => breathProgress, value => breathProgress = value, 1f, 1.05f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(UnityEngine.Random.Range(0f, 0.65f));
            }

            var scale = restScale;
            scale.y *= 1f + breathProgress * idleBreathAmount;
            targetRenderer.transform.localScale = scale;
        }

        private void StopIdleBreath()
        {
            if (idleBreath == null) return;
            idleBreath.Kill();
            idleBreath = null;
            if (targetRenderer != null) targetRenderer.transform.localScale = restScale;
        }

        public IEnumerator PlaySupportPose(UnitPose pose, bool holdUntilResolved = false, System.Action onStarted = null, float? duration = null)
        {
            if (pose != UnitPose.Block && pose != UnitPose.Heal)
                throw new ArgumentOutOfRangeException(nameof(pose));
            if (!isActiveAndEnabled) yield break;
            CancelSupportPose();
            int run = ++supportPoseVersion;
            supportPosePlaying = true;
            try
            {
                if (!SetPose(pose)) { CancelSupportPose(); yield break; }
                onStarted?.Invoke();
                float holdDuration = duration ?? supportPoseDuration;
                if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);
            }
            finally
            {
                if (run == supportPoseVersion && !holdUntilResolved) CancelSupportPose();
            }
        }

        public void CancelSupportPose()
        {
            supportPoseVersion++;
            if (!supportPosePlaying) return;
            supportPosePlaying = false;
            ResetPose();
        }

        public bool SetAttackPose(string style, bool after)
        {
            Sprite selected = style switch { "thrust" => after ? thrustAfter : thrustBefore, "slash" => after ? slashAfter : slashBefore, "punch" => after ? punchAfter : punchBefore, _ => null };
            if (selected == null || targetRenderer == null) return SetPose(after ? UnitPose.AttackAfter : UnitPose.AttackBefore);
            StopIdleBreath();
            targetRenderer.sprite = selected;
            CurrentPose = after ? UnitPose.AttackAfter : UnitPose.AttackBefore;
            return true;
        }

        public bool SetPose(UnitPose pose)
        {
            var profile = encounterSource != null ? encounterSource.Encounter : null;
            Sprite sprite = profile != null ? (pose switch
            {
                UnitPose.Idle or UnitPose.Heal => profile.idle,
                UnitPose.AttackBefore => profile.attackBefore,
                UnitPose.AttackAfter => profile.attackAfter,
                UnitPose.Block => profile.block,
                UnitPose.JumpGround => profile.jumpGround,
                UnitPose.JumpAir => profile.jumpAir,
                _ => throw new ArgumentOutOfRangeException(nameof(pose))
            }) ?? idle : pose switch
            {
                UnitPose.Idle => idle,
                UnitPose.AttackBefore => attackBefore,
                UnitPose.AttackAfter => attackAfter,
                UnitPose.Block => block,
                UnitPose.Heal => heal,
                UnitPose.JumpGround => jumpGround,
                UnitPose.JumpAir => jumpAir,
                _ => throw new ArgumentOutOfRangeException(nameof(pose), pose, "Unknown unit pose.")
            };
            if (targetRenderer == null || sprite == null)
            {
                Debug.LogError($"캐릭터 SpriteRenderer와 {pose} 이미지를 연결해주세요.", this);
                return false;
            }
            if (pose != UnitPose.Idle) StopIdleBreath();
            targetRenderer.sprite = sprite;
            CurrentPose = pose;
            return true;
        }

        public bool ResetPose() => SetPose(UnitPose.Idle);
    }
}
