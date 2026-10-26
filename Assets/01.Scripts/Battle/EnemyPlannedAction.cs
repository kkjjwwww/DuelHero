using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    public readonly struct EnemyPlannedAction
    {
        public CardInstance Card { get; }
        public Vector2Int Direction { get; }

        public EnemyPlannedAction(CardInstance card, Vector2Int direction)
        { Card = card; Direction = direction; }
    }
}

