using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class KeywordTooltip : MonoBehaviour
    {
        private RectTransform panel;
        private Text label;
        private Canvas canvas;
        public static KeywordTooltip Create(Canvas canvas, Font font)
        {
            var go = new GameObject("KeywordTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(KeywordTooltip));
            go.transform.SetParent(canvas.transform, false);
            var result = go.GetComponent<KeywordTooltip>();
            result.canvas = canvas;
            result.panel = (RectTransform)go.transform;
            result.panel.anchorMin = result.panel.anchorMax = Vector2.zero;
            result.panel.pivot = Vector2.zero;
            var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var background = go.GetComponent<Image>(); background.color = new Color(0.055f, 0.065f, 0.08f, 0.98f); background.raycastTarget = false;
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            result.label = textGo.GetComponent<Text>();
            result.label.font = font; result.label.fontSize = 16; result.label.supportRichText = true;
            result.label.color = new Color(0.96f, 0.91f, 0.8f); result.label.raycastTarget = false;
            var rect = result.label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12, 10); rect.offsetMax = new Vector2(-12, -10);
            result.Hide();
            return result;
        }
        public void Show(KeywordDefinition keyword, Vector2 screenPosition)
        {
            label.text = string.IsNullOrEmpty(keyword.name) ? keyword.description
                : $"<color=#FFBA47>[{keyword.name}]</color>\n{keyword.description}";
            panel.sizeDelta = new Vector2(280f, 28f + label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, label.GetGenerationSettings(new Vector2(256f, 0f))) / label.pixelsPerUnit);
            gameObject.SetActive(true); transform.SetAsLastSibling(); Move(screenPosition);
        }
        public void Move(Vector2 screenPosition)
        {
            var parent = (RectTransform)canvas.transform;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out var local);
            Vector2 position = local - parent.rect.min + new Vector2(16, 16);
            if (position.x + panel.rect.width > parent.rect.width - 8) position.x -= panel.rect.width + 32;
            if (position.y + panel.rect.height > parent.rect.height - 8) position.y -= panel.rect.height + 32;
            position.x = Mathf.Clamp(position.x, 8, Mathf.Max(8, parent.rect.width - panel.rect.width - 8));
            position.y = Mathf.Clamp(position.y, 8, Mathf.Max(8, parent.rect.height - panel.rect.height - 8));
            panel.anchoredPosition = position;
        }
        public void Hide() => gameObject.SetActive(false);
    }
}
