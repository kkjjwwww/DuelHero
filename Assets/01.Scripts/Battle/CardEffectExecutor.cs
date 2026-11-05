using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;
namespace DuelHero.Battle
{
    public sealed class CardEffectExecutor : MonoBehaviour
    {
        [SerializeField] private CombatEffectResolver combatEffects;
        public bool Supports(string actorId, CardDefinition card)
        {
            if (combatEffects == null || !combatEffects.CanResolve(actorId)) return false;
            return SupportsDefinition(card);
        }
        public static bool SupportsDefinition(CardDefinition card)
        {
            if (card == null) return true;
            if (WaitCard.IsWait(card) || StunCard.IsStun(card)) return card.energyCost == 0 && card.effects != null && card.effects.Length == 0;
            return card.energyCost >= 0 && card.effects != null && card.effects.Length > 0 && card.effects.All(effect =>
                effect != null && SupportsMovementMetadata(card, effect) && effect.value >= 0 && effect.statusMultiplier >= 0 &&
                (string.IsNullOrEmpty(effect.triggerTiming) || effect.triggerTiming == "action" || effect.IsCounter) &&
                (!effect.IsCounter || new[] { "damage", "guard", "energyRestore", "healthRestore" }.Contains(effect.effectType)) &&
                (effect.statusMultiplier == 0 || (effect.effectType == "damage" && effect.statusId == "bleed")) && (effect.effectType switch
                {
                    "move" => effect.targetType == "자신" && effect.value > 0 && TryDirection(effect.rangeOffsets, out _),
                    "guard" => effect.targetType == "자신" && effect.durationSlots > 0,
                    "energyRestore" => effect.targetType == "자신",
                    "healthRestore" => effect.targetType == "자신",
                    "damage" => effect.targetType == "적" && effect.rangeOffsets != null && effect.rangeOffsets.Length > 0,
                    "applyStatus" => effect.targetType == "적" && (effect.statusId == "stun" || effect.statusId == "bleed") && effect.value > 0 &&
                        card.effects.Any(hit => hit.effectType == "damage" && hit.id == effect.triggerEffectId && hit.resolutionOrder < effect.resolutionOrder && hit.IsCounter == effect.IsCounter),
                    _ => false
                }));
        }
        public static bool SupportsMovementMetadata(CardDefinition card, CardEffectDefinition effect) =>
            (string.IsNullOrEmpty(effect.movementStyle) || (effect.IsDash && !effect.IsCounter)) &&
            (string.IsNullOrEmpty(effect.rangeBasis) || (effect.UsesMovementPath && effect.effectType == "damage" && !effect.IsCounter &&
                card.effects.Any(e => e.effectType == "move" && !e.IsCounter && e.resolutionOrder < effect.resolutionOrder) &&
                card.effects.Where(e => e.effectType == "move").All(e => e.resolutionOrder < effect.resolutionOrder)));
        public void ExecutePreparation(string actorId, CardDefinition card, int round, bool counter = false, List<Vector2Int> movementPath = null)
        {
            var steps = ExecutePreparationSteps(actorId, card, round, counter, null, null, false, null, movementPath);
            try { while (steps.MoveNext()) { } }
            finally { (steps as System.IDisposable)?.Dispose(); }
        }
        public IEnumerator ExecutePreparationAnimated(string actorId, CardDefinition card, int round,
            UnitJumpPresentation jump, UnitVisualPlacement placement, bool counter = false,
            UnitAttackPresentation attack = null, List<Vector2Int> movementPath = null) =>
            ExecutePreparationSteps(actorId, card, round, counter, jump, placement, true, attack, movementPath);

        public void ShowAttackRange(UnitAttackPresentation attack, string actorId, CardDefinition card,
            IReadOnlyList<Vector2Int> movementPath = null, bool counter = false)
        {
            if (attack == null || card == null || !combatEffects.TryGetUnit(actorId, out var unit, out _)) return;
            var cells = new HashSet<Vector2Int>();
            foreach (var effect in card.effects.Where(e => e.effectType == "damage" && e.IsCounter == counter))
                cells.UnionWith(AttackRangeCalculator.Calculate(unit.GridPosition, effect, movementPath));
            cells.RemoveWhere(cell => !unit.Grid.Contains(cell));
            attack.ShowRange(unit, cells);
        }

        public IEnumerator PlayAttackAnimated(UnitAttackPresentation attack, string actorId, CardDefinition card,
            IReadOnlyList<Vector2Int> movementPath = null, bool counter = false)
        {
            if (attack == null) yield break;
            try
            {
                ShowAttackRange(attack, actorId, card, movementPath, counter);
                yield return attack.PlayAttack(card?.attackStyle);
            }
            finally { attack.HideRange(); }
        }

        public void CancelPreparationPresentation(string actorId)
        {
            if (combatEffects != null && combatEffects.TryGetUnit(actorId, out var movement, out _))
                movement.GetComponent<UnitSpritePresenter>()?.CancelSupportPose();
        }

        private IEnumerator ExecutePreparationSteps(string actorId, CardDefinition card, int round, bool counter,
            UnitJumpPresentation jump, UnitVisualPlacement placement, bool animateSupport, UnitAttackPresentation attack, List<Vector2Int> movementPath)
        {
            if (card == null) yield break;
            combatEffects.TryGetUnit(actorId, out var movement, out var stats);
            var sprites = animateSupport ? movement.GetComponent<UnitSpritePresenter>() : null;
            movementPath?.Clear(); movementPath?.Add(movement.GridPosition);
            foreach (var effect in card.effects.Where(e => e.IsCounter == counter).OrderBy(e => e.resolutionOrder))
            {
                switch (effect.effectType)
                {
                    case "move":
                        TryDirection(effect.rangeOffsets, out var direction);
                        bool dash = effect.IsDash && attack != null && attack.isActiveAndEnabled;
                        Vector3 dashStart = dash ? attack.MotionWorldPosition : Vector3.zero;
                        for (int step = 0; step < effect.value; step++)
                        {
                            bool animate = !effect.IsDash && jump != null && jump.isActiveAndEnabled;
                            Vector3 before = animate ? jump.MotionWorldPosition : Vector3.zero;
                            if (!movement.TryMove(direction, card.id, round)) break;
                            movementPath?.Add(movement.GridPosition);
                            if (animate)
                            {
                                // Include layout offsets when entering/leaving an occupied cell.
                                placement?.Refresh();
                                yield return jump.PlayJump(jump.MotionWorldPosition - before);
                                if (!movement.isActiveAndEnabled) yield break;
                            }
                        }
                        if (dash)
                        {
                            placement?.Refresh();
                            try
                            {
                                ShowAttackRange(attack, actorId, card, movementPath, counter);
                                yield return attack.PlayDash(attack.MotionWorldPosition - dashStart);
                            }
                            finally { attack.HideRange(); }
                        }
                        break;
                    case "guard": combatEffects.ResolveGuard(actorId, card.id, effect, round); break;
                    case "energyRestore": stats.RestoreEnergy(effect.value); break;
                    case "healthRestore": stats.Heal(effect.value); break;
                }
                if (sprites != null && effect.value > 0)
                {
                    if (effect.effectType == "guard") yield return sprites.PlaySupportPose(UnitPose.Block, true);
                    else if (effect.effectType == "healthRestore" || effect.effectType == "energyRestore")
                        yield return sprites.PlaySupportPose(UnitPose.Heal);
                    if (!movement.isActiveAndEnabled) yield break;
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
