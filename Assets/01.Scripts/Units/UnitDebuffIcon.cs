using DuelHero.Cards;
using DuelHero.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Units
{
    internal sealed class UnitDebuffIcon : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private const float Size = 28f;
        private const float Spacing = 4f;
        private Text countLabel;
        private KeywordDefinition keyword;
        private KeywordTooltip tooltip;
        private bool fromRight;

        internal static UnitDebuffIcon Create(RectTransform energyBar, Sprite sprite, KeywordDefinition keyword, bool fromRight)
        {
            var root = new GameObject(sprite.name, typeof(RectTransform), typeof(Image), typeof(UnitDebuffIcon));
            var rect = (RectTransform)root.transform;
            rect.SetParent(energyBar, false);
            rect.anchorMin = rect.anchorMax = new Vector2(fromRight ? 1f : 0f, 0f);
            rect.pivot = new Vector2(fromRight ? 1f : 0f, 1f);
            rect.sizeDelta = new Vector2(Size, Size);
            root.GetComponent<Image>().sprite = sprite;

            var label = new GameObject("Stacks", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            var labelRect = label.rectTransform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = new Vector2(0f, -10f);
            label.font = GameFonts.UI;
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = 13;
            label.alignment = TextAnchor.LowerRight;
            label.color = new Color(1f, .97f, .85f);
            label.raycastTarget = false;
            label.GetComponent<Outline>().effectColor = new Color(.07f, .06f, .05f);

            var icon = root.GetComponent<UnitDebuffIcon>();
            icon.countLabel = label;
            icon.keyword = keyword;
            icon.fromRight = fromRight;
            root.SetActive(false);
            return icon;
        }

        internal void SetCount(int count, int index)
        {
            if (count > 0)
            {
                float offset = index * (Size + Spacing);
                ((RectTransform)transform).anchoredPosition = new Vector2(fromRight ? -offset : offset, -Spacing);
                countLabel.text = count.ToString();
            }
            if (gameObject.activeSelf != (count > 0))
                gameObject.SetActive(count > 0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (keyword == null) return;
            if (tooltip == null) tooltip = KeywordTooltip.Create(GetComponentInParent<Canvas>().rootCanvas);
            tooltip.Show(keyword, eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (tooltip != null && tooltip.gameObject.activeSelf) tooltip.Move(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData) => HideTooltip();
        private void OnDisable() => HideTooltip();

        private void HideTooltip()
        {
            if (tooltip != null) tooltip.Hide();
        }

        private void OnDestroy()
        {
            if (tooltip != null) Destroy(tooltip.gameObject);
        }
    }
}
