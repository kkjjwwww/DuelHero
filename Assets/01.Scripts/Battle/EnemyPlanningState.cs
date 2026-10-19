using System;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;
namespace DuelHero.Battle
{
    public sealed class EnemyPlanningState
    {
        public Vector2Int Position { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; }
        public GridMap Map { get; }
        public EnemyPlanningState(Vector2Int position, int energy, int maxEnergy, GridMap map)
        { Position = position; Energy = energy; MaxEnergy = maxEnergy; Map = map; }
        public EnemyPlanningState Predict(CardDefinition card, Vector2Int direction)
        {
            var next = new EnemyPlanningState(Position, Energy, MaxEnergy, Map);
            next.Apply(card, direction); return next;
        }
        public void Apply(CardDefinition card, Vector2Int direction)
        {
            card = CardDirectionRules.ForExecution(card, direction);
            if (card.energyCost < 0 || card.energyCost > Energy) throw new InvalidOperationException("적 AI 카드 비용이 부족합니다.");
            Energy -= card.energyCost;
            foreach (var effect in card.effects.OrderBy(e => e.resolutionOrder))
            {
                if (effect.effectType == "move" && CardEffectExecutor.TryDirection(effect.rangeOffsets, out var moveDirection))
                    Position = Map.Move(Position, moveDirection, effect.value);
                else if (effect.effectType == "energyRestore") Energy += Math.Min(effect.value, MaxEnergy - Energy);
            }
        }
    }
}

