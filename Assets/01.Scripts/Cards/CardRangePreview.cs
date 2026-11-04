using UnityEngine;

namespace DuelHero.Cards
{
    // One reusable range panel for both the hand and inspection cards.
    public sealed class CardRangePreview : MonoBehaviour
    {
        [SerializeField] private CardRangeGraphic graphic;
        [SerializeField] private float cardGap = 8f;
        [SerializeField] private float verticalOffset;
        [SerializeField, Min(0f)] private float screenMargin = 8f;
        private Canvas canvas;
        private CardView owner;
        private readonly Vector3[] corners = new Vector3[4];

        public static CardRangePreview GetOrCreate(Canvas canvas)
        {
            canvas = canvas.rootCanvas;
            var existing = canvas.GetComponentInChildren<CardRangePreview>(true);
            if (existing != null) return existing;
            var panel = Instantiate(Resources.Load<CardRangePreview>("UI/Cards/CardRangePreview"), canvas.transform, false);
            panel.canvas = canvas;
            panel.gameObject.SetActive(false);
            return panel;
        }

        public void Show(CardView card)
        {
            owner = card;
            graphic.SetOffsets(card.RangeOffsets);
            gameObject.SetActive(card.RangeOffsets.Length > 0);
            transform.SetAsLastSibling();
            Position();
        }

        public void Hide(CardView card)
        {
            if (owner != card) return;
            owner = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (owner == null || !owner.isActiveAndEnabled)
            {
                gameObject.SetActive(false);
                return;
            }
            Position();
        }

        private void Position()
        {
            if (owner == null) return;
            var root = (RectTransform)canvas.transform;
            var panel = (RectTransform)transform;
            owner.VisualRoot.GetWorldCorners(corners);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (var corner in corners)
            {
                Vector3 point = root.InverseTransformPoint(corner);
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            Vector2 half = panel.rect.size * .5f;
            Rect bounds = root.rect;
            float x = right + cardGap + half.x;
            if (x + half.x > bounds.xMax - screenMargin) x = left - cardGap - half.x;
            panel.anchoredPosition = new Vector2(
                Mathf.Clamp(x, bounds.xMin + half.x + screenMargin, bounds.xMax - half.x - screenMargin),
                Mathf.Clamp((bottom + top) * .5f + verticalOffset, bounds.yMin + half.y + screenMargin, bounds.yMax - half.y - screenMargin));
        }
    }
}
