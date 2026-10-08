using System.Linq;
using DuelHero.Cards;
using UnityEngine;
namespace DuelHero.Battle
{
    public sealed class CardEffectExecutor : MonoBehaviour
    {
        [SerializeField] private CombatEffectResolver combatEffects;
        public bool Supports(string actorId, CardDefinition card)
        {
            if (combatEffects == null || !combatEffects.CanResolve(actorId)) return false;
            if (card == null) return true;
            return card.energyCost >= 0 && card.effects != null && card.effects.Length > 0 && card.effects.All(effect =>
                effect != null && effect.value >= 0 && (effect.effectType switch
                {
                    "move" => effect.targetType == "자신" && effect.value > 0 && TryDirection(effect.rangeOffsets, out _),
                    "guard" => effect.targetType == "자신" && effect.durationSlots > 0,
                    "energyRestore" => effect.targetType == "자신",
                    "damage" => effect.targetType == "적" && effect.rangeOffsets != null && effect.rangeOffsets.Length > 0,
                    _ => false
                }));
        }
        public void ExecutePreparation(string actorId, CardDefinition card, int round)
        {
            if (card == null) return;
            combatEffects.TryGetUnit(actorId, out var movement, out var stats);
            foreach (var effect in card.effects.OrderBy(e => e.resolutionOrder))
            {
                switch (effect.effectType)
                {
                    case "move":
                        TryDirection(effect.rangeOffsets, out var direction);
                        for (int step = 0; step < effect.value; step++)
                            if (!movement.TryMove(direction, card.id, round)) break;
                        break;
                    case "guard": combatEffects.ResolveGuard(actorId, card.id, effect, round); break;
                    case "energyRestore": stats.RestoreEnergy(effect.value); break;
                }
            }
        }
        public static bool TryDirection(Vector2Int[] offsets, out Vector2Int direction)
        {
            direction = offsets != null && offsets.Length == 1 ? offsets[0] : Vector2Int.zero;
            return direction == Vector2Int.up || direction == Vector2Int.down || direction == Vector2Int.left || direction == Vector2Int.right;
        }
    }
}
