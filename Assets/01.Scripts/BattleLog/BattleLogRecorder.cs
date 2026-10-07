using System;
using System.Collections.Generic;
using DuelHero.Battle;
using DuelHero.Cards;
using UnityEngine;
namespace DuelHero.Logging
{
    public sealed class BattleLogRecorder : MonoBehaviour
    {
        [SerializeField] private BattleLog destination;
        [SerializeField] private CardReservationQueue turnSource;
        [SerializeField] private MonoBehaviour[] sources = Array.Empty<MonoBehaviour>();
        private readonly HashSet<IBattleActionSource> subscribed = new();
        private void OnEnable()
        {
            foreach (var component in sources)
                if (component is IBattleActionSource source && subscribed.Add(source)) source.ActionResolved += Record;
        }
        private void OnDisable()
        {
            foreach (var source in subscribed) source.ActionResolved -= Record;
            subscribed.Clear();
        }
        private void Record(BattleActionResult result)
        {
            if (destination == null) return;
            var kind = result.Kind switch
            {
                BattleActionKind.Movement => BattleLogKind.Movement,
                BattleActionKind.CardExecuted => BattleLogKind.CardExecuted,
                BattleActionKind.ExecutionBlocked => BattleLogKind.ExecutionBlocked,
                BattleActionKind.TurnStarted => BattleLogKind.TurnStarted,
                BattleActionKind.TurnEnded => BattleLogKind.TurnEnded,
                BattleActionKind.Damage => BattleLogKind.Damage,
                _ => throw new ArgumentOutOfRangeException()
            };
            var outcome = result.Outcome switch
            {
                BattleActionOutcome.Success => BattleLogResult.Success,
                BattleActionOutcome.BoundaryBlocked => BattleLogResult.BoundaryBlocked,
                BattleActionOutcome.UnsupportedEffect => BattleLogResult.UnsupportedEffect,
                BattleActionOutcome.NoTarget => BattleLogResult.NoTarget,
                _ => throw new ArgumentOutOfRangeException()
            };
            int round = result.Round > 0 ? result.Round : turnSource != null ? turnSource.CurrentRound : 0;
            destination.Record(new BattleLogEntry(kind, round, result.ActorId, result.CardId, outcome,
                result.From, result.To, result.Value, result.TargetId));
        }
    }
}
