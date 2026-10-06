using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    // Handle only hover; ScrollRect remains the scroll and drag event handler.
    public sealed class CardHoverInfo : MonoBehaviour, IPointerEnterHandler
    {
        public Action ShowInfo { get; set; }
        public void OnPointerEnter(PointerEventData eventData) => ShowInfo?.Invoke();
    }
}
