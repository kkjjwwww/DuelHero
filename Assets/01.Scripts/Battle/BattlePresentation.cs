using System.Collections;
using DG.Tweening;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    public sealed class BattlePresentation : MonoBehaviour
    {
        [SerializeField] private BattleCameraController cameraController;
        [SerializeField] private RectTransform handPanel;
        [SerializeField] private RectTransform[] playerSlots;
        [SerializeField] private RectTransform progressButton;
        [SerializeField, Min(.01f)] private float duration = .35f;
        [SerializeField, Range(0f, 1f)] private float hiddenCardRatio = .70f;
        [SerializeField, Min(0f)] private float slotDrop = 48f;
        [SerializeField, Min(1f)] private float slotScale = 1.15f;
        [SerializeField, Min(1f)] private float cameraZoom = 1.12f;
        [SerializeField] private Vector3 cameraPositionOffset;
        [SerializeField] private float cameraPitchOffset;
        public bool IsActive { get; private set; }
        private Vector2 handPosition;
        private Vector2 buttonPosition;
        private Vector2[] slotPositions;
        private Vector3[] slotScales;
        private float cameraBlend;
        private Sequence motion;

        private void Awake()
        {
            handPosition = handPanel.anchoredPosition;
            buttonPosition = progressButton.anchoredPosition;
            slotPositions = new Vector2[playerSlots.Length];
            slotScales = new Vector3[playerSlots.Length];
            for (int i = 0; i < playerSlots.Length; i++)
            {
                slotPositions[i] = playerSlots[i].anchoredPosition;
                slotScales[i] = playerSlots[i].localScale;
            }
        }

        public IEnumerator Animate(bool active)
        {
            motion?.Kill();
            if (active) IsActive = true;
            var handTarget = handPosition;
            if (active)
            {
                var fan = handPanel.GetComponentInChildren<CardFanLayout>();
                if (fan != null) handTarget.y -= fan.GetHideDistance(hiddenCardRatio);
            }
            motion = DOTween.Sequence().SetUpdate(true);
            motion.Join(handPanel.DOAnchorPos(handTarget, duration));
            float widthMultiplier = active ? slotScale - 1f : 0f;
            float horizontalOffset = 0f;
            for (int i = 0; i < playerSlots.Length; i++)
                horizontalOffset -= playerSlots[i].rect.width * slotScales[i].x * widthMultiplier * .5f;
            for (int i = 0; i < playerSlots.Length; i++)
            {
                float addedWidth = playerSlots[i].rect.width * slotScales[i].x * widthMultiplier;
                horizontalOffset += addedWidth * .5f;
                motion.Join(playerSlots[i].DOAnchorPos(slotPositions[i] + new Vector2(horizontalOffset, active ? -slotDrop : 0f), duration));
                motion.Join(playerSlots[i].DOScale(slotScales[i] * (active ? slotScale : 1f), duration));
                horizontalOffset += addedWidth * .5f;
            }
            motion.Join(progressButton.DOAnchorPos(buttonPosition + new Vector2(horizontalOffset, active ? -slotDrop : 0f), duration));
            motion.Join(DOTween.To(() => cameraBlend, blend =>
            {
                cameraBlend = blend;
                cameraController.SetBattleView(cameraPositionOffset * blend, cameraPitchOffset * blend,
                    Mathf.Lerp(1f, cameraZoom, blend));
            }, active ? 1f : 0f, duration));
            motion.SetEase(Ease.InOutQuad);
            yield return motion.WaitForCompletion();
            motion = null;
            if (!active) IsActive = false;
        }

        private void OnDisable()
        {
            motion?.Kill();
            if (slotPositions == null) return;
            handPanel.anchoredPosition = handPosition;
            progressButton.anchoredPosition = buttonPosition;
            for (int i = 0; i < playerSlots.Length; i++)
            {
                playerSlots[i].anchoredPosition = slotPositions[i];
                playerSlots[i].localScale = slotScales[i];
            }
            cameraBlend = 0f;
            if (cameraController != null) cameraController.ResetBattleView();
            IsActive = false;
        }
    }
}
