using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardRaycastArea : MonoBehaviour, ICanvasRaycastFilter
    {
        [Tooltip("Exclude transparent frame margins in unscaled card units: left, bottom, right, top.")]
        [SerializeField] private Vector4 bodyInsets = new Vector4(10f, 8f, 10f, 8f);
        [SerializeField] private RectTransform body;
        [SerializeField] private Image energyBadge;
        [SerializeField] private Image typeBadge;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!isActiveAndEnabled) return true;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPoint, eventCamera, out var local)
                && ContainsLocalPoint(local);
        }

        // Also usable with an unanimated hand pose, without moving the actual card.
        public bool ContainsLocalPoint(Vector2 point)
        {
            if (!isActiveAndEnabled) return ((RectTransform)transform).rect.Contains(point);
            Vector3 worldPoint = transform.TransformPoint(point);
            var rect = body != null ? body : (RectTransform)transform;
            Vector2 local = rect.InverseTransformPoint(worldPoint);
            Rect bounds = rect.rect;
            bounds.xMin += Mathf.Max(0, bodyInsets.x);
            bounds.yMin += Mathf.Max(0, bodyInsets.y);
            bounds.xMax -= Mathf.Max(0, bodyInsets.z);
            bounds.yMax -= Mathf.Max(0, bodyInsets.w);
            return bounds.Contains(local) || ContainsBadge(energyBadge, worldPoint) || ContainsBadge(typeBadge, worldPoint);
        }

        private static bool ContainsBadge(Image badge, Vector3 worldPoint)
        {
            if (badge == null || !badge.isActiveAndEnabled) return false;
            var rect = badge.rectTransform;
            Vector2 local = rect.InverseTransformPoint(worldPoint);
            Rect bounds = rect.rect;
            // The square badge sprites are drawn with Preserve Aspect inside their rectangles.
            float radius = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            return radius > 0f && (local - bounds.center).sqrMagnitude <= radius * radius;
        }
    }
}
