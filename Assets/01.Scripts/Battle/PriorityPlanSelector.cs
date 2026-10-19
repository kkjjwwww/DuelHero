using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;
using static DuelHero.Battle.EnemyPlanningRules;

namespace DuelHero.Battle
{
    public sealed class PriorityPlanSelector : IEnemyPlanSelector<EnemyTacticState>
    {
        public EnemyPlannedAction[] Select(EnemyDecisionContext context, EnemyTacticState tactic)
        {
            var cards = context.Cards;
            var round = context.Round;
            var target = context.Target;
            var initial = context.State;
            var state = new EnemyPlanningState(initial.Position, initial.Energy, initial.MaxEnergy, initial.Width, initial.Height);
            var selected = new HashSet<CardInstance>();
            var plan = new EnemyPlannedAction[3];
            for (int slot = 0; slot < 3; slot++)
            {
                // 재사용 대기 중이거나 앞 슬롯에서 선택한 카드 인스턴스는 제외한다.
                var available = cards.Where(c => c.CanReserve(round) && !selected.Contains(c)).ToArray();
                // 앞 슬롯의 소비·회복까지 반영한 예상 에너지로 비용을 감당할 수 있어야 한다.
                var affordable = available.Where(c => c.Definition.energyCost <= state.Energy).ToArray();
                // 회복 상태는 최저 공격 비용이 부족한 동안 회복을 우선한다.
                CardInstance choice = tactic == EnemyTacticState.Recover && NeedsEnergy(available, state.Energy)
                    ? affordable.FirstOrDefault(c => Has(c, "energyRestore")) : null;
                // 접근·교전의 공통 규칙. 회복 후에도 가상 위치·에너지로 공격 가능 여부를 다시 검사한다.
                if (choice == null) choice = affordable.Where(c => Hits(c.Definition, state.Predict(c.Definition).Position, target))
                    .OrderBy(c => c.Definition.energyCost).FirstOrDefault();
                // 접근 후 사거리에 들어오면 다음 슬롯에서 공격할 수 있다.
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
                selected.Add(choice); plan[slot] = new EnemyPlannedAction(choice, Vector2Int.left); state.Apply(choice.Definition);
            }
            return plan;
        }
    }
}

