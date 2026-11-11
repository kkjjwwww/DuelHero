using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Map
{
    // Endpoints are normalized to the graph rect so links follow resized nodes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RouteMapConnections : MaskableGraphic
    {
        [SerializeField] private List<Vector4> segments = new List<Vector4>();
        [SerializeField] private Texture2D dashTexture;
        [SerializeField] private Rect dashUv = new Rect(.284f, .4f, .432f, .2f);
        [SerializeField, Min(1f)] private float lineWidth = 6f;
        [SerializeField, Min(1f)] private float dashLength = 26f;
        [SerializeField, Min(0f)] private float dashGap = 12f;
        private IEnumerable<Vector4> preview;

        public override Texture mainTexture => dashTexture != null ? dashTexture : Texture2D.whiteTexture;

        public void SetPreview(IEnumerable<Vector4> connections)
        {
            preview = connections;
            SetVerticesDirty();
        }

        public void SetConnections(List<Vector4> connections)
        {
            segments = connections;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (preview != null)
            {
                var previewColor = color;
                previewColor.a *= .5f;
                DrawConnections(vertices, preview, previewColor);
            }
            DrawConnections(vertices, segments, color);
        }

        private void DrawConnections(VertexHelper vertices, IEnumerable<Vector4> connections, Color tint)
        {
            var rect = rectTransform.rect;
            foreach (var segment in connections)
            {
                var start = new Vector2(rect.xMin + segment.x * rect.width,
                    rect.yMin + segment.y * rect.height);
                var end = new Vector2(rect.xMin + segment.z * rect.width,
                    rect.yMin + segment.w * rect.height);
                var offset = end - start;
                float distance = offset.magnitude;
                if (distance <= 0f) continue;

                var direction = offset / distance;
                // Keep the dashes clear of both node icons.
                float startInset = Mathf.Min(36f, distance * .25f);
                float endInset = Mathf.Min(40f, distance * .4f);
                start += direction * startInset;
                distance -= startInset + endInset;
                var normal = new Vector2(-direction.y, direction.x);
                float bendDirection = Mathf.Repeat(segment.x * 13f + segment.z * 17f + segment.y * 7f, 1f);
                float bend = Mathf.Lerp(-1f, 1f, bendDirection) * Mathf.Min(38f, distance * .15f);
                float length = Mathf.Min(dashLength, distance);
                float spacing = length + dashGap;
                int count = Mathf.FloorToInt((distance + dashGap) / spacing);
                float margin = (distance - ((count - 1) * spacing + length)) * .5f;
                for (int dash = 0; dash < count; dash++)
                {
                    float dashOffset = margin + dash * spacing;
                    var dashStart = PointOnRoute(dashOffset);
                    var dashEnd = PointOnRoute(dashOffset + length);
                    var tangent = (dashEnd - dashStart).normalized;
                    var halfWidth = new Vector2(-tangent.y, tangent.x) * (lineWidth * .5f);
                    int index = vertices.currentVertCount;
                    vertices.AddVert(dashStart - halfWidth, tint, new Vector2(dashUv.xMin, dashUv.yMin));
                    vertices.AddVert(dashStart + halfWidth, tint, new Vector2(dashUv.xMin, dashUv.yMax));
                    vertices.AddVert(dashEnd + halfWidth, tint, new Vector2(dashUv.xMax, dashUv.yMax));
                    vertices.AddVert(dashEnd - halfWidth, tint, new Vector2(dashUv.xMax, dashUv.yMin));
                    vertices.AddTriangle(index, index + 1, index + 2);
                    vertices.AddTriangle(index, index + 2, index + 3);
                }

                Vector2 PointOnRoute(float along)
                {
                    float t = along / distance;
                    return start + direction * along + normal * (4f * t * (1f - t) * bend);
                }
            }
        }
    }
}
