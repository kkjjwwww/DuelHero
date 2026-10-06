using System.Collections;
using System.Linq;
using UnityEngine;
using DuelHero.Logging;

namespace DuelHero.Cards
{
    public sealed class CardActionExecutor : MonoBehaviour
    {
        [SerializeField] private CardReservationQueue queue;
        [SerializeField] private GridMovement movement;
        [SerializeField] private BattleLog battleLog;
        [SerializeField] private string actorId;
        [SerializeField, Min(0.01f)] private float stepDelay = 0.4f;
        public string LastError { get; private set; }
        private void Start() => Record(BattleLogKind.TurnStarted);
        private void Record(BattleLogKind kind, string cardId = null, BattleLogResult result = BattleLogResult.Success,
            Vector2Int from = default, Vector2Int to = default, int value = 0)
        {
            if (battleLog != null && queue != null) battleLog.Record(new BattleLogEntry(kind, queue.CurrentRound, actorId, cardId, result, from, to, value));
        }
        public bool TryExecute()
        {
            if (queue == null || movement == null || !isActiveAndEnabled || queue.IsExecuting || queue.Reservations.Count != 3) return false;
            foreach (var card in queue.Reservations)
            {
                if (card.Definition.effects == null || card.Definition.effects.Length == 0 ||
                    card.Definition.effects.Any(effect => effect.effectType != "move" || effect.value < 1 || !TryDirection(effect.fixedDirection, out _)))
                {
                    LastError = card.Definition.name + ": 현재 이동 효과만 실행할 수 있습니다. 예약을 변경해주세요.";
                    Record(BattleLogKind.ExecutionBlocked, card.Definition.id, BattleLogResult.UnsupportedEffect);
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
                Record(BattleLogKind.CardExecuted, queue.Reservations[slot].Definition.id);
                foreach (var effect in queue.Reservations[slot].Definition.effects.OrderBy(effect => effect.resolutionOrder))
                {
                    TryDirection(effect.fixedDirection, out var direction);
                    for (int step = 0; step < effect.value; step++)
                    {
                        Vector2Int from = movement.GridPosition;
                        bool moved = movement.TryMove(direction);
                        Record(BattleLogKind.Movement, queue.Reservations[slot].Definition.id,
                            moved ? BattleLogResult.Success : BattleLogResult.BoundaryBlocked, from, movement.GridPosition, moved ? 1 : 0);
                        yield return new WaitForSeconds(stepDelay);
                        if (!moved) break;
                    }
                }
            }
            Record(BattleLogKind.TurnEnded);
            queue.CompleteExecution();
            Record(BattleLogKind.TurnStarted);
        }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (queue != null && queue.IsExecuting) queue.AbortExecution();
        }
        private static bool TryDirection(string value, out Vector2Int direction)
        {
            direction = value switch { "상" => Vector2Int.up, "하" => Vector2Int.down, "좌" => Vector2Int.left, "우" => Vector2Int.right, _ => Vector2Int.zero };
            return direction != Vector2Int.zero;
        }
    }
}
