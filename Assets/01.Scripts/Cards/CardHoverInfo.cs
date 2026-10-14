using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    // Shared hover callbacks for player and enemy card views.
    public sealed class CardHoverInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action ShowInfo { get; set; }
        public Action HideInfo { get; set; }
        public void OnPointerEnter(PointerEventData eventData) => ShowInfo?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => HideInfo?.Invoke();
        private void OnDisable() => HideInfo?.Invoke();
    }
}
