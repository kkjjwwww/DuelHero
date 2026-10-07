using System;
using UnityEngine;
namespace DuelHero.Battle
{
    public enum BattleActionKind { Movement, CardExecuted, ExecutionBlocked, TurnStarted, TurnEnded, Damage, GuardApplied, GuardExpired }
    public enum BattleActionOutcome { Success, BoundaryBlocked, UnsupportedEffect, NoTarget }
    public interface IBattleActionSource
    {
        event Action<BattleActionResult> ActionResolved;
    }
    // Actual results, independent of logging or UI. Round 0 means caller supplied no turn context.
    public sealed class BattleActionResult
    {
        public BattleActionKind Kind { get; }
        public BattleActionOutcome Outcome { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string CardId { get; }
        public int Round { get; }
        public int Value { get; }
        public int ReducedDamage { get; }
        public int DurationSlots { get; }
        public Vector2Int From { get; }
        public Vector2Int To { get; }
        public BattleActionResult(BattleActionKind kind, string actorId, int round = 0, string cardId = null,
            BattleActionOutcome outcome = BattleActionOutcome.Success, Vector2Int from = default,
            Vector2Int to = default, int value = 0, string targetId = null, int reducedDamage = 0, int durationSlots = 0)
        {
            Kind = kind; ActorId = actorId; Round = round; CardId = cardId;
            Outcome = outcome; From = from; To = to; Value = value; TargetId = targetId;
            ReducedDamage = reducedDamage; DurationSlots = durationSlots;
        }
    }
}
