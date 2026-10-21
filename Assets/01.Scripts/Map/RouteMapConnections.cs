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
        [SerializeField] private float lineWidth = 3f;

        public void SetConnections(List<Vector4> connections)
        {
            segments = connections;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = rectTransform.rect;
            foreach (var segment in segments)
            {
                var start = new Vector2(rect.xMin + segment.x * rect.width,
                    rect.yMin + segment.y * rect.height);
                var end = new Vector2(rect.xMin + segment.z * rect.width,
                    rect.yMin + segment.w * rect.height);
                var direction = (end - start).normalized;
                var normal = new Vector2(-direction.y, direction.x);
                var halfWidth = normal * (lineWidth * .5f);
                int index = vertices.currentVertCount;
                vertices.AddVert(start - halfWidth, color, Vector2.zero);
                vertices.AddVert(start + halfWidth, color, Vector2.zero);
                vertices.AddVert(end + halfWidth, color, Vector2.zero);
                vertices.AddVert(end - halfWidth, color, Vector2.zero);
                vertices.AddTriangle(index, index + 1, index + 2);
                vertices.AddTriangle(index, index + 2, index + 3);

                // An arrow between nodes makes the bottom-to-top direction clear.
                var arrow = Vector2.Lerp(start, end, .6f);
                index = vertices.currentVertCount;
                vertices.AddVert(arrow + direction * 8f, color, Vector2.zero);
                vertices.AddVert(arrow - direction * 5f + normal * 5f, color, Vector2.zero);
                vertices.AddVert(arrow - direction * 5f - normal * 5f, color, Vector2.zero);
                vertices.AddTriangle(index, index + 1, index + 2);
            }
        }
    }
}
