using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Battle
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class BattleTutorialMask : Graphic
    {
        private readonly Vector2[] outline = new Vector2[28];
        private Vector2 center;
        private bool hasHole;

        internal void SetHole(Vector2[] corners, float radius)
        {
            hasHole = corners != null;
            if (hasHole)
            {
                center = (corners[0] + corners[1] + corners[2] + corners[3]) * .25f;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 corner = corners[i];
                    Vector2 before = Vector2.MoveTowards(corner, corners[(i + 3) % 4], radius);
                    Vector2 after = Vector2.MoveTowards(corner, corners[(i + 1) % 4], radius);
                    for (int j = 0; j < 7; j++)
                    {
                        float t = j / 6f;
                        outline[i * 7 + j] = Vector2.Lerp(Vector2.Lerp(before, corner, t),
                            Vector2.Lerp(corner, after, t), t);
                    }
                }
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (!hasHole)
            {
                mesh.AddVert(rect.min, color, Vector2.zero);
                mesh.AddVert(new Vector2(rect.xMin, rect.yMax), color, Vector2.zero);
                mesh.AddVert(rect.max, color, Vector2.zero);
                mesh.AddVert(new Vector2(rect.xMax, rect.yMin), color, Vector2.zero);
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(0, 2, 3);
                return;
            }

            float nearestEdge = float.PositiveInfinity;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 edge = outline[(i + 1) % outline.Length] - outline[i];
                if (edge.sqrMagnitude < .0001f) continue;
                Vector2 offset = center - outline[i];
                nearestEdge = Mathf.Min(nearestEdge, Mathf.Abs(edge.x * offset.y - edge.y * offset.x) / edge.magnitude);
            }
            // Enlarge the same outline about its center until it encloses the viewport.
            float scale = ((rect.center - center).magnitude + rect.size.magnitude) / Mathf.Max(.01f, nearestEdge) + 1f;
            for (int i = 0; i < outline.Length; i++)
            {
                mesh.AddVert(outline[i], color, Vector2.zero);
                mesh.AddVert(center + (outline[i] - center) * scale, color, Vector2.zero);
            }
            for (int i = 0; i < outline.Length; i++)
            {
                int a = i * 2;
                int b = ((i + 1) % outline.Length) * 2;
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
