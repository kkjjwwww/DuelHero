using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class CardView : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Button button;
        [SerializeField] private RectTransform visualRoot;

        [Header("Front")]
        [SerializeField] private GameObject frontRoot;
        [SerializeField] private Image illustration;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text energyLabel;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private RectTransform keywordLinksRoot;
        [SerializeField] private Text keywordLinkTemplate;
        [SerializeField] private Text stateLabel;
        [SerializeField] private RectTransform descriptionArea;
        [SerializeField] private RectTransform rangeArea;
        [SerializeField] private CardRangeGraphic rangeGraphic;
        [SerializeField] private Image typeBadge;
        [SerializeField] private Text cooldownLabel;

        [Header("Back / State")]
        [SerializeField] private GameObject backRoot;
        [SerializeField] private Material grayscaleMaterial;
        [SerializeField] private Graphic[] grayscaleGraphics = Array.Empty<Graphic>();

        [Header("Type Badges")]
        [SerializeField] private Sprite attackBadge;
        [SerializeField] private Sprite defenseBadge;
        [SerializeField] private Sprite recoveryBadge;
        [SerializeField] private Sprite specialBadge;
        [SerializeField] private Sprite moveBadge;

        private readonly Dictionary<Text, Color> normalTextColors = new();
        private readonly List<Text> keywordLinks = new();
        private Action<KeywordDefinition> onKeywordEnter;
        private Action onKeywordExit;

        public Button Button => button;
        public Text StateLabel => stateLabel;
        public RectTransform VisualRoot => visualRoot;

        public void Bind(CardDefinition card, IEnumerable<KeywordDefinition> keywords)
        {
            if (card == null) return;
            normalTextColors.Clear();
            nameLabel.text = card.name;
            energyLabel.text = card.energyCost.ToString();
            string body = card.ResolvedDescription();
            HashSet<string> ids = new HashSet<string>(card.keywordIds ?? Array.Empty<string>());
            KeywordDefinition[] linkedKeywords = (keywords ?? Enumerable.Empty<KeywordDefinition>())
                .Where(keyword => ids.Contains(keyword.id) && body.Contains($"[{keyword.name}]"))
                .ToArray();
            foreach (KeywordDefinition keyword in linkedKeywords)
                body = body.Replace($"[{keyword.name}]", string.Empty);
            descriptionLabel.text = Regex.Replace(body, @"[ \t]*\r?\n[ \t]*$", string.Empty).TrimEnd();
            BuildKeywordLinks(linkedKeywords);
            typeBadge.sprite = BadgeFor(card.actionType);

            Vector2Int[] range = (card.effects ?? Array.Empty<CardEffectDefinition>())
                .Where(effect => effect.rangeOffsets != null)
                .SelectMany(effect => effect.rangeOffsets)
                .Distinct().ToArray();
            bool hasRange = range.Length > 0;
            rangeArea.gameObject.SetActive(hasRange);
            rangeGraphic.SetOffsets(range);
            descriptionArea.anchorMin = new Vector2(descriptionArea.anchorMin.x, hasRange ? 0.18f : 0.085f);
        }

        public void SetKeywordHoverActions(Action<KeywordDefinition> enter, Action exit)
        {
            onKeywordEnter = enter;
            onKeywordExit = exit;
        }

        internal void NotifyKeywordEnter(KeywordDefinition keyword) => onKeywordEnter?.Invoke(keyword);
        internal void NotifyKeywordExit() => onKeywordExit?.Invoke();

        public void SetState(string state, int remainingCooldown, bool interactable)
        {
            CacheTextColors();
            stateLabel.text = state;
            button.interactable = interactable;
            bool coolingDown = remainingCooldown > 0;
            cooldownLabel.gameObject.SetActive(coolingDown);
            cooldownLabel.text = coolingDown ? remainingCooldown.ToString() : string.Empty;
            foreach (Graphic graphic in grayscaleGraphics)
                if (graphic != null) graphic.material = coolingDown ? grayscaleMaterial : null;
            foreach (KeyValuePair<Text, Color> entry in normalTextColors)
                if (entry.Key != null) entry.Key.color = coolingDown ? Grayscale(entry.Value) : entry.Value;
        }

        public void SetFrontVisible(bool visible)
        {
            frontRoot.SetActive(visible);
            backRoot.SetActive(!visible);
        }

        public void SetIllustration(Sprite sprite)
        {
            illustration.sprite = sprite;
            illustration.enabled = sprite != null;
        }

        public void SetFont(Font font)
        {
            if (font == null) return;
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = font;
        }

        public void SetClickAction(UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }

        private Sprite BadgeFor(string actionType)
        {
            return actionType switch
            {
                "공격" => attackBadge,
                "방어" => defenseBadge,
                "회복" => recoveryBadge,
                "이동" => moveBadge,
                _ => specialBadge
            };
        }

        private void BuildKeywordLinks(IEnumerable<KeywordDefinition> keywords)
        {
            foreach (Text link in keywordLinks)
                if (link != null)
                {
                    if (Application.isPlaying) Destroy(link.gameObject);
                    else DestroyImmediate(link.gameObject);
                }
            keywordLinks.Clear();

            foreach (KeywordDefinition keyword in keywords)
            {
                Text link = Instantiate(keywordLinkTemplate, keywordLinksRoot, false);
                link.name = "Keyword_" + keyword.id;
                link.text = $"[{keyword.name}]";
                link.raycastTarget = true;
                link.gameObject.SetActive(true);
                KeywordHoverLink hover = link.GetComponent<KeywordHoverLink>() ?? link.gameObject.AddComponent<KeywordHoverLink>();
                hover.Initialize(this, keyword);
                keywordLinks.Add(link);
            }
            keywordLinksRoot.gameObject.SetActive(keywordLinks.Count > 0);
        }

        private void CacheTextColors()
        {
            if (normalTextColors.Count > 0) return;
            foreach (Text label in GetComponentsInChildren<Text>(true))
                if (label != cooldownLabel) normalTextColors[label] = label.color;
        }

        private static Color Grayscale(Color color)
        {
            float value = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return new Color(value, value, value, color.a);
        }
    }
}
