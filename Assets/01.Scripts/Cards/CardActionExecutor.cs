using System.Collections;
using System.Linq;
using UnityEngine;
using System;
using DuelHero.Battle;

namespace DuelHero.Cards
{
    public sealed class CardActionExecutor : MonoBehaviour, IBattleActionSource
    {
        [SerializeField] private CardReservationQueue queue;
        [SerializeField] private GridMovement movement;
        [SerializeField] private CombatEffectResolver combatEffects;
        [SerializeField] private string actorId;
        [SerializeField, Min(0.01f)] private float stepDelay = 0.4f;
        public string LastError { get; private set; }
        public event Action<BattleActionResult> ActionResolved;
        private void Start() => Publish(BattleActionKind.TurnStarted);
        private void Publish(BattleActionKind kind, string cardId = null, BattleActionOutcome outcome = BattleActionOutcome.Success)
        {
            if (queue != null) ActionResolved?.Invoke(new BattleActionResult(kind, actorId, queue.CurrentRound, cardId, outcome));
        }
        public bool TryExecute()
        {
            if (queue == null || movement == null || !isActiveAndEnabled || queue.IsExecuting || queue.Reservations.Count != 3) return false;
            if (!queue.HasEnoughEnergy(out string energyError)) { LastError = energyError; return false; }
            foreach (var card in queue.Reservations)
            {
                if (card.Definition.effects == null || card.Definition.effects.Length == 0 ||
                    card.Definition.effects.Any(effect => !Supports(effect)))
                {
                    LastError = card.Definition.name + ": 지원하지 않는 효과이거나 유닛 참조가 준비되지 않았습니다.";
                    Publish(BattleActionKind.ExecutionBlocked, card.Definition.id, BattleActionOutcome.UnsupportedEffect);
                    return false;
                }
            }
            LastError = null;
            if (!queue.BeginExecution()) return false;
            StartCoroutine(Execute()); return true;
        }
        private IEnumerator Execute()
        {
            for (int slot = 0; slot < queue.Reservations.Count; slot++)
            {
                queue.SetActiveSlot(slot);
                if (!queue.UnitStats.TrySpendEnergy(queue.Reservations[slot].Definition.energyCost))
                {
                    LastError = "실행 중 에너지가 부족해졌습니다."; queue.AbortExecution(); yield break;
                }
                Publish(BattleActionKind.CardExecuted, queue.Reservations[slot].Definition.id);
                foreach (var effect in queue.Reservations[slot].Definition.effects.OrderBy(effect => effect.resolutionOrder))
                {
                    if (effect.effectType == "guard")
                    {
                        combatEffects.ResolveGuard(actorId, queue.Reservations[slot].Definition.id, effect, queue.CurrentRound);
                        yield return new WaitForSeconds(stepDelay);
                        continue;
                    }
                    if (effect.effectType == "damage")
                    {
                        combatEffects.ResolveDamage(actorId, queue.Reservations[slot].Definition.id, effect, queue.CurrentRound);
                        yield return new WaitForSeconds(stepDelay);
                        continue;
                    }
                    if (effect.effectType == "energyRestore")
                    {
                        queue.UnitStats.RestoreEnergy(effect.value);
                        yield return new WaitForSeconds(stepDelay);
                        continue;
                    }
                    TryDirection(effect.rangeOffsets, out var direction);
                    for (int step = 0; step < effect.value; step++)
                    {
                        bool moved = movement.TryMove(direction, queue.Reservations[slot].Definition.id, queue.CurrentRound);
                        yield return new WaitForSeconds(stepDelay);
                        if (!moved) break;
                    }
                }
                // One shared slot boundary, regardless of effect count or movement steps.
                if (combatEffects != null) combatEffects.EndSlot(queue.CurrentRound);
            }
            Publish(BattleActionKind.TurnEnded);
            queue.CompleteExecution();
            Publish(BattleActionKind.TurnStarted);
        }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (queue != null && queue.IsExecuting) queue.AbortExecution();
        }
        private bool Supports(CardEffectDefinition effect)
        {
            if (effect == null) return false;
            return effect.effectType switch
            {
                "move" => effect.value > 0 && effect.targetType == "자신" && TryDirection(effect.rangeOffsets, out _),
                "energyRestore" => effect.value >= 0 && effect.targetType == "자신",
                "guard" => effect.value >= 0 && effect.durationSlots > 0 && effect.targetType == "자신" && combatEffects != null && combatEffects.CanResolve(actorId),
                "damage" => effect.value >= 0 && effect.targetType == "적" && effect.rangeOffsets != null && effect.rangeOffsets.Length > 0 && combatEffects != null && combatEffects.CanResolve(actorId),
                _ => false
            };
        }
        private static bool TryDirection(Vector2Int[] offsets, out Vector2Int direction)
        {
            direction = offsets != null && offsets.Length == 1 ? offsets[0] : Vector2Int.zero;
            return direction == Vector2Int.up || direction == Vector2Int.down || direction == Vector2Int.left || direction == Vector2Int.right;
        }
    }
}
