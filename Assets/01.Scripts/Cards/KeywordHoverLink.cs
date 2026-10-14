using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

namespace DuelHero.Cards
{
    public sealed class KeywordHoverLink : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private CardView owner;
        private TMP_Text text;
        private Canvas canvas;
        private readonly Dictionary<string, KeywordDefinition> keywords = new();
        private bool pointerInside;
        private Vector2 pointerPosition;
        private Vector2 lastTooltipPosition;
        private int currentLink = -1;

        public void Initialize(CardView cardView, TMP_Text label, IEnumerable<KeywordDefinition> definitions)
        {
            ClearLink();
            owner = cardView;
            text = label;
            canvas = label.GetComponentInParent<Canvas>();
            keywords.Clear();
            foreach (var definition in definitions) keywords[definition.id] = definition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (eventData != null) pointerPosition = eventData.position;
            UpdateLink();
        }
        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventData != null) pointerPosition = eventData.position;
            UpdateLink();
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            ClearLink();
        }
        // Recheck a stationary cursor while cards move, rotate or enlarge.
        private void LateUpdate() { if (pointerInside) UpdateLink(); }
        private void UpdateLink()
        {
            if (!pointerInside || text == null || !text.gameObject.activeInHierarchy) { ClearLink(); return; }
            if (text.havePropertiesChanged) text.ForceMeshUpdate();
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            int index = TMP_TextUtilities.FindIntersectingLink(text, pointerPosition, camera);
            if (index < 0) { ClearLink(); return; }
            if (index != currentLink)
            {
                ClearLink();
                if (!keywords.TryGetValue(text.textInfo.linkInfo[index].GetLinkID(), out var keyword)) return;
                currentLink = index;
                lastTooltipPosition = pointerPosition;
                owner?.NotifyKeywordEnter(keyword, pointerPosition);
            }
            else if (lastTooltipPosition != pointerPosition)
            {
                lastTooltipPosition = pointerPosition;
                owner?.NotifyKeywordMove(pointerPosition);
            }
        }
        private void ClearLink()
        {
            if (currentLink < 0) return;
            currentLink = -1;
            owner?.NotifyKeywordExit();
        }
        private void OnDisable() { pointerInside = false; ClearLink(); }
    }
}
