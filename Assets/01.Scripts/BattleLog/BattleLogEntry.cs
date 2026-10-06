using UnityEngine;
namespace DuelHero.Logging
{
    public enum BattleLogKind { TurnStarted, TurnEnded, CardExecuted, Movement, ExecutionBlocked }
    public enum BattleLogResult { Success, BoundaryBlocked, UnsupportedEffect }
    public sealed class BattleLogEntry
    {
        public BattleLogKind Kind { get; }
        public BattleLogResult Result { get; }
        public int Round { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string CardId { get; }
        public int Value { get; }
        public Vector2Int From { get; }
        public Vector2Int To { get; }
        public BattleLogEntry(BattleLogKind kind, int round, string actorId = null, string cardId = null,
            BattleLogResult result = BattleLogResult.Success, Vector2Int from = default, Vector2Int to = default,
            int value = 0, string targetId = null)
        {
            Kind = kind; Round = round; ActorId = actorId; CardId = cardId; Result = result;
            From = from; To = to; Value = value; TargetId = targetId;
        }
    }
}
