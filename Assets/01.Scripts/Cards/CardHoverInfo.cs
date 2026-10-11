using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    // Handle only hover; ScrollRect remains the scroll and drag event handler.
    public sealed class CardHoverInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action ShowInfo { get; set; }
        public Action HideInfo { get; set; }
        public void OnPointerEnter(PointerEventData eventData) => ShowInfo?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => HideInfo?.Invoke();
        private void OnDisable() => HideInfo?.Invoke();
    }
}
