using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuelHero.Units;

namespace DuelHero.Battle
{
    // Only visual children are offset; logical grid positions stay unchanged.
    public sealed class UnitVisualPlacement : MonoBehaviour
    {
        [Serializable]
        private sealed class UnitVisual
        {
            public GridMovement movement;
            public SpriteRenderer sprite;
            public UnitDataKind side;
            public bool artworkFacesRight = true;
            [NonSerialized] public Vector3 position, scale;
            [NonSerialized] public bool captured;
        }
        [SerializeField] private BoardOccupancy board;
        [SerializeField] private UnitVisual[] units = Array.Empty<UnitVisual>();
        [SerializeField, Min(0)] private float gap = 0.06f;
        [SerializeField, Min(0)] private float cellPadding = 0.05f;
        private readonly List<UnitVisual> entries = new();
        private void Awake()
        {
            foreach (var unit in units)
            {
                if (unit == null || !Capture(unit)) continue;
                if (board != null && !board.RegisterUnit(unit.movement))
                { Debug.LogError("전장에 등록할 수 있는 유닛은 플레이어 포함 최대 4명입니다.", this); continue; }
                if (!entries.Any(e => e.movement == unit.movement)) entries.Add(unit);
            }
        }
        private bool Capture(UnitVisual entry)
        {
            if (entry.movement == null || entry.sprite == null || entry.sprite.transform == entry.movement.transform)
            { Debug.LogError("유닛과 별도 Visual 자식의 스프라이트를 연결해주세요.", this); return false; }
            if (!entry.captured)
            {
                entry.position = entry.sprite.transform.localPosition;
                entry.scale = entry.sprite.transform.localScale;
                entry.captured = true;
            }
            return true;
        }
        // Spawn systems can register units without changing the turn controller.
        public bool RegisterUnit(GridMovement movement, SpriteRenderer sprite, UnitDataKind side, bool artworkFacesRight = true)
        {
            if (entries.Any(e => e.movement == movement)) return true;
            var entry = new UnitVisual { movement = movement, sprite = sprite, side = side, artworkFacesRight = artworkFacesRight };
            if (!Capture(entry) || (board != null && !board.RegisterUnit(movement))) return false;
            entries.Add(entry);
            return true;
        }
        public void UnregisterUnit(GridMovement movement)
        {
            foreach (var entry in entries.Where(e => e.movement == movement).ToArray())
            { Restore(entry); entries.Remove(entry); }
            if (board != null) board.UnregisterUnit(movement);
        }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            foreach (var entry in entries) Restore(entry);
            var active = entries.Where(e => e.movement != null && e.sprite != null &&
                e.movement.isActiveAndEnabled && e.movement.gameObject.activeInHierarchy).ToArray();
            foreach (var group in active.GroupBy(e => e.movement.GridPosition))
            {
                var occupants = group.OrderBy(e => e.side).ToArray();
                int count = occupants.Length;
                if (count > BoardOccupancy.MaxUnits) continue;
                float available = Mathf.Max(0.01f, occupants.Min(e => e.movement.CellSize) - 2 * cellPadding);
                if (count == 2)
                {
                    float firstWidth = Width(occupants[0]), secondWidth = Width(occupants[1]);
                    float factor = FitScale(firstWidth, secondWidth, gap, available);
                    float actualGap = Mathf.Min(gap, available * 0.25f);
                    Place(occupants[0], new Vector3(-(firstWidth * factor + actualGap) * 0.5f, 0, 0), factor);
                    Place(occupants[1], new Vector3((secondWidth * factor + actualGap) * 0.5f, 0, 0), factor);
                }
                else if (count > 2)
                {
                    float maxWidth = occupants.Max(Width), maxHeight = occupants.Max(Height);
                    float factor = Mathf.Min(1, maxWidth > 0 ? (available * 0.5f - gap) / maxWidth : 1);
                    factor = Mathf.Min(factor, maxHeight > 0 ? available * 0.4f / maxHeight : 1);
                    factor = Mathf.Clamp01(factor);
                    for (int i = 0; i < count; i++) Place(occupants[i], SlotOffset(count, i, available), factor);
                }
            }
            // Face the nearest opposing side using visual positions, not range coordinates.
            foreach (var entry in active)
            {
                var opponent = active.Where(other => other.side != entry.side)
                    .OrderBy(other => (other.movement.GridPosition - entry.movement.GridPosition).sqrMagnitude)
                    .FirstOrDefault();
                bool facesRight = entry.side == UnitDataKind.Player;
                if (opponent != null)
                {
                    float dx = opponent.sprite.transform.position.x - entry.sprite.transform.position.x;
                    if (Mathf.Abs(dx) > 0.001f) facesRight = dx > 0;
                }
                entry.sprite.flipX = entry.artworkFacesRight != facesRight;
            }
        }
        public static Vector3 SlotOffset(int count, int index, float available)
        {
            if (count < 1 || count > 4 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 1) return Vector3.zero;
            if (count == 2) return new Vector3((index == 0 ? -1 : 1) * available * 0.25f, 0, 0);
            float x = count == 3 && index == 2 ? 0 : (index % 2 == 0 ? -1 : 1) * available * 0.25f;
            return new Vector3(x, 0, (index < 2 ? 1 : -1) * available * 0.32f);
        }
        public static float FitScale(float playerWidth, float enemyWidth, float gap, float available)
        {
            float total = playerWidth + enemyWidth;
            if (total <= 0) return 1;
            float actualGap = Mathf.Min(Mathf.Max(0, gap), available * 0.25f);
            return Mathf.Clamp01((available - actualGap) / total);
        }
        private static float Width(UnitVisual entry) => entry.sprite.sprite == null ? 0 : Mathf.Abs(entry.sprite.sprite.bounds.size.x * entry.sprite.transform.lossyScale.x);
        private static float Height(UnitVisual entry) => entry.sprite.sprite == null ? 0 : Mathf.Abs(entry.sprite.sprite.bounds.size.y * entry.sprite.transform.lossyScale.y);
        private static void Place(UnitVisual entry, Vector3 offset, float factor)
        {
            entry.sprite.transform.localScale = entry.scale * factor;
            entry.sprite.transform.position += offset;
        }
        private static void Restore(UnitVisual entry)
        {
            if (!entry.captured || entry.sprite == null) return;
            entry.sprite.transform.localPosition = entry.position;
            entry.sprite.transform.localScale = entry.scale;
        }
        private void OnDisable() { foreach (var entry in entries) Restore(entry); }
    }
}

