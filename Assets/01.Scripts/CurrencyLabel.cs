using DuelHero.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.UI
{
    internal sealed class CurrencyLabel
    {
        public RectTransform Position { get; }
        private readonly Text label;
        private readonly Image icon;
        private TextMeshProUGUI balanceText;

        public static CurrencyLabel CreateGoldBalance(TextMeshProUGUI label)
        {
            var balance = new CurrencyLabel(label, Resources.Load<Sprite>("UI/Currency/money-pouch"), 36f)
            {
                balanceText = label
            };
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            balance.ConfigureGoldRow();
            return balance;
        }

        public static CurrencyLabel CreateGoldBalance(Text label, TMP_FontAsset font)
        {
            var balance = new CurrencyLabel(label, Resources.Load<Sprite>("UI/Currency/money-pouch"), 36f);
            balance.ConfigureGoldRow();
            label.raycastTarget = false;
            if (font != null)
            {
                var textObject = new GameObject("Gold Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(balance.Position, false);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                text.font = font;
                text.fontSize = label.fontSize;
                text.color = label.color;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
                balance.balanceText = text;
                label.enabled = false;
            }
            return balance;
        }

        private void ConfigureGoldRow()
        {
            var row = Position.gameObject;
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            row.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            row.AddComponent<Image>().color = Color.clear;
            row.AddComponent<DelayedTooltipTrigger>().Configure("보유골드", 0f);
        }

        public CurrencyLabel(Text label, Sprite sprite, float iconSize) : this((Graphic)label, sprite, iconSize)
        {
            this.label = label;
            label.alignment = TextAnchor.MiddleCenter;
        }

        private CurrencyLabel(Graphic label, Sprite sprite, float iconSize)
        {
            var textRect = label.rectTransform;
            var row = new GameObject(label.name + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Position = (RectTransform)row.transform;
            Position.SetParent(textRect.parent, false);
            Position.SetSiblingIndex(textRect.GetSiblingIndex());
            Position.anchorMin = textRect.anchorMin;
            Position.anchorMax = textRect.anchorMax;
            Position.pivot = textRect.pivot;
            Position.sizeDelta = textRect.sizeDelta;
            Position.anchoredPosition3D = textRect.anchoredPosition3D;
            Position.localRotation = textRect.localRotation;
            Position.localScale = textRect.localScale;

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var iconObject = new GameObject("CurrencyIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(Position, false);
            icon = iconObject.GetComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconLayout = iconObject.GetComponent<LayoutElement>();
            iconLayout.preferredWidth = iconLayout.preferredHeight = iconSize;
            textRect.SetParent(Position, false);
            textRect.localScale = Vector3.one;
            textRect.localRotation = Quaternion.identity;
        }

        public void SetAmount(int amount) => Set(amount.ToString(), true);
        public void SetStatus(string status) => Set(status, false);

        private void Set(string value, bool amount)
        {
            if (balanceText != null)
            {
                balanceText.text = value;
                balanceText.fontStyle = amount ? FontStyles.Bold : FontStyles.Normal;
            }
            else
            {
                label.text = value;
                label.fontStyle = amount ? FontStyle.Bold : FontStyle.Normal;
            }
            icon.gameObject.SetActive(amount && icon.sprite != null);
        }
    }
}
