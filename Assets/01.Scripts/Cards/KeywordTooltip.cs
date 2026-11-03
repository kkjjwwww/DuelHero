using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class KeywordTooltip : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text label;
        [SerializeField] private bool fitWidth;
        [SerializeField, Min(1f)] private float minWidth = 48f;
        [SerializeField, Min(1f)] private float maxWidth = 280f;
        private Canvas canvas;
        public static KeywordTooltip Create(Canvas canvas, bool compact = false)
        {
            var prefab = Resources.Load<KeywordTooltip>("UI/Tooltips/" + (compact ? "DelayedTooltip" : "KeywordTooltip"));
            var result = Instantiate(prefab, canvas.rootCanvas.transform, false);
            result.name = prefab.name;
            result.canvas = canvas.rootCanvas;
            result.Hide();
            return result;
        }
        public void Show(KeywordDefinition keyword, Vector2 screenPosition)
        {
            ShowText(string.IsNullOrEmpty(keyword.name) ? keyword.description
                : $"<color={keyword.TextColor}>[{keyword.name}]</color>\n{keyword.description}", screenPosition);
        }
        public void Show(string text, Vector2 screenPosition) => ShowText(text, screenPosition);
        private void ShowText(string text, Vector2 screenPosition)
        {
            label.text = text;
            Vector2 padding = label.rectTransform.offsetMin - label.rectTransform.offsetMax;
            float width = fitWidth ? Mathf.Clamp(label.preferredWidth + padding.x, minWidth, maxWidth) : maxWidth;
            panel.sizeDelta = new Vector2(width, padding.y + label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, label.GetGenerationSettings(new Vector2(width - padding.x, 0f))) / label.pixelsPerUnit);
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
