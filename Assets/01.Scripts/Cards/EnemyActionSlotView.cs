using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    // Displays one action; never generates plans or executes effects.
    public sealed class EnemyActionSlotView : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private GameObject frontFace;
        [SerializeField] private GameObject backFace;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text costLabel;
        [SerializeField] private Text effectLabel;
        [SerializeField] private CanvasGroup cardCanvasGroup;
        [SerializeField] private GameObject executionHighlight;
        [Header("Execution Scale")]
        [SerializeField, Min(1f)] private float executionScale = 1.1f;
        [SerializeField, Min(0f)] private float executionMoveDistance = 12f;
        [Header("Result Inspection")]
        [SerializeField, Min(1f)] private float resultHoverScale = 1.55f;
        [SerializeField, Min(0f)] private float resultHoverDuration = 0.15f;
        [Header("Card Front")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private CardDatabase cardDatabase;
        private CardView frontCard;
        private KeywordTooltip keywordTooltip;
        private Font cardFont;
        private CardDefinition card;
        private Sequence flip;
        private Tween executionTween;
        private CardInspectionPresenter inspection;
        private bool canInspect;
        private System.Action openCardList;
        private Vector3 frontScale, backScale;
        private Vector2 frontPosition, backPosition;
        private bool scalesCaptured;
        public bool IsRevealing { get; private set; }
        public bool IsRevealed { get; private set; }
        public bool IsExecuting { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsPresented { get; private set; }

        public void Bind(CardDefinition value)
        {
            SetResultInspection(false, null);
            IsPresented = false;
            StopFlip();
            card = value;
            IsRevealed = IsExecuting = IsCompleted = false;
            nameLabel.text = card?.name ?? "대기";
            costLabel.text = card == null ? "" : $"에너지 {card.energyCost}";
            effectLabel.text = card?.ResolvedDescription() ?? "";
            BindCardFront();
            RefreshAppearance();
        }
        private void BindCardFront()
        {
            bool useCard = card != null && cardPrefab != null && cardDatabase != null;
            nameLabel.gameObject.SetActive(!useCard);
            costLabel.gameObject.SetActive(!useCard);
            effectLabel.gameObject.SetActive(!useCard);
            var background = frontFace.GetComponent<Image>();
            if (background != null) background.enabled = !useCard;
            if (keywordTooltip != null) keywordTooltip.Hide();
            if (!useCard)
            {
                if (frontCard != null) frontCard.gameObject.SetActive(false);
                return;
            }
            if (frontCard == null)
            {
                frontCard = Instantiate(cardPrefab, frontFace.transform, false);
                frontCard.name = "EnemyCardFront";
                var font = cardFont = DuelHero.UI.GameFonts.UI;
                frontCard.SetFont(font);
                frontCard.SetClickAction(OpenCardList);
                var hover = frontCard.GetComponent<CardHoverInfo>() ?? frontCard.gameObject.AddComponent<CardHoverInfo>();
                hover.ShowInfo = ShowInspection;
                hover.HideInfo = null;
                frontCard.SetKeywordHoverActions((keyword, position) =>
                {
                    if (keywordTooltip == null) keywordTooltip = KeywordTooltip.Create(GetComponentInParent<Canvas>().rootCanvas, font);
                    keywordTooltip.Show(keyword, position);
                }, () => { if (keywordTooltip != null) keywordTooltip.Hide(); },
                position => { if (keywordTooltip != null) keywordTooltip.Move(position); });
            }
            frontCard.gameObject.SetActive(true);
            frontCard.Bind(card, cardDatabase.keywords);
            frontCard.SetFrontVisible(true);
            frontCard.SetState(string.Empty, 0, true);
            var rect = (RectTransform)frontCard.transform;
            var bounds = ((RectTransform)frontFace.transform).rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * Mathf.Min(bounds.width / rect.rect.width, bounds.height / rect.rect.height);
        }
        public void Reveal()
        {
            StopFlip();
            IsRevealed = true;
            RefreshAppearance();
        }
        public void Reveal(float duration)
        {
            if (IsRevealed || IsRevealing) return;
            if (duration <= 0f || !isActiveAndEnabled) { Reveal(); return; }
            CaptureScales();
            var targetScale = frontScale * (IsPresented ? executionScale : 1f);
            IsRevealing = true;
            cardCanvasGroup.blocksRaycasts = false;
            flip = DOTween.Sequence();
            flip.Append(backFace.transform.DOScaleX(0f, duration * .5f).SetEase(Ease.InQuad));
            flip.AppendCallback(() =>
            {
                IsRevealed = true;
                frontFace.transform.localScale = new Vector3(0f, targetScale.y, targetScale.z);
                RefreshAppearance();
            });
            flip.Append(frontFace.transform.DOScaleX(targetScale.x, duration * .5f).SetEase(Ease.OutQuad));
            flip.OnComplete(() =>
            {
                flip = null;
                IsRevealing = false;
                RestoreScales();
                RefreshAppearance();
            });
        }
        private void CaptureScales()
        {
            if (scalesCaptured) return;
            frontScale = frontFace.transform.localScale;
            backScale = backFace.transform.localScale;
            frontPosition = ((RectTransform)frontFace.transform).anchoredPosition;
            backPosition = ((RectTransform)backFace.transform).anchoredPosition;
            scalesCaptured = true;
        }
        private void RestoreScales()
        {
            if (!scalesCaptured) return;
            float multiplier = IsPresented ? executionScale : 1f;
            var offset = IsPresented ? Vector2.down * executionMoveDistance : Vector2.zero;
            frontFace.transform.localScale = frontScale * multiplier;
            backFace.transform.localScale = backScale * multiplier;
            ((RectTransform)frontFace.transform).anchoredPosition = frontPosition + offset;
            ((RectTransform)backFace.transform).anchoredPosition = backPosition + offset;
        }
        private void StopFlip()
        {
            flip?.Kill();
            flip = null;
            executionTween?.Kill();
            executionTween = null;
            IsRevealing = false;
            RestoreScales();
        }
        public void BeginExecution()
        {
            if (!IsPresented) Present(0f);
            StopFlip();
            IsRevealed = IsExecuting = true;
            IsCompleted = false;
            RefreshAppearance();
        }
        public void CompleteExecution()
        {
            IsExecuting = false;
            IsCompleted = true;
            RefreshAppearance();
        }
        public void Present(float duration)
        {
            if (IsPresented) return;
            CaptureScales();
            IsPresented = true;
            executionTween?.Kill();
            executionTween = null;
            var front = (RectTransform)frontFace.transform;
            var back = (RectTransform)backFace.transform;
            var offset = Vector2.down * executionMoveDistance;
            if (duration <= 0f || !isActiveAndEnabled) RestoreScales();
            else
                executionTween = DOTween.Sequence()
                    .Join(front.DOScale(frontScale * executionScale, duration).SetEase(Ease.OutQuad))
                    .Join(front.DOAnchorPos(frontPosition + offset, duration).SetEase(Ease.OutQuad))
                    .Join(back.DOScale(backScale * executionScale, duration).SetEase(Ease.OutQuad))
                    .Join(back.DOAnchorPos(backPosition + offset, duration).SetEase(Ease.OutQuad));
        }
        private void RefreshAppearance()
        {
            frontFace.SetActive(IsRevealed);
            backFace.SetActive(!IsRevealed);
            if (executionHighlight != null) executionHighlight.SetActive(false);
            cardCanvasGroup.alpha = 1f;
            cardCanvasGroup.blocksRaycasts = !IsRevealing;
        }
        public void SetResultInspection(bool enabled, System.Action onClick)
        {
            canInspect = enabled;
            openCardList = onClick;
            if (!enabled) HideInspection();
            RefreshAppearance();
        }
        private void OpenCardList()
        {
            if (!canInspect) return;
            HideInspection();
            openCardList?.Invoke();
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            // The slot's old hit rectangle can extend beyond the moved card.
            // Wait for the actual card's enter event in that part of the slot.
            if (eventData != null && frontCard != null)
            {
                var canvas = GetComponentInParent<Canvas>().rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var hitArea = frontCard.GetComponent<CardRaycastArea>();
                if (hitArea != null && !hitArea.IsRaycastLocationValid(eventData.position, camera)) return;
            }
            ShowInspection();
        }
        private void ShowInspection()
        {
            if (!canInspect || !IsRevealed || card == null || cardPrefab == null || cardDatabase == null) return;
            if (inspection == null) inspection = CardInspectionPresenter.GetOrCreate(GetComponentInParent<Canvas>().rootCanvas);
            inspection.Show(InspectionAnchor, cardPrefab, card, cardDatabase, cardFont,
                click: OpenCardList, scale: resultHoverScale, duration: resultHoverDuration);
        }
        private RectTransform InspectionAnchor => (RectTransform)(frontCard != null ? frontCard.transform : frontFace.transform);
        private void HideInspection()
        {
            if (inspection != null) inspection.Hide(InspectionAnchor);
            if (keywordTooltip != null) keywordTooltip.Hide();
        }
        private void OnDisable()
        {
            HideInspection();
            if (IsRevealing) IsRevealed = true;
            StopFlip();
            RefreshAppearance();
            if (keywordTooltip != null) keywordTooltip.Hide();
        }
        private void OnDestroy()
        {
            if (keywordTooltip != null) Destroy(keywordTooltip.gameObject);
        }
    }
}
