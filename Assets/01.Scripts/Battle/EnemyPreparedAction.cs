using DuelHero.Cards;
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
        private readonly bool hasMove;

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
                if (Effects[i].effectType == "damage") HasDamage = true;
                if (Effects[i].effectType == "move") hasMove = true;
            }
        }

        internal Vector2Int MoveFrom(Vector2Int origin, GridMap map)
        {
            if (!hasMove) return origin;
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                if (effect.effectType == "move" && CardEffectExecutor.TryDirection(effect.rangeOffsets, out var direction))
                    origin = map.Move(origin, CardDirectionRules.Rotate(direction, this.direction), effect.value);
            }
            return origin;
        }

        internal bool CanHit(Vector2Int origin, Vector2Int target)
        {
            if (!HasDamage) return false;
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                if (effect.effectType == "damage" && Hits(effect, origin, target))
                    return true;
            }
            return false;
        }

        internal int RawDamage(Vector2Int origin, Vector2Int target)
        {
            int damage = 0;
            if (!HasDamage) return damage;
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                if (effect.effectType == "damage" && Hits(effect, origin, target)) damage += effect.value;
            }
            return damage;
        }

        internal bool Hits(CardEffectDefinition effect, Vector2Int origin, Vector2Int target)
        {
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
