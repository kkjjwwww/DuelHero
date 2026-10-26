using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    // Only round-start public information; no player reservations or live-unit references.
    public sealed class EnemyDecisionContext
    {
        public IReadOnlyList<CardInstance> Cards { get; }
        public EnemyPlanningState State { get; }
        public Vector2Int Target { get; }
        public int Round { get; }
        public EnemyUnitSnapshot Actor { get; }
        public EnemyUnitSnapshot TargetUnit { get; }
        public IReadOnlyDictionary<Vector2Int, int> DistancesToTarget { get; }
        private readonly Dictionary<(CardDefinition, Vector2Int), EnemyPreparedAction> prepared = new();
        private readonly Dictionary<Vector2Int, AttackOptions> attackOptions = new();

        public EnemyDecisionContext(IReadOnlyList<CardInstance> cards, EnemyPlanningState state, Vector2Int target, int round,
            EnemyUnitSnapshot actor, EnemyUnitSnapshot targetUnit)
        {
            Cards = System.Array.AsReadOnly(cards.ToArray()); State = state; Target = target; Round = round;
            Actor = actor; TargetUnit = targetUnit;
            DistancesToTarget = state.Map.DistancesFrom(target);
        }

        public EnemyPreparedAction GetPreparedAction(EnemyPlannedAction action)
        {
            var definition = action.Card.Definition;
            var direction = CardDirectionRules.CanSelect(definition) ? action.Direction : Vector2Int.right;
            var key = (definition, direction);
            if (!prepared.TryGetValue(key, out var result))
            {
                var baseKey = (definition, Vector2Int.right);
                if (!prepared.TryGetValue(baseKey, out var original))
                {
                    original = new EnemyPreparedAction(definition, Vector2Int.right);
                    prepared.Add(baseKey, original);
                }
                if (direction == Vector2Int.right) return original;
                result = new EnemyPreparedAction(definition, direction, original);
                prepared.Add(key, result);
            }
            return result;
        }

        // At most three copies have been selected. Four cheapest distinct instances are
        // sufficient to find a remaining affordable attack without rescanning the deck per branch.
        internal bool HasAttackOpportunity(Vector2Int position, int energy, EnemyPlannedAction[] selected, int count)
        {
            if (!attackOptions.TryGetValue(position, out var options))
            {
                options = new AttackOptions();
                for (int i = 0; i < Cards.Count; i++)
                {
                    var card = Cards[i];
                    if (!card.CanReserve(Round) || !EnemyPlanningRules.Has(card, "damage")) continue;
                    for (int direction = 0; direction < EnemyPlanningRules.DirectionCount(card); direction++)
                    {
                        var action = GetPreparedAction(EnemyPlanningRules.Action(card, direction));
                        if (!action.CanHit(action.MoveFrom(position, State.Map), Target)) continue;
                        options.Add(card);
                        break;
                    }
                }
                attackOptions.Add(position, options);
            }
            for (int i = 0; i < 4; i++)
            {
                var card = options.Get(i);
                if (card == null || card.Definition.energyCost > energy) continue;
                bool used = false;
                for (int slot = 0; slot < count; slot++) if (selected[slot].Card == card) { used = true; break; }
                if (!used) return true;
            }
            return false;
        }

        private struct AttackOptions
        {
            private CardInstance first, second, third, fourth;
            internal CardInstance Get(int index) => index == 0 ? first : index == 1 ? second : index == 2 ? third : fourth;
            internal void Add(CardInstance card)
            {
                for (int i = 0; i < 4; i++) if (Get(i) == card) return;
                if (first == null || card.Definition.energyCost < first.Definition.energyCost)
                { fourth = third; third = second; second = first; first = card; }
                else if (second == null || card.Definition.energyCost < second.Definition.energyCost)
                { fourth = third; third = second; second = card; }
                else if (third == null || card.Definition.energyCost < third.Definition.energyCost)
                { fourth = third; third = card; }
                else if (fourth == null || card.Definition.energyCost < fourth.Definition.energyCost) fourth = card;
            }
        }
    }
}

