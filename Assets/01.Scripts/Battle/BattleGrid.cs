using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuelHero.Battle
{
    // Dimensions come from registered scene tiles, never from individual units.
    public sealed class BattleGrid : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        private readonly HashSet<Vector2Int> cells = new();
        private GridMap map;
        [NonSerialized] private bool initialized;
        public GridMap Map { get { EnsureInitialized(); return map; } }
        public int Width => Map.Width;
        public int Height => Map.Height;
        public float CellSize => transform.TransformVector(Vector3.right * cellSize).magnitude;

        public void Invalidate() => initialized = false;
        private void OnValidate() => Invalidate();
        private void OnTransformChildrenChanged() => Invalidate();
        private void EnsureInitialized() { if (!initialized) Rebuild(); }
        public void Rebuild()
        {
            initialized = false;
            cells.Clear();
            var obstacles = new HashSet<Vector2Int>();
            if (cellSize <= 0) throw new InvalidOperationException("전장 칸 간격은 0보다 커야 합니다.");
            foreach (var tile in GetComponentsInChildren<GridCell>())
            {
                // Include enabled scene tiles even before their OnEnable callback has run.
                if (!tile.enabled || !tile.gameObject.activeInHierarchy || tile.GetComponentInParent<BattleGrid>() != this) continue;
                var cell = tile.Coordinate;
                if (cell.x < 0 || cell.y < 0 || !cells.Add(cell))
                    throw new InvalidOperationException("전장 타일 좌표에 음수 또는 중복이 있습니다.");
                var local = transform.InverseTransformPoint(tile.transform.position);
                if (Mathf.Abs(local.x - cell.x * cellSize) > .001f || Mathf.Abs(local.z - cell.y * cellSize) > .001f)
                    throw new InvalidOperationException("전장 타일 위치가 좌표·칸 간격과 일치하지 않습니다.");
                if (tile.Terrain == GridTerrain.LowObstacle) obstacles.Add(cell);
            }
            map = new GridMap(cells, obstacles);
            initialized = true;
        }
        public bool Contains(Vector2Int cell) => Map.Contains(cell);
        public bool CanEnter(Vector2Int cell) => Map.CanEnter(cell);
        public Vector3 CellToWorld(Vector2Int cell, float elevation = 0f) =>
            transform.TransformPoint(new Vector3(cell.x * cellSize, 0f, cell.y * cellSize)) + transform.up * elevation;
    }
}
