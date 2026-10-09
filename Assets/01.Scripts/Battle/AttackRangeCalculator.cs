using System;
using System.Collections.Generic;
using UnityEngine;
namespace DuelHero.Battle
{
    public static class AttackRangeCalculator
    {
        public static HashSet<Vector2Int> Calculate(Vector2Int origin, Vector2Int[] offsets, bool mirrorX = false)
        {
            if (offsets == null || offsets.Length == 0) throw new ArgumentException("공격 범위가 없습니다.", nameof(offsets));
            var cells = new HashSet<Vector2Int>();
            foreach (var offset in offsets) cells.Add(origin + (mirrorX ? new Vector2Int(-offset.x, offset.y) : offset));
            return cells;
        }
    }
}
