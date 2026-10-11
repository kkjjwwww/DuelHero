using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    [DisallowMultipleComponent]
    public sealed class DirectionArrowPreviewHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private ActionSlotDirectionUI owner;
        private int directionIndex;

        public void Configure(ActionSlotDirectionUI directionUI, int index)
        {
            owner = directionUI;
            directionIndex = index;
        }

        public void OnPointerEnter(PointerEventData eventData) => owner?.PreviewDirection(directionIndex);
        public void OnPointerExit(PointerEventData eventData) => owner?.ClearPreviewDirection(directionIndex);
        private void OnDisable() => owner?.ClearPreviewDirection(directionIndex);
    }
}
