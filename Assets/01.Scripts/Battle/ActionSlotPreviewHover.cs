using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Battle
{
    public sealed class ActionSlotPreviewHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BattlePreviewPresenter preview;
        [SerializeField, Range(0, 2)] private int slot;
        public void OnPointerEnter(PointerEventData eventData) => preview?.FocusSlot(slot);
        public void OnPointerExit(PointerEventData eventData) => preview?.ClearSlot(slot);
        private void OnDisable() => preview?.ClearSlot(slot);
    }
}
