using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class CardRewardUI : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Image screenBackground;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionLabel, goldLabel;
        [SerializeField, Range(0f, 1f), InspectorName("보상 배경 불투명도")]
        private float backgroundOpacity = .88f;
        private readonly List<CardView> views = new();
        private KeywordTooltip tooltip;
        private CardRangePreview rangePreview;
        private bool showing;
        private bool inputEnabled;
        public bool IsShowing => showing;
        public event Action<string> CardSelected;
        public event Action ActionRequested;

        public void Show(IReadOnlyList<CardDefinition> cards, CardDatabase database, int gold)
        {
            if (showing) return;
            showing = true;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            screenBackground.gameObject.SetActive(true);
            screenBackground.transform.SetAsLastSibling();
            ApplyBackgroundOpacity();
            actionButton.onClick.RemoveListener(RequestAction);
            actionButton.onClick.AddListener(RequestAction);
            content.gameObject.SetActive(true);
            goldLabel.text = "획득 예정 골드  +" + gold;
            actionLabel.text = cards.Count == 0 ? "계속" : "건너뛰기";
            message.text = cards.Count == 0 ? "획득 가능한 카드가 없습니다." : "카드를 클릭하면 획득하고 맵으로 돌아갑니다.";
            Font font = DuelHero.UI.GameFonts.UI;
            tooltip = KeywordTooltip.Create(GetComponentInParent<Canvas>().rootCanvas);
            rangePreview = CardRangePreview.GetOrCreate(GetComponentInParent<Canvas>().rootCanvas);
            for (int i = 0; i < cards.Count; i++)
            {
                var definition = cards[i];
                var card = Instantiate(cardPrefab, content, false);
                card.name = "Reward_" + definition.id;
                var rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(132, 132 * 88f / 63f);
                rect.localScale = Vector3.one * 1.65f;
                rect.anchoredPosition = new Vector2((i - (cards.Count - 1) * .5f) * 260f, 5);
                card.SetFont(font);
                card.Bind(definition, database.keywords);
                card.SetFrontVisible(true);
                card.SetState(string.Empty, 0, true);
                card.SetClickAction(() => Select(definition.id));
                var hover = card.GetComponent<CardHoverInfo>();
                hover.ShowInfo = () => { if (inputEnabled) rangePreview.Show(card); };
                hover.HideInfo = () => rangePreview.Hide(card);
                card.SetKeywordHoverActions((keyword, position) => tooltip.Show(keyword, position),
                    () => tooltip.Hide(), position => tooltip.Move(position));
                views.Add(card);
            }
            SetInteractable(true);
        }

        private void Select(string id)
        {
            if (!inputEnabled) return;
            SetInteractable(false);
            CardSelected?.Invoke(id);
        }

        public void SetInteractable(bool value)
        {
            inputEnabled = value;
            actionButton.interactable = value;
            foreach (var card in views)
            {
                card.SetState(string.Empty, 0, value);
                if (!value) rangePreview.Hide(card);
            }
            if (!value && tooltip != null) tooltip.Hide();
        }

        public void ShowReturn(int gold, bool transitioning, string error)
        {
            SetInteractable(false);
            foreach (var card in views) card.gameObject.SetActive(false);
            goldLabel.text = "획득 골드  +" + gold;
            message.text = transitioning ? "맵으로 이동 중…" : error;
            actionLabel.text = "이동 재시도";
            actionButton.gameObject.SetActive(!transitioning && !string.IsNullOrEmpty(error));
            actionButton.interactable = !transitioning;
        }

        private void RequestAction() => ActionRequested?.Invoke();

        private void OnDestroy()
        {
            if (actionButton != null) actionButton.onClick.RemoveListener(RequestAction);
            if (tooltip != null) Destroy(tooltip.gameObject);
        }
        private void ApplyBackgroundOpacity()
        {
            var color = screenBackground.color;
            color.a = backgroundOpacity;
            screenBackground.color = color;
        }

        private void OnValidate()
        {
            if (showing && screenBackground != null) ApplyBackgroundOpacity();
        }
    }
}
