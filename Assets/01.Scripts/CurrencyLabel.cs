using DuelHero.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.UI
{
    internal sealed class CurrencyLabel
    {
        public RectTransform Position { get; }
        private readonly TextMeshProUGUI label;
        private readonly Image icon;

        public static CurrencyLabel CreateGoldBalance(TextMeshProUGUI label)
        {
            var balance = new CurrencyLabel(label, Resources.Load<Sprite>("UI/Currency/money-pouch"), 36f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            balance.ConfigureGoldRow();
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

        public CurrencyLabel(TextMeshProUGUI label, Sprite sprite, float iconSize)
        {
            this.label = label;
            label.alignment = TextAlignmentOptions.Center;
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
            label.text = value;
            label.fontStyle = amount ? FontStyles.Bold : FontStyles.Normal;
            icon.gameObject.SetActive(amount && icon.sprite != null);
        }
    }
}
