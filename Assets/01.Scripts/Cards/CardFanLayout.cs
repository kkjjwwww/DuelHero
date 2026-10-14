using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelHero.Cards
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    public sealed class CardFanLayout : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float spacingRatio = 0.78f;
        [SerializeField, Range(0f, 35f)] private float maximumAngle = 18f;
        [SerializeField, Min(1f)] private float hoverScale = 1.55f;
        [SerializeField] private float hoverLift = 70f;
        [SerializeField, Range(0f, 0.5f)] private float hiddenDescriptionRatio = 0.30f;
        [SerializeField, Min(0f)] private float edgeDrop = 32f;
        private readonly List<CardView> cards = new();
        private readonly Dictionary<CardView, Vector2> positions = new();
        private readonly Dictionary<CardView, float> angles = new();
        private readonly Dictionary<CardView, CardRaycastArea> hitAreas = new();
        private readonly Vector3[] canvasCorners = new Vector3[4];
        private Canvas canvas;
        private bool hoverEnabled;
        private CardView focused;
        private Vector2 lastSize;
        public CardView Focused => focused;
        public event System.Action<CardView> FocusChanged;

        public void SetCards(IEnumerable<CardView> views)
        {
            ResetFocus();
            cards.Clear(); cards.AddRange(views);
            hitAreas.Clear();
            foreach (var card in cards) if (card != null) hitAreas[card] = card.GetComponent<CardRaycastArea>();
            Arrange();
        }

        public void SetHoverEnabled(bool enabled)
        {
            hoverEnabled = enabled;
            if (!enabled && focused != null) ResetFocus();
        }
        private void Update()
        {
            if (!Application.isPlaying || !hoverEnabled || Mouse.current == null) return;
            UpdateHover(Mouse.current.position.ReadValue());
        }
        private void UpdateHover(Vector2 position)
        {
            if (!hoverEnabled) return;
            if (canvas == null) canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var area = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, position, camera, out var local))
            { if (focused != null) Release(focused); return; }
            CardView candidate = null;
            // Keep the enlarged card until the pointer leaves its final visible shape.
            if (focused != null && ContainsAtPose(focused, local, true)) candidate = focused;
            else
                for (int i = cards.Count - 1; i >= 0; i--)
                    if (ContainsAtPose(cards[i], local, false)) { candidate = cards[i]; break; }
            if (candidate != null) Focus(candidate);
            else if (focused != null) Release(focused);
        }
        private bool ContainsAtPose(CardView card, Vector2 point, bool hover)
        {
            if (card == null || !card.gameObject.activeInHierarchy || !positions.ContainsKey(card)) return false;
            GetPose(card, hover, out var position, out var angle, out var scale);
            var rect = (RectTransform)card.transform;
            var parent = (RectTransform)transform;
            Vector2 anchor = parent.rect.min + Vector2.Scale(parent.rect.size, rect.anchorMin);
            Vector2 local = Quaternion.Euler(0, 0, -angle) * ((Vector3)(point - anchor - position) / scale);
            return hitAreas.TryGetValue(card, out var hit) && hit != null
                ? hit.ContainsLocalPoint(local) : rect.rect.Contains(local);
        }

        private void OnRectTransformDimensionsChange() { if (cards.Count > 0) Arrange(); }
        private void LateUpdate()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size != lastSize) Arrange();
        }

        public void Arrange()
        {
            var area = (RectTransform)transform;
            lastSize = area.rect.size;
            positions.Clear(); angles.Clear();
            if (cards.Count == 0) return;
            float width = ((RectTransform)cards[0].transform).rect.width;
            float height = ((RectTransform)cards[0].transform).rect.height;
            float usable = Mathf.Max(width, area.rect.width - 24f);
            float spacing = cards.Count < 2 ? 0f : Mathf.Min(width * spacingRatio, (usable - width - 24f) / (cards.Count - 1));
            spacing = Mathf.Max(0f, spacing);
            float angle = Mathf.Min(maximumAngle, (cards.Count - 1) * 5f) * Mathf.Clamp01(spacing / (width * spacingRatio));
            Rect screen = CanvasBounds();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                var rect = (RectTransform)card.transform;
                float t = cards.Count == 1 ? 0f : (2f * i / (cards.Count - 1) - 1f);
                angles[card] = -t * angle;
                // Keep the centre high and let both ends fall below the screen edge.
                float hiddenHeight = height * 0.45f * hiddenDescriptionRatio;
                positions[card] = new Vector2((i - (cards.Count - 1) * 0.5f) * spacing,
                    screen.yMin + height * 0.5f - hiddenHeight - edgeDrop * t * t);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                Apply(card, card == focused, true);
            }
            RestoreOrder();
        }

        public void Focus(CardView card)
        {
            if (focused == card || !positions.ContainsKey(card)) return;
            if (focused != null) Apply(focused, false, false);
            focused = card;
            RestoreOrder();
            Apply(card, true, false);
            FocusChanged?.Invoke(focused);
        }

        public void Release(CardView card)
        {
            if (focused != card) return;
            focused = null;
            if (card != null && positions.ContainsKey(card)) Apply(card, false, false);
            RestoreOrder();
            FocusChanged?.Invoke(null);
        }

        private void Apply(CardView card, bool hover, bool immediate)
        {
            var rect = (RectTransform)card.transform;
            rect.DOKill();
            GetPose(card, hover, out var position, out var angle, out var scale);
            Vector3 rotation = new Vector3(0, 0, angle);
            if (immediate || !Application.isPlaying)
            {
                rect.anchoredPosition = position; rect.localEulerAngles = rotation; rect.localScale = Vector3.one * scale;
            }
            else
            {
                float duration = hover ? 0.15f : 0.12f;
                rect.DOAnchorPos(position, duration).SetEase(Ease.OutQuad).SetUpdate(true);
                rect.DOLocalRotate(rotation, duration).SetEase(Ease.OutQuad).SetUpdate(true);
                rect.DOScale(scale, duration).SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }
        private void GetPose(CardView card, bool hover, out Vector2 position, out float angle, out float scale)
        {
            var rect = (RectTransform)card.transform;
            position = positions[card] + (hover ? Vector2.up * hoverLift : Vector2.zero);
            scale = hover ? hoverScale : 1f;
            if (hover)
            {
                float half = rect.rect.width * scale * 0.5f + 6f;
                float available = ((RectTransform)transform).rect.width * 0.5f;
                position.x = Mathf.Clamp(position.x, -Mathf.Max(0, available - half), Mathf.Max(0, available - half));
                Rect screen = CanvasBounds();
                float halfHeight = rect.rect.height * scale * 0.5f;
                position.y = Mathf.Clamp(position.y, screen.yMin + halfHeight + 8f, Mathf.Max(screen.yMin + halfHeight + 8f, screen.yMax - halfHeight - 8f));
            }
            angle = hover ? 0 : angles[card];
        }

        private void RestoreOrder()
        {
            if (!gameObject.activeInHierarchy) return;
            // Later cards overlap earlier cards, exposing every card's upper-left cost.
            foreach (var card in cards) if (card != null) card.transform.SetAsLastSibling();
            if (focused != null) focused.transform.SetAsLastSibling();
        }

        private Rect CanvasBounds()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return ((RectTransform)transform).rect;
            ((RectTransform)canvas.transform).GetWorldCorners(canvasCorners);
            Vector3 bottomLeft = transform.InverseTransformPoint(canvasCorners[0]);
            Vector3 topRight = transform.InverseTransformPoint(canvasCorners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        public void ResetFocus()
        {
            bool hadFocus = focused != null;
            focused = null;
            foreach (var card in cards)
                if (card != null && positions.ContainsKey(card)) Apply(card, false, true);
            RestoreOrder();
            if (hadFocus) FocusChanged?.Invoke(null);
        }
        private void OnDisable() => ResetFocus();
    }
}
