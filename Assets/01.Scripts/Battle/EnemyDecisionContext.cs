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
        public uint DecisionSeed { get; }
        public EnemyDecisionStyle Style { get; }
        public EnemyUnitSnapshot Actor { get; }
        public EnemyUnitSnapshot TargetUnit { get; }
        private readonly Dictionary<(CardDefinition, Vector2Int), EnemyPreparedAction> prepared = new();
        private readonly Dictionary<Vector2Int, AttackOptions> attackOptions = new();
        private bool attacksPrepared;
        private readonly Dictionary<Vector2Int, int> risks = new();
        private readonly Dictionary<Vector2Int, int> attackDistances = new();
        private readonly Queue<Vector2Int> pathQueue = new();
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        public EnemyDecisionContext(IReadOnlyList<CardInstance> cards, EnemyPlanningState state, Vector2Int target, int round,
            EnemyUnitSnapshot actor, EnemyUnitSnapshot targetUnit, uint decisionSeed = 0,
            IReadOnlyList<CardInstance> targetCards = null)
        {
            Cards = System.Array.AsReadOnly(cards.ToArray()); State = state; Target = target; Round = round;
            Actor = actor; TargetUnit = targetUnit;
            DecisionSeed = decisionSeed;
            Style = EnemyDecisionRandom.Style(decisionSeed);
            // Snapshot only definition/cooldown/energy, never private reservation state.
            if (targetCards != null)
            {
                var unique = new HashSet<CardDefinition>();
                for (int i = 0; i < targetCards.Count; i++)
                {
                    var card = targetCards[i];
                    if (card.RemainingCooldown(round) != 0 || card.Definition.energyCost < 0 ||
                        card.Definition.energyCost > targetUnit.Energy || !EnemyPlanningRules.Has(card, "damage") ||
                        !unique.Add(card.Definition)) continue;
                    for (int d = 0; d < EnemyPlanningRules.DirectionCount(card); d++)
                    {
                        var action = GetPreparedAction(EnemyPlanningRules.Action(card, d));
                        var origin = action.MoveFrom(target, state.Map);
                        for (int e = 0; e < action.Effects.Length; e++)
                        {
                            var effect = action.Effects[e];
                            if (effect.effectType != "damage" || effect.IsCounter) continue;
                            for (int o = 0; o < effect.rangeOffsets.Length; o++)
                            {
                                var cell = action.AttackCell(origin, effect.rangeOffsets[o]);
                                if (!state.Map.CanEnter(cell)) continue;
                                int damage = action.RawDamage(origin, cell, actor.Bleed);
                                if (damage > RiskAt(cell)) risks[cell] = damage;
                            }
                        }
                    }
                }
            }
        }

        public int RiskAt(Vector2Int position) => risks.TryGetValue(position, out int value) ? value : 0;

        // One multi-source terrain search per slot, not one path search per candidate.
        internal void PrepareAttackDistances(int energy, EnemyPlannedAction[] selected, int count)
        {
            attackDistances.Clear(); pathQueue.Clear();
            for (int y = 0; y < State.Map.Height; y++) for (int x = 0; x < State.Map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!State.Map.CanEnter(cell) || !HasAttackOpportunity(cell, energy, selected, count)) continue;
                attackDistances.Add(cell, 0); pathQueue.Enqueue(cell);
            }
            while (pathQueue.Count > 0)
            {
                var cell = pathQueue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (!State.Map.CanEnter(next) || attackDistances.ContainsKey(next)) continue;
                    attackDistances.Add(next, attackDistances[cell] + 1); pathQueue.Enqueue(next);
                }
            }
        }

        internal int AttackApproach(Vector2Int position) =>
            attackDistances.TryGetValue(State.Position, out int initial) && attackDistances.TryGetValue(position, out int current)
                ? System.Math.Min(3, System.Math.Max(0, initial - current)) : 0;

        internal bool HasAvailableAttack(int energy, EnemyPlannedAction[] selected, int count)
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                var card = Cards[i];
                if (!card.CanReserve(Round) || card.Definition.energyCost < 0 || card.Definition.energyCost > energy ||
                    !EnemyPlanningRules.Has(card, "damage")) continue;
                bool used = false;
                for (int s = 0; s < count; s++) if (selected[s].Card == card) { used = true; break; }
                if (!used) return true;
            }
            return false;
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
        // sufficient to find a remaining affordable attack without rescanning the deck per candidate.
        internal bool HasAttackOpportunity(Vector2Int position, int energy, EnemyPlannedAction[] selected, int count)
        {
            if (!attacksPrepared)
            {
                attacksPrepared = true;
                for (int i = 0; i < Cards.Count; i++)
                {
                    var card = Cards[i];
                    if (!card.CanReserve(Round) || card.Definition.energyCost < 0 || !EnemyPlanningRules.Has(card, "damage")) continue;
                    for (int direction = 0; direction < EnemyPlanningRules.DirectionCount(card); direction++)
                    {
                        var action = GetPreparedAction(EnemyPlanningRules.Action(card, direction));
                        if (action.HasMovement)
                        {
                            for (int y = 0; y < State.Map.Height; y++) for (int x = 0; x < State.Map.Width; x++)
                            {
                                var cell = new Vector2Int(x, y);
                                if (State.Map.CanEnter(cell) && action.CanHit(action.MoveFrom(cell, State.Map), Target))
                                    AddAttackOption(cell, card);
                            }
                        }
                        else for (int e = 0; e < action.Effects.Length; e++)
                        {
                            var effect = action.Effects[e];
                            if (effect.effectType != "damage") continue;
                            for (int o = 0; o < effect.rangeOffsets.Length; o++)
                            {
                                var cell = action.AttackOrigin(Target, effect.rangeOffsets[o]);
                                if (State.Map.CanEnter(cell)) AddAttackOption(cell, card);
                            }
                        }
                    }
                }
            }
            if (!attackOptions.TryGetValue(position, out var options)) return false;
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

        private void AddAttackOption(Vector2Int position, CardInstance card)
        {
            attackOptions.TryGetValue(position, out var options);
            options.Add(card);
            attackOptions[position] = options;
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

