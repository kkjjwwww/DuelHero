using System;
using System.Collections;
using UnityEngine;

namespace DuelHero.Units
{
    public enum UnitPose { Idle, AttackBefore, AttackAfter, Block, Heal, JumpGround, JumpAir }

    [DisallowMultipleComponent]
    public sealed class UnitSpritePresenter : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite attackBefore;
        [SerializeField] private Sprite attackAfter;
        [SerializeField] private Sprite block;
        [SerializeField] private Sprite heal;
        [SerializeField] private Sprite jumpGround;
        [SerializeField] private Sprite jumpAir;
        [SerializeField, Min(0f)] private float supportPoseDuration = 0.4f;
        private int supportPoseVersion;
        private bool supportPosePlaying;

        public UnitPose CurrentPose { get; private set; } = UnitPose.Idle;

        private void Reset() => targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        private void OnEnable() => ResetPose();
        private void OnDisable() => CancelSupportPose();

        public IEnumerator PlaySupportPose(UnitPose pose, bool holdUntilResolved = false)
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
                if (supportPoseDuration > 0f) yield return new WaitForSeconds(supportPoseDuration);
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

        public bool SetPose(UnitPose pose)
        {
            Sprite sprite = pose switch
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
            targetRenderer.sprite = sprite;
            CurrentPose = pose;
            return true;
        }

        public bool ResetPose() => SetPose(UnitPose.Idle);
    }
}
