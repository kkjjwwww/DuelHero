using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    internal static class EnemyPlanningRules
    {
        internal static bool NeedsEnergy(IEnumerable<CardInstance> cards, int energy) =>
            cards.Where(c => Has(c, "damage")).Select(c => c.Definition.energyCost).DefaultIfEmpty(0).Min() > energy;
        internal static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        internal static bool Has(CardInstance card, string type) => card.Definition.effects.Any(e => e.effectType == type);
        internal static bool Hits(CardDefinition card, Vector2Int origin, Vector2Int target) =>
            CardDirectionRules.ForExecution(card, Vector2Int.left).effects.Any(e => e.effectType == "damage" && AttackRangeCalculator.Calculate(origin, e.rangeOffsets).Contains(target));
    }
}

