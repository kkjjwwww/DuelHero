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

        internal static bool Has(CardInstance card, string type)
        {
            foreach (var effect in card.Definition.effects) if (effect.effectType == type) return true;
            return false;
        }
    }
}

