using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    internal static class EnemyPlanningRules
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        internal static int DirectionCount(CardInstance card) => CardDirectionRules.CanSelect(card.Definition) ? 4 : 1;
        internal static EnemyPlannedAction Action(CardInstance card, int index) =>
            new EnemyPlannedAction(card, CardDirectionRules.CanSelect(card.Definition) ? Directions[index] : Vector2Int.left);

        // Preserve card order, then use the declared direction order for ties.
        internal static IEnumerable<EnemyPlannedAction> Actions(CardInstance card)
        {
            for (int i = 0; i < DirectionCount(card); i++) yield return Action(card, i);
        }

        internal static bool NeedsEnergy(IEnumerable<CardInstance> cards, int energy) =>
            cards.Where(c => Has(c, "damage")).Select(c => c.Definition.energyCost).DefaultIfEmpty(0).Min() > energy;
        internal static bool Has(CardInstance card, string type)
        {
            foreach (var effect in card.Definition.effects) if (effect.effectType == type) return true;
            return false;
        }
        internal static bool Hits(EnemyPlannedAction action, EnemyPlanningState state, EnemyDecisionContext context)
        {
            var prepared = context.GetPreparedAction(action);
            var origin = state.Predict(prepared).Position;
            return prepared.CanHit(origin, context.Target);
        }
    }
}

