using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private TextMeshProUGUI illustrationMaskLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI energyLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private RectTransform descriptionArea;
        [SerializeField] private RectTransform rangeArea;
        [SerializeField] private CardRangeGraphic rangeGraphic;
        [SerializeField] private Image typeBadge;
        [SerializeField] private Text cooldownLabel;
        [SerializeField] private CardReservationOutline reservationOutline;
        [SerializeField] private Text reservationSlotLabel;

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

        private readonly Dictionary<Graphic, Color> normalTextColors = new();
        private Color normalDescriptionColor;
        private KeywordHoverLink keywordHover;
        private static readonly Regex KeywordToken = new Regex(@"\[([^\[\]\r\n]+)\]");
        private const string CooldownLinkId = "__card_cooldown";
        private Action<KeywordDefinition, Vector2> onKeywordEnter;
        private Action<Vector2> onKeywordMove;
        private Action onKeywordExit;
        private int displayedCooldown = int.MinValue;
        private bool? displayedInteractable;
        private int displayedReservationSlot = int.MinValue;
        private static readonly string[] ReservationNumbers = { "1", "2", "3" };
        private bool informationMasked;
        private Sprite illustrationBeforeMask;

        public Button Button => button;
        public RectTransform VisualRoot => visualRoot;

        public void Bind(CardDefinition card, IEnumerable<KeywordDefinition> keywords, bool typeOnly = false)
        {
            if (card == null) return;
            if (informationMasked) SetIllustration(illustrationBeforeMask);
            informationMasked = typeOnly;
            if (illustrationMaskLabel != null) illustrationMaskLabel.gameObject.SetActive(typeOnly);
            displayedReservationSlot = int.MinValue;
            SetReservationSlot(-1);
            // A pooled view may still be grey from its previous owner.
            foreach (var entry in normalTextColors)
                if (entry.Key != null) entry.Key.color = entry.Value;
            if (normalTextColors.Count > 0) descriptionLabel.color = normalDescriptionColor;
            descriptionLabel.overrideColorTags = false;
            normalTextColors.Clear();
            displayedCooldown = int.MinValue;
            displayedInteractable = null;
            SetIllustration(illustration.sprite);
            nameLabel.text = card.name;
            energyLabel.text = card.energyCost.ToString();
            string body = card.ResolvedDescription(FormatEffectValue, turns => $"[{CooldownLinkId}]");
            HashSet<string> ids = new HashSet<string>(card.keywordIds ?? Array.Empty<string>());
            var linkedKeywords = new Dictionary<string, KeywordDefinition>();
            foreach (var keyword in keywords ?? Enumerable.Empty<KeywordDefinition>())
                if (ids.Contains(keyword.id)) linkedKeywords[keyword.name] = keyword;
            if (card.EffectiveCooldownTurns > 0)
                linkedKeywords[CooldownLinkId] = new KeywordDefinition
                {
                    id = CooldownLinkId,
                    name = string.Empty,
                    description = "<color=#FFFFFF>이 카드를 사용하면 다음 " +
                        $"<color=#999999>{card.EffectiveCooldownTurns}</color>턴 동안 재사용 할 수 없다.</color>"
                };
            descriptionLabel.text = KeywordToken.Replace(body, match =>
                linkedKeywords.TryGetValue(match.Groups[1].Value, out var keyword)
                    ? keyword.id == CooldownLinkId
                        ? $"<link=\"{keyword.id}\"><color=#999999>[{card.EffectiveCooldownTurns}턴]</color></link>"
                        : $"<link=\"{keyword.id}\"><color=#FFBA47>[{keyword.name}]</color></link>"
                    : match.Value);
            if (keywordHover == null) keywordHover = descriptionLabel.GetComponent<KeywordHoverLink>();
            keywordHover.Initialize(this, descriptionLabel, linkedKeywords.Values);
            keywordHover.enabled = !typeOnly;
            typeBadge.sprite = BadgeFor(card.actionType);

            var effects = card.effects ?? Array.Empty<CardEffectDefinition>();
            bool selfOnly = effects.Length > 0 && effects.All(effect => effect.targetType == "자신");
            Vector2Int[] range = effects
                .Where(effect => effect.rangeOffsets != null)
                .SelectMany(effect => effect.rangeOffsets)
                .Distinct().ToArray();
            bool hasRange = !typeOnly && !selfOnly && range.Length > 0;
            rangeArea.gameObject.SetActive(hasRange);
            rangeGraphic.SetOffsets(range);
            descriptionArea.anchorMin = new Vector2(descriptionArea.anchorMin.x, hasRange ? 0.18f : 0.085f);
            if (typeOnly)
            {
                nameLabel.text = energyLabel.text = descriptionLabel.text = "?";
                illustrationBeforeMask = illustration.sprite;
                SetIllustration(null);
            }
        }

        private static string FormatEffectValue(CardEffectDefinition effect, int value)
        {
            string color = effect.effectType switch
            {
                "damage" => "#FF5A5A",
                "guard" => "#5AA9FF",
                "energyRestore" => "#FFD54F",
                "healthRestore" => "#66CC7A",
                _ => null
            };
            return color == null ? value.ToString() : $"<color={color}>{value}</color>";
        }

        public void SetKeywordHoverActions(Action<KeywordDefinition, Vector2> enter, Action exit, Action<Vector2> move = null)
        {
            onKeywordEnter = enter;
            onKeywordExit = exit;
            onKeywordMove = move;
        }

        internal void NotifyKeywordEnter(KeywordDefinition keyword, Vector2 position) => onKeywordEnter?.Invoke(keyword, position);
        internal void NotifyKeywordMove(Vector2 position) => onKeywordMove?.Invoke(position);
        internal void NotifyKeywordExit() => onKeywordExit?.Invoke();

        public void SetState(string state, int remainingCooldown, bool interactable)
        {
            if (informationMasked) remainingCooldown = 0;
            if (displayedCooldown == remainingCooldown && displayedInteractable == interactable) return;
            CacheTextColors();
            if (displayedInteractable != interactable) button.interactable = interactable;
            bool coolingDown = remainingCooldown > 0;
            if (displayedCooldown != remainingCooldown)
            {
                cooldownLabel.gameObject.SetActive(coolingDown);
                cooldownLabel.text = coolingDown ? remainingCooldown.ToString() : string.Empty;
            }
            if (displayedCooldown == int.MinValue || (displayedCooldown > 0) != coolingDown)
            {
                foreach (Graphic graphic in grayscaleGraphics)
                    if (graphic != null) graphic.material = coolingDown ? grayscaleMaterial : null;
                foreach (KeyValuePair<Graphic, Color> entry in normalTextColors)
                    if (entry.Key != null) entry.Key.color = coolingDown ? Grayscale(entry.Value) : entry.Value;
                descriptionLabel.color = coolingDown ? Grayscale(normalDescriptionColor) : normalDescriptionColor;
                descriptionLabel.overrideColorTags = coolingDown;
            }
            displayedCooldown = remainingCooldown;
            displayedInteractable = interactable;
        }

        public void SetFrontVisible(bool visible)
        {
            frontRoot.SetActive(visible);
            backRoot.SetActive(!visible);
        }

        public void SetReservationSlot(int slotIndex)
        {
            if (displayedReservationSlot == slotIndex) return;
            bool reserved = slotIndex >= 0 && slotIndex < ReservationNumbers.Length;
            if (reservationOutline != null && reservationOutline.enabled != reserved)
                reservationOutline.enabled = reserved;
            if (reservationSlotLabel != null)
            {
                reservationSlotLabel.text = reserved ? ReservationNumbers[slotIndex] : string.Empty;
                reservationSlotLabel.gameObject.SetActive(reserved);
            }
            displayedReservationSlot = slotIndex;
        }

        public void SetIllustration(Sprite sprite)
        {
            illustration.sprite = sprite;
            illustration.enabled = true;
            illustration.color = sprite != null ? Color.white : new Color(0.12f, 0.14f, 0.18f, 1f);
        }

        public void SetFont(Font font)
        {
            if (font == null) return;
            foreach (Text label in GetComponentsInChildren<Text>(true))
            {
                // Numeric badges keep the prefab font and its vertical metrics.
                if (label == cooldownLabel || label == reservationSlotLabel) continue;
                label.font = font;
            }
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

        private void CacheTextColors()
        {
            if (normalTextColors.Count > 0) return;
            normalDescriptionColor = descriptionLabel.color;
            foreach (Text label in GetComponentsInChildren<Text>(true))
                if (label != cooldownLabel) normalTextColors[label] = label.color;
            normalTextColors[nameLabel] = nameLabel.color;
            normalTextColors[energyLabel] = energyLabel.color;
        }

        private static Color Grayscale(Color color)
        {
            float value = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return new Color(value, value, value, color.a);
        }
    }
}
