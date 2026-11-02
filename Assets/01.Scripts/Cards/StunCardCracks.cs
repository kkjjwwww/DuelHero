using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    // UI geometry keeps the broken surface sharp at every slot scale.
    public sealed class StunCardCracks : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            Vector2 P(float x, float y) => new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
            void Line(Vector2 a, Vector2 b, float width)
            {
                Vector2 n = (b - a).normalized;
                n = new Vector2(-n.y, n.x) * width;
                int i = vh.currentVertCount;
                vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
                vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
            }
            var points = new[] { P(.02f,.95f), P(.2f,.79f), P(.15f,.68f), P(.36f,.58f), P(.29f,.46f) };
            for (int i = 1; i < points.Length; i++) Line(points[i-1], points[i], .8f);
            Line(points[2], P(.04f,.57f), .45f); Line(points[3], P(.61f,.64f), .45f);
            Line(P(.99f,.44f), P(.93f,.36f), .8f); Line(P(.93f,.36f), P(.96f,.22f), .8f);
            Line(P(.96f,.22f), P(.9f,.03f), .8f); Line(P(.96f,.22f), P(.99f,.19f), .45f);
        }
    }
}
