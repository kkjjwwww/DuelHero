using DuelHero.Cards;
using System.Collections.Generic;
using UnityEngine;

namespace DuelHero.Battle
{
    // Prepared once per definition/direction in a decision context.
    public sealed class EnemyPreparedAction
    {
        private readonly CardDefinition definition;
        private readonly Vector2Int direction;
        private CardDefinition executionCard;
        public CardDefinition ExecutionCard => executionCard ??= CardDirectionRules.ForExecution(definition, direction);
        public int EnergyCost => definition.energyCost;
        // Source coordinates are shared across directions; only the final execution card
        // materializes rotated effect arrays. Prediction rotates range checks without allocations.
        internal readonly CardEffectDefinition[] Effects;
        public bool HasDamage { get; }
        internal bool HasPathDamage { get; }
        private readonly bool hasMove;
        internal bool HasMovement => hasMove;
        internal Vector2Int AttackCell(Vector2Int origin, Vector2Int offset) => origin + CardDirectionRules.Rotate(offset, direction);
        internal Vector2Int AttackOrigin(Vector2Int target, Vector2Int offset) => target - CardDirectionRules.Rotate(offset, direction);

        internal EnemyPreparedAction(CardDefinition card, Vector2Int direction, EnemyPreparedAction original = null)
        {
            definition = card;
            this.direction = CardDirectionRules.CanSelect(card) ? direction : Vector2Int.right;
            if (!CardDirectionRules.IsCardinal(this.direction)) throw new System.ArgumentException("상하좌우 방향을 지정해주세요.");
            if (original == null)
            {
                var effects = (CardEffectDefinition[])card.effects.Clone();
                // Small effect lists: stable insertion sort also preserves equal-order rules.
                for (int i = 1; i < effects.Length; i++)
                {
                    var value = effects[i]; int j = i;
                    while (j > 0 && effects[j - 1].resolutionOrder > value.resolutionOrder)
                    { effects[j] = effects[j - 1]; j--; }
                    effects[j] = value;
                }
                Effects = effects;
            }
            else Effects = original.Effects;
            for (int i = 0; i < Effects.Length; i++)
            {
                if (Effects[i].effectType == "damage" && !Effects[i].IsCounter) HasDamage = true;
                if (Effects[i].UsesMovementPath) HasPathDamage = true;
                if (Effects[i].effectType == "move" && !Effects[i].IsCounter) hasMove = true;
            }
        }

        internal Vector2Int MoveFrom(Vector2Int origin, GridMap map, List<Vector2Int> path = null)
        {
            path?.Clear(); path?.Add(origin);
            if (!hasMove) return origin;
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                if (!effect.IsCounter && effect.effectType == "move" && CardEffectExecutor.TryDirection(effect.rangeOffsets, out var direction))
                {
                    var rotated = CardDirectionRules.Rotate(direction, this.direction);
                    for (int step = 0; step < effect.value; step++)
                    {
                        var next = map.Move(origin, rotated, 1);
                        if (next == origin) break;
                        origin = next; path?.Add(origin);
                    }
                }
            }
            return origin;
        }

        internal bool CanHit(Vector2Int origin, Vector2Int target, IReadOnlyList<Vector2Int> path = null)
        {
            if (!HasDamage) return false;
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                if (effect.effectType == "damage" && !effect.IsCounter && Hits(effect, origin, target, path))
                    return true;
            }
            return false;
        }

        internal int RawDamage(Vector2Int origin, Vector2Int target, int bleed = 0, IReadOnlyList<Vector2Int> path = null)
        {
            int damage = 0;
            if (!HasDamage) return damage;
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                if (effect.effectType == "damage" && !effect.IsCounter && Hits(effect, origin, target, path))
                    damage += effect.value + (effect.statusId == "bleed" ? bleed * effect.statusMultiplier : 0);
            }
            return damage;
        }
        internal int AppliedBleed(Vector2Int origin, Vector2Int target, IReadOnlyList<Vector2Int> path = null)
        {
            int amount = 0;
            foreach (var effect in Effects)
                if (effect.effectType == "applyStatus" && effect.statusId == "bleed" && !effect.IsCounter)
                    foreach (var hit in Effects)
                        if (hit.id == effect.triggerEffectId && hit.effectType == "damage" && !hit.IsCounter && Hits(hit, origin, target, path))
                            amount += effect.value;
            return amount;
        }

        internal bool Hits(CardEffectDefinition effect, Vector2Int origin, Vector2Int target, IReadOnlyList<Vector2Int> path = null)
        {
            if (effect.UsesMovementPath)
            {
                if (path == null) throw new System.ArgumentException("경로 공격 평가에 이동 경로가 필요합니다.");
                for (int i = 0; i < path.Count; i++) if (path[i] == target) return true;
                return false;
            }
            var offsets = effect.rangeOffsets;
            // Rotate the target once into the card's source coordinates, rather than
            // rotating every cell of an area attack on every candidate check.
            var localTarget = CardDirectionRules.Rotate(target - origin, new Vector2Int(direction.x, -direction.y));
            for (int i = 0; i < offsets.Length; i++)
                if (offsets[i] == localTarget) return true;
            return false;
        }
    }
}
