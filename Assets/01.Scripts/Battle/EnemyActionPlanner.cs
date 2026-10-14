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
                // 재사용 대기 중이거나 앞 슬롯에서 선택한 카드 인스턴스는 제외한다.
                var available = cards.Where(c => c.CanReserve(round) && !selected.Contains(c)).ToArray();
                // 앞 슬롯의 소비·회복까지 반영한 예상 에너지로 비용을 감당할 수 있어야 한다.
                var affordable = available.Where(c => c.Definition.energyCost <= state.Energy).ToArray();
                // 1순위: 카드의 이동 효과까지 예측한 위치에서 명중 가능한 공격 중 비용이 가장 낮은 카드.
                var choice = affordable.Where(c => Hits(c.Definition, state.Predict(c.Definition).Position, target))
                    .OrderBy(c => c.Definition.energyCost).FirstOrDefault();
                // 사거리와 무관하게, 현재 예상 에너지보다 비싼 공격이 하나라도 있는지 검사한다.
                bool needsEnergy = available.Any(c => c.Definition.effects.Any(e => e.effectType == "damage") && c.Definition.energyCost > state.Energy);
                // 이후 조건은 choice == null일 때만 검사하므로 앞에서 선택한 카드를 덮어쓰지 않는다.
                // 2순위: 비싼 공격을 위한 에너지 회복.
                if (choice == null && needsEnergy) choice = affordable.FirstOrDefault(c => Has(c, "energyRestore"));
                // 3순위: 플레이어와의 거리를 줄이는 이동 중 도착 후 거리가 가장 짧은 카드.
                if (choice == null)
                {
                    int currentDistance = Distance(state.Position, target);
                    choice = affordable.Where(c => Has(c, "move") && Distance(state.Predict(c.Definition).Position, target) < currentDistance)
                        .OrderBy(c => Distance(state.Predict(c.Definition).Position, target)).FirstOrDefault();
                }
                // 4순위: 방어. 별도 정렬이 없는 후보는 입력 카드 목록의 순서대로 선택한다.
                if (choice == null) choice = affordable.FirstOrDefault(c => Has(c, "guard"));
                // 5순위: 에너지가 최대가 아니라면 회복.
                if (choice == null && state.Energy < state.MaxEnergy) choice = affordable.FirstOrDefault(c => Has(c, "energyRestore"));
                // 6순위: 거리를 줄이지 못하더라도 실제 위치가 바뀌는 이동 중 가장 가까운 도착지.
                if (choice == null) choice = affordable.Where(c => Has(c, "move") && state.Predict(c.Definition).Position != state.Position)
                    .OrderBy(c => Distance(state.Predict(c.Definition).Position, target)).FirstOrDefault();
                // 7순위: 최대 에너지 여부와 무관하게 회복 카드로 남은 슬롯을 채운다.
                if (choice == null) choice = affordable.FirstOrDefault(c => Has(c, "energyRestore"));
                // 후보가 없으면 불완전한 계획 대신 구성 오류를 알린다.
                if (choice == null) throw new InvalidOperationException("적이 3슬롯을 채울 수 없습니다. 기본 행동 카드 구성을 확인해주세요.");
                // 현재 슬롯을 확정하고 가상 위치·에너지를 갱신한 뒤 다음 슬롯을 고른다.
                // 세 슬롯의 모든 조합을 탐색하지 않으며, 플레이어 위치는 계획 시점의 target을 사용한다.
                selected.Add(choice); plan[slot] = choice; state.Apply(choice.Definition);
            }
            return plan;
        }
        private static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        private static bool Has(CardInstance card, string type) => card.Definition.effects.Any(e => e.effectType == type);
        private static bool Hits(CardDefinition card, Vector2Int origin, Vector2Int target) =>
            CardDirectionRules.ForExecution(card, Vector2Int.left).effects.Any(e => e.effectType == "damage" && AttackRangeCalculator.Calculate(origin, e.rangeOffsets).Contains(target));
    }
}
