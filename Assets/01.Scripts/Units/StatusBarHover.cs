using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Units
{
    internal sealed class StatusBarHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Text label;
        private Image hitArea;

        public static StatusBarHover Bind(Image fill, Text label)
        {
            if (fill == null || label == null) return null;
            // The segment container keeps its full width even when the fill is empty.
            var container = fill.transform.parent.gameObject;
            if (!container.TryGetComponent<StatusBarHover>(out var hover))
            {
                hover = container.AddComponent<StatusBarHover>();
                hover.hitArea = container.AddComponent<Image>();
                hover.hitArea.color = Color.clear;
            }
            hover.label = label;
            label.raycastTarget = false;
            label.enabled = false;
            hover.enabled = true;
            hover.hitArea.raycastTarget = true;
            return hover;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActiveAndEnabled) label.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData) => label.enabled = false;

        private void OnDisable()
        {
            if (label != null) label.enabled = false;
            if (hitArea != null) hitArea.raycastTarget = false;
        }
    }
}
