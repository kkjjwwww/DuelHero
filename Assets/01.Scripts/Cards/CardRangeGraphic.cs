using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    /// <summary>Draws only occupied range cells plus the player's origin marker.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardRangeGraphic : MaskableGraphic
    {
        [SerializeField] private Color rangeColor = new Color(0.82f, 0.12f, 0.1f, 1f);
        [SerializeField] private Color originColor = Color.white;
        [SerializeField, Range(0f, 0.3f)] private float cellGapRatio = 0.12f;
        [SerializeField, Range(0.2f, 0.9f)] private float originDiameterRatio = 0.56f;
        [SerializeField, Range(8, 48)] private int circleSegments = 24;

        private Vector2Int[] offsets = Array.Empty<Vector2Int>();

        public void SetOffsets(IEnumerable<Vector2Int> values)
        {
            offsets = values == null
                ? Array.Empty<Vector2Int>()
                : values.Distinct().ToArray();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (offsets.Length == 0) return;

            int minX = Mathf.Min(0, offsets.Min(p => p.x));
            int maxX = Mathf.Max(0, offsets.Max(p => p.x));
            int minY = Mathf.Min(0, offsets.Min(p => p.y));
            int maxY = Mathf.Max(0, offsets.Max(p => p.y));
            int columns = maxX - minX + 1;
            int rows = maxY - minY + 1;
            Rect drawRect = GetPixelAdjustedRect();
            float cell = Mathf.Min(drawRect.width / columns, drawRect.height / rows);
            Vector2 boardSize = new Vector2(columns * cell, rows * cell);
            Vector2 boardMin = drawRect.center - boardSize * 0.5f;
            float inset = cell * cellGapRatio * 0.5f;

            foreach (Vector2Int offset in offsets)
            {
                Vector2 center = CellCenter(offset, minX, minY, boardMin, cell);
                AddQuad(vh, new Rect(center.x - cell * 0.5f + inset, center.y - cell * 0.5f + inset,
                    cell - inset * 2f, cell - inset * 2f), rangeColor);
            }

            Vector2 origin = CellCenter(Vector2Int.zero, minX, minY, boardMin, cell);
            AddCircle(vh, origin, cell * originDiameterRatio * 0.5f, originColor);
        }

        private static Vector2 CellCenter(Vector2Int cellPosition, int minX, int minY, Vector2 boardMin, float cellSize)
        {
            return boardMin + new Vector2((cellPosition.x - minX + 0.5f) * cellSize,
                (cellPosition.y - minY + 0.5f) * cellSize);
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        private void AddCircle(VertexHelper vh, Vector2 center, float radius, Color32 color)
        {
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= circleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / circleSegments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * radius, color, direction * 0.5f + Vector2.one * 0.5f);
                if (i > 0) vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
            }
        }
    }
}
