using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    public sealed class KeywordHoverLink : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CardView owner;
        private KeywordDefinition keyword;

        public void Initialize(CardView cardView, KeywordDefinition definition)
        {
            owner = cardView;
            keyword = definition;
        }

        public void OnPointerEnter(PointerEventData eventData) => owner?.NotifyKeywordEnter(keyword);
        public void OnPointerExit(PointerEventData eventData) => owner?.NotifyKeywordExit();
    }
}
