using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    // A category uses pooled HorizontalLayoutGroup rows; spacing never becomes negative.
    [RequireComponent(typeof(RectTransform), typeof(LayoutElement))]
    public sealed class CardWrapLayout : MonoBehaviour
    {
        private readonly List<RectTransform> items = new();
        private readonly List<HorizontalLayoutGroup> rows = new();
        private Vector2 cellSize;
        private float spacing, rowSpacing;
        private float lastWidth = -1f;
        private bool dirty;
        public event Action LayoutChanged;
        public int ActiveRowCount { get; private set; }

        public void SetItems(IEnumerable<RectTransform> values, Vector2 size, float gap, float rowGap)
        {
            items.Clear(); items.AddRange(values);
            cellSize = size; spacing = Mathf.Max(0, gap); rowSpacing = Mathf.Max(0, rowGap);
            dirty = true;
        }
        private void OnRectTransformDimensionsChange()
        {
            if (!Mathf.Approximately(lastWidth, ((RectTransform)transform).rect.width)) dirty = true;
        }
        private void LateUpdate()
        {
            if (dirty || !Mathf.Approximately(lastWidth, ((RectTransform)transform).rect.width)) Reflow();
        }
        public void Reflow()
        {
            float width = ((RectTransform)transform).rect.width;
            if (!dirty && Mathf.Approximately(lastWidth, width)) return;
            dirty = false;
            if (width <= 0f || cellSize.x <= 0f) return;
            lastWidth = width;
            float usable = Mathf.Max(0, width - 8f);
            int capacity = Mathf.Max(1, Mathf.FloorToInt(usable / cellSize.x));
            int count = Mathf.CeilToInt(items.Count / (float)capacity);
            while (rows.Count < count)
            {
                var go = new GameObject("CardRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(transform, false);
                var group = go.GetComponent<HorizontalLayoutGroup>();
                group.childAlignment = TextAnchor.MiddleLeft;
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = group.childForceExpandHeight = false;
                group.padding = new RectOffset(4, 4, 0, 0);
                rows.Add(group);
            }
            for (int row = 0; row < rows.Count; row++)
            {
                var group = rows[row];
                group.gameObject.SetActive(row < count);
                if (row >= count) continue;
                int first = row * capacity;
                int length = Mathf.Min(capacity, items.Count - first);
                group.spacing = length < 2 ? spacing : Mathf.Clamp((usable - length * cellSize.x) / (length - 1), 0f, spacing);
                var rect = (RectTransform)group.transform;
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                rect.sizeDelta = new Vector2(0, cellSize.y);
                rect.anchoredPosition = new Vector2(0, -32f - row * (cellSize.y + rowSpacing));
                for (int i = 0; i < length; i++)
                {
                    var item = items[first + i];
                    if (item.parent != rect) item.SetParent(rect, false);
                    item.SetSiblingIndex(i);
                }
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            }
            ActiveRowCount = count;
            GetComponent<LayoutElement>().preferredHeight = 32f + count * cellSize.y + Mathf.Max(0, count - 1) * rowSpacing;
            LayoutChanged?.Invoke();
        }
    }
}
