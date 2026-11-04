using System;
using System.Collections.Generic;
using UnityEngine;
namespace DuelHero.Battle
{
    public static class AttackRangeCalculator
    {
        public static HashSet<Vector2Int> Calculate(Vector2Int origin, DuelHero.Cards.CardEffectDefinition effect, IReadOnlyList<Vector2Int> movementPath)
        {
            if (!effect.UsesMovementPath) return Calculate(origin, effect.rangeOffsets);
            if (movementPath == null || movementPath.Count == 0) throw new ArgumentException("이동 경로가 없습니다.");
            return new HashSet<Vector2Int>(movementPath);
        }
        public static HashSet<Vector2Int> Calculate(Vector2Int origin, Vector2Int[] offsets)
        {
            if (offsets == null || offsets.Length == 0) throw new ArgumentException("공격 범위가 없습니다.", nameof(offsets));
            var cells = new HashSet<Vector2Int>();
            foreach (var offset in offsets) cells.Add(origin + offset);
            return cells;
        }
    }
}
