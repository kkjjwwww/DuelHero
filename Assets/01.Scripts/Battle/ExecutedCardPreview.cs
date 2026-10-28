using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuelHero.Battle
{
    // A snapshot of actual execution, independent of subsequent unit movement.
    public sealed class ExecutedCardPreview
    {
        public IReadOnlyList<Vector2Int> MovementPath { get; }
        public IReadOnlyList<Vector2Int> AttackCells { get; }
        public Vector2Int EndPosition => MovementPath[MovementPath.Count - 1];

        public ExecutedCardPreview(IEnumerable<Vector2Int> path, IEnumerable<Vector2Int> attacks)
        {
            MovementPath = Array.AsReadOnly(path.ToArray());
            AttackCells = Array.AsReadOnly(attacks.OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToArray());
        }
    }
}
