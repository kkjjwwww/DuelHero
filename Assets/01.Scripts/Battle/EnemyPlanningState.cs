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
        public int Width { get; }
        public int Height { get; }
        public EnemyPlanningState(Vector2Int position, int energy, int maxEnergy, int width, int height)
        { Position = position; Energy = energy; MaxEnergy = maxEnergy; Width = width; Height = height; }
        public EnemyPlanningState Predict(CardDefinition card)
        {
            var next = new EnemyPlanningState(Position, Energy, MaxEnergy, Width, Height);
            next.Apply(card); return next;
        }
        public void Apply(CardDefinition card)
        {
            if (card.energyCost < 0 || card.energyCost > Energy) throw new InvalidOperationException("적 AI 카드 비용이 부족합니다.");
            Energy -= card.energyCost;
            foreach (var effect in card.effects.OrderBy(e => e.resolutionOrder))
            {
                if (effect.effectType == "move" && CardEffectExecutor.TryDirection(effect.rangeOffsets, out var direction))
                    Position = GridRules.Move(Position, direction, effect.value, Width, Height);
                else if (effect.effectType == "energyRestore") Energy += Math.Min(effect.value, MaxEnergy - Energy);
            }
        }
    }
}

