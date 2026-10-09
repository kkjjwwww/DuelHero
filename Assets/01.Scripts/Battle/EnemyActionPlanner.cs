using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;
namespace DuelHero.Battle
{
    // Pure planning: does not access the player's reservations or mutate live units/cards.
    public sealed class EnemyActionPlanner
    {
        public CardInstance[] CreatePlan(IReadOnlyList<CardInstance> cards, EnemyPlanningState state, Vector2Int target, int round)
        {
            var selected = new HashSet<CardInstance>();
            var plan = new CardInstance[3];
            // Work on a copy, so even the supplied planning snapshot stays unchanged.
            state = new EnemyPlanningState(state.Position, state.Energy, state.MaxEnergy, state.Width, state.Height);
            for (int slot = 0; slot < 3; slot++)
            {
                var available = cards.Where(c => c.CanReserve(round) && !selected.Contains(c)).ToArray();
                var affordable = available.Where(c => c.Definition.energyCost <= state.Energy).ToArray();
                var choice = affordable.Where(c => Hits(c.Definition, state.Predict(c.Definition).Position, target))
                    .OrderBy(c => c.Definition.energyCost).FirstOrDefault();
                bool needsEnergy = available.Any(c => c.Definition.effects.Any(e => e.effectType == "damage") && c.Definition.energyCost > state.Energy);
                if (choice == null && needsEnergy) choice = affordable.FirstOrDefault(c => Has(c, "energyRestore"));
                if (choice == null)
                {
                    int currentDistance = Distance(state.Position, target);
                    choice = affordable.Where(c => Has(c, "move") && Distance(state.Predict(c.Definition).Position, target) < currentDistance)
                        .OrderBy(c => Distance(state.Predict(c.Definition).Position, target)).FirstOrDefault();
                }
                if (choice == null) choice = affordable.FirstOrDefault(c => Has(c, "guard"));
                if (choice == null && state.Energy < state.MaxEnergy) choice = affordable.FirstOrDefault(c => Has(c, "energyRestore"));
                if (choice == null) choice = affordable.Where(c => Has(c, "move") && state.Predict(c.Definition).Position != state.Position)
                    .OrderBy(c => Distance(state.Predict(c.Definition).Position, target)).FirstOrDefault();
                if (choice == null) choice = affordable.FirstOrDefault(c => Has(c, "energyRestore"));
                if (choice == null) throw new InvalidOperationException("적이 3슬롯을 채울 수 없습니다. 기본 행동 카드 구성을 확인해주세요.");
                selected.Add(choice); plan[slot] = choice; state.Apply(choice.Definition);
            }
            return plan;
        }
        private static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        private static bool Has(CardInstance card, string type) => card.Definition.effects.Any(e => e.effectType == type);
        private static bool Hits(CardDefinition card, Vector2Int origin, Vector2Int target) =>
            card.effects.Any(e => e.effectType == "damage" && AttackRangeCalculator.Calculate(origin, e.rangeOffsets, true).Contains(target));
    }
}
