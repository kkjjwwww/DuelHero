using System.Collections.Generic;
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

        public EnemyDecisionContext(IReadOnlyList<CardInstance> cards, EnemyPlanningState state, Vector2Int target, int round)
        { Cards = cards; State = state; Target = target; Round = round; }
    }
}

