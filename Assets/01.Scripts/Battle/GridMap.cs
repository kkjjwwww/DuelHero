using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuelHero.Battle
{
    // Immutable terrain snapshot shared by live movement and pure planning.
    public sealed class GridMap
    {
        private readonly HashSet<Vector2Int> cells;
        private readonly HashSet<Vector2Int> obstacles;
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        public int Width { get; }
        public int Height { get; }

        public GridMap(IEnumerable<Vector2Int> cells, IEnumerable<Vector2Int> obstacles)
        {
            this.cells = new HashSet<Vector2Int>(cells);
            this.obstacles = new HashSet<Vector2Int>(obstacles);
            if (this.cells.Count == 0) throw new ArgumentException("전장에는 칸이 하나 이상 필요합니다.");
            foreach (var cell in this.cells)
            {
                if (cell.x < 0 || cell.y < 0) throw new ArgumentException("전장 좌표는 음수일 수 없습니다.");
                Width = Math.Max(Width, cell.x + 1);
                Height = Math.Max(Height, cell.y + 1);
            }
        }

        public bool Contains(Vector2Int cell) => cells.Contains(cell);
        public bool CanEnter(Vector2Int cell) => cells.Contains(cell) && !obstacles.Contains(cell);
        public Vector2Int Move(Vector2Int origin, Vector2Int direction, int steps)
        {
            for (int step = 0; step < steps; step++)
            {
                var next = origin + direction;
                if (!CanEnter(next)) break;
                origin = next;
            }
            return origin;
        }

        public IReadOnlyDictionary<Vector2Int, int> DistancesFrom(Vector2Int target)
        {
            var distances = new Dictionary<Vector2Int, int>();
            if (!CanEnter(target)) return distances;
            var queue = new Queue<Vector2Int>();
            distances.Add(target, 0);
            queue.Enqueue(target);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (!CanEnter(next) || distances.ContainsKey(next)) continue;
                    distances.Add(next, distances[current] + 1);
                    queue.Enqueue(next);
                }
            }
            return distances;
        }
    }
}
