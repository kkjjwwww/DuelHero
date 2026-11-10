using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    [DisallowMultipleComponent]
    public sealed class DelayedTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField, TextArea] private string text;
        [SerializeField, Min(0f)] private float delay = 2f;
        private Selectable selectable;
        private bool allowDisabled;
        private KeywordTooltip tooltip;
        private static DelayedTooltipTrigger owner;
        private bool hovered;
        private float enteredAt;
        private Vector2 pointerPosition;
        internal bool IsHovered => hovered;

        internal void Configure(string message, float hoverDelay, bool showWhenDisabled = false)
        {
            text = message;
            delay = hoverDelay;
            allowDisabled = showWhenDisabled;
        }

        private void Awake() => selectable = GetComponent<Selectable>();
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!allowDisabled && selectable != null && !selectable.IsInteractable()) return;
            if (owner != null) owner.Cancel();
            owner = this;
            hovered = true;
            enteredAt = Time.unscaledTime;
            pointerPosition = eventData.position;
        }
        public void OnPointerMove(PointerEventData eventData)
        {
            pointerPosition = eventData.position;
            if (owner == this && tooltip != null && tooltip.gameObject.activeSelf) tooltip.Move(pointerPosition);
        }
        public void OnPointerExit(PointerEventData eventData) => Cancel();
        private void Update()
        {
            if (!hovered) return;
            if (!allowDisabled && selectable != null && !selectable.IsInteractable()) { Cancel(); return; }
            if (Time.unscaledTime - enteredAt < delay || string.IsNullOrEmpty(text)) return;
            if (tooltip != null && tooltip.gameObject.activeSelf) return;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var existing = canvas.transform.Find("DelayedTooltip");
            tooltip = existing != null ? existing.GetComponent<KeywordTooltip>() : KeywordTooltip.Create(canvas, compact: true);
            tooltip.name = "DelayedTooltip";
            tooltip.Show(text, pointerPosition);
        }
        public void Cancel()
        {
            hovered = false;
            if (owner != this) return;
            if (tooltip != null) tooltip.Hide();
            owner = null;
        }
        private void OnDisable() => Cancel();
    }
}
