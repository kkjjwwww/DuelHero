using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Shop
{
    public sealed class ShopUnavailableCardClick : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public Func<bool> IsUnavailable { get; set; }
        public Action Clicked { get; set; }
        private bool unavailableOnPress;

        public void OnPointerDown(PointerEventData eventData)
        {
            unavailableOnPress = eventData.button == PointerEventData.InputButton.Left && IsUnavailable?.Invoke() == true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && unavailableOnPress) Clicked?.Invoke();
            unavailableOnPress = false;
        }

        private void OnDisable() => unavailableOnPress = false;
    }
}
