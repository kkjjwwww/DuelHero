using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class CardRewardUI : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Text message;
        [SerializeField] private CardView cardPrefab;
        [SerializeField, Range(0f, 1f), InspectorName("보상 배경 불투명도")]
        private float backgroundOpacity = .88f;
        private readonly List<CardView> views = new();
        private KeywordTooltip tooltip;
        private CardRangePreview rangePreview;
        private bool showing;
        private bool inputEnabled;
        private Image screenBackground;
        private Button actionButton;
        private Text actionLabel, goldLabel;
        public bool IsShowing => showing;
        public event Action<string> CardSelected;
        public event Action ActionRequested;

        public void Show(IReadOnlyList<CardDefinition> cards, CardDatabase database, int gold)
        {
            if (showing) return;
            showing = true;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            BuildOverlay(canvas);
            content.gameObject.SetActive(true);
            goldLabel.text = "획득 예정 골드  +" + gold;
            actionLabel.text = cards.Count == 0 ? "계속" : "건너뛰기";
            message.text = cards.Count == 0 ? "획득 가능한 카드가 없습니다." : "카드를 클릭하면 획득하고 맵으로 돌아갑니다.";
            Font font = message.font;
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

        private void BuildOverlay(Canvas canvas)
        {
            if (screenBackground != null) return;
            var overlay = new GameObject("CardRewardOverlay", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)overlay.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            screenBackground = overlay.GetComponent<Image>();
            screenBackground.color = Color.black;
            ApplyBackgroundOpacity();
            content.SetParent(rect, false);

            CreateLabel(rect, "승리", new Vector2(0, 225), 32);
            goldLabel = CreateLabel(rect, "", new Vector2(0, -177), 23);
            var action = new GameObject("RewardAction", typeof(RectTransform), typeof(Image), typeof(Button));
            var actionRect = (RectTransform)action.transform;
            actionRect.SetParent(rect, false);
            actionRect.sizeDelta = new Vector2(220, 46);
            actionRect.anchoredPosition = new Vector2(0, -225);
            action.GetComponent<Image>().color = new Color(.25f, .25f, .25f);
            actionButton = action.GetComponent<Button>();
            actionButton.onClick.AddListener(() => ActionRequested?.Invoke());
            actionLabel = CreateLabel(actionRect, "", Vector2.zero, 22);
        }

        private Text CreateLabel(RectTransform parent, string value, Vector2 position, int size)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.rectTransform.sizeDelta = new Vector2(650, 44);
            label.rectTransform.anchoredPosition = position;
            label.font = message.font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = message.color;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        private void OnDestroy()
        {
            if (screenBackground != null) Destroy(screenBackground.gameObject);
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
