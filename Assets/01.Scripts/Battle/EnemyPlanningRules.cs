using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    internal static class EnemyPlanningRules
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        // Preserve card order, then use the declared direction order for ties.
        internal static IEnumerable<EnemyPlannedAction> Actions(CardInstance card) =>
            CardDirectionRules.CanSelect(card.Definition)
                ? Directions.Select(direction => new EnemyPlannedAction(card, direction))
                : new[] { new EnemyPlannedAction(card, Vector2Int.left) };

        internal static bool NeedsEnergy(IEnumerable<CardInstance> cards, int energy) =>
            cards.Where(c => Has(c, "damage")).Select(c => c.Definition.energyCost).DefaultIfEmpty(0).Min() > energy;
        internal static bool Has(CardInstance card, string type) => card.Definition.effects.Any(e => e.effectType == type);
        internal static bool Hits(EnemyPlannedAction action, EnemyPlanningState state, Vector2Int target)
        {
            var origin = state.Predict(action.Card.Definition, action.Direction).Position;
            return action.ForExecution().effects.Any(e => e.effectType == "damage" &&
                AttackRangeCalculator.Calculate(origin, e.rangeOffsets).Contains(target));
        }
    }
}

