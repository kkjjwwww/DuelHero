using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardReservationOutline : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float thickness = 1.5f;
        [SerializeField, Min(0f)] private float cornerCut = 4f;

        public float Thickness
        {
            get => thickness;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(thickness, value)) return;
                thickness = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect outer = GetPixelAdjustedRect();
            float width = Mathf.Clamp(thickness, 0f, Mathf.Min(outer.width, outer.height) * 0.5f);
            if (width <= 0f) return;
            float cut = Mathf.Clamp(cornerCut, 0f, Mathf.Min(outer.width, outer.height) * 0.5f);
            Rect inner = Rect.MinMaxRect(outer.xMin + width, outer.yMin + width, outer.xMax - width, outer.yMax - width);
            float innerCut = Mathf.Clamp(cut - width * (2f - Mathf.Sqrt(2f)), 0f, Mathf.Min(inner.width, inner.height) * 0.5f);
            for (int i = 0; i < 8; i++)
            {
                vh.AddVert(Corner(outer, cut, i), color, Vector2.zero);
                vh.AddVert(Corner(inner, innerCut, i), color, Vector2.zero);
            }
            for (int i = 0; i < 8; i++)
            {
                int current = i * 2;
                int next = ((i + 1) % 8) * 2;
                vh.AddTriangle(current, next, current + 1);
                vh.AddTriangle(next, next + 1, current + 1);
            }
        }

        private static Vector2 Corner(Rect rect, float cut, int index)
        {
            return index switch
            {
                0 => new Vector2(rect.xMin + cut, rect.yMin),
                1 => new Vector2(rect.xMin, rect.yMin + cut),
                2 => new Vector2(rect.xMin, rect.yMax - cut),
                3 => new Vector2(rect.xMin + cut, rect.yMax),
                4 => new Vector2(rect.xMax - cut, rect.yMax),
                5 => new Vector2(rect.xMax, rect.yMax - cut),
                6 => new Vector2(rect.xMax, rect.yMin + cut),
                _ => new Vector2(rect.xMax - cut, rect.yMin)
            };
        }
    }
}
