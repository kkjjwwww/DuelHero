using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class CardRewardUI : MonoBehaviour
    {
        [SerializeField] private RectTransform resultPanel;
        [SerializeField] private RectTransform resultTitle;
        [SerializeField] private GameObject heading;
        [SerializeField] private RectTransform actionButton;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text message;
        [SerializeField] private CardView cardPrefab;
        private readonly List<CardView> views = new();
        private KeywordTooltip tooltip;
        private bool showing;
        private bool inputEnabled;
        private Vector2 panelSize, titlePosition, buttonPosition;
        private bool headingActive;
        public event Action<string> CardSelected;

        public void Show(IReadOnlyList<CardDefinition> cards, CardDatabase database)
        {
            if (showing) return;
            showing = true;
            panelSize = resultPanel.sizeDelta;
            titlePosition = resultTitle.anchoredPosition;
            buttonPosition = actionButton.anchoredPosition;
            headingActive = heading.activeSelf;
            resultPanel.sizeDelta = new Vector2(920, 560);
            resultTitle.anchoredPosition = new Vector2(0, 225);
            actionButton.anchoredPosition = new Vector2(0, -225);
            heading.SetActive(false);
            content.gameObject.SetActive(true);
            message.text = cards.Count == 0 ? "획득 가능한 카드가 없습니다." : "카드를 클릭하면 획득하고 맵으로 돌아갑니다.";
            Font font = message.font;
            tooltip = KeywordTooltip.Create(GetComponentInParent<Canvas>().rootCanvas, font);
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
                card.SetKeywordHoverActions((keyword, position) => tooltip.Show(keyword, position),
                    () => tooltip.Hide(), position => tooltip.Move(position));
                views.Add(card);
                var label = new GameObject("Rarity", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(card.transform, false);
                label.font = font;
                label.text = definition.rarity;
                label.fontSize = 13;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = RarityColor(definition.rarity);
                label.raycastTarget = false;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0);
                label.rectTransform.sizeDelta = new Vector2(132, 24);
                label.rectTransform.anchoredPosition = new Vector2(0, -15);
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
            foreach (var card in views) card.SetState(string.Empty, 0, value);
            if (!value && tooltip != null) tooltip.Hide();
        }

        public void Hide()
        {
            if (!showing) return;
            showing = false;
            inputEnabled = false;
            if (tooltip != null) { Destroy(tooltip.gameObject); tooltip = null; }
            foreach (var card in views) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            views.Clear();
            content.gameObject.SetActive(false);
            resultPanel.sizeDelta = panelSize;
            resultTitle.anchoredPosition = titlePosition;
            actionButton.anchoredPosition = buttonPosition;
            heading.SetActive(headingActive);
        }

        private static Color RarityColor(string rarity) => rarity switch
        {
            "고급" => new Color(.4f, .85f, .5f),
            "희귀" => new Color(.4f, .65f, 1),
            "전설" => new Color(1, .7f, .2f),
            _ => new Color(.9f, .9f, .9f)
        };
    }
}
