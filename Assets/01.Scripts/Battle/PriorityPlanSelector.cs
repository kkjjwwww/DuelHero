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
            var initial = context.State;
            var state = new EnemyPlanningState(initial.Position, initial.Energy, initial.MaxEnergy, initial.Map);
            var distances = context.DistancesToTarget;
            int DistanceToTarget(Vector2Int position) => distances.TryGetValue(position, out int distance) ? distance : int.MaxValue;
            var selected = new HashSet<CardInstance>();
            var plan = new EnemyPlannedAction[3];
            for (int slot = 0; slot < 3; slot++)
            {
                // 재사용 대기 중이거나 앞 슬롯에서 선택한 카드 인스턴스는 제외한다.
                var available = cards.Where(c => c.CanReserve(round) && !selected.Contains(c)).ToArray();
                // 앞 슬롯의 소비·회복까지 반영한 예상 에너지로 비용을 감당할 수 있어야 한다.
                var affordable = available.Where(c => c.Definition.energyCost <= state.Energy).ToArray();
                var candidates = affordable.SelectMany(Actions).ToArray();
                // 회복 상태는 최저 공격 비용이 부족한 동안 회복을 우선한다.
                EnemyPlannedAction choice = tactic == EnemyTacticState.Recover && NeedsEnergy(available, state.Energy)
                    ? candidates.FirstOrDefault(a => Has(a.Card, "energyRestore")) : default;
                // 접근·교전의 공통 규칙. 회복 후에도 가상 위치·에너지로 공격 가능 여부를 다시 검사한다.
                if (choice.Card == null) choice = candidates.Where(a => Hits(a, state, context))
                    .OrderBy(a => a.Card.Definition.energyCost).FirstOrDefault();
                // 접근 후 사거리에 들어오면 다음 슬롯에서 공격할 수 있다.
                if (choice.Card == null)
                {
                    int currentDistance = DistanceToTarget(state.Position);
                    choice = candidates.Where(a => Has(a.Card, "move") && DistanceToTarget(state.Predict(context.GetPreparedAction(a)).Position) < currentDistance)
                        .OrderBy(a => DistanceToTarget(state.Predict(context.GetPreparedAction(a)).Position)).FirstOrDefault();
                }
                // 전술 선택 이후에는 남은 기본 카드로 보충한다.
                var basic = candidates.Where(a => a.Card.Definition.isBasicAction).ToArray();
                if (choice.Card == null) choice = basic.FirstOrDefault(a => Has(a.Card, "guard"));
                // 5순위: 에너지가 최대가 아니라면 회복.
                if (choice.Card == null && state.Energy < state.MaxEnergy) choice = basic.FirstOrDefault(a => Has(a.Card, "energyRestore"));
                // 6순위: 이동 가능한 후보 우선. 모두 경계에 막혀도 남은 이동 카드는 사용한다.
                if (choice.Card == null) choice = basic.Where(a => Has(a.Card, "move"))
                    .OrderByDescending(a => state.Predict(context.GetPreparedAction(a)).Position != state.Position)
                    .ThenBy(a => DistanceToTarget(state.Predict(context.GetPreparedAction(a)).Position)).FirstOrDefault();
                // 7순위: 최대 에너지 여부와 무관하게 회복 카드로 남은 슬롯을 채운다.
                if (choice.Card == null) choice = basic.FirstOrDefault(a => Has(a.Card, "energyRestore"));
                // 기본 카드 외의 사용 가능한 카드도 모두 소진한 뒤에만 대기한다.
                if (choice.Card == null) choice = candidates.FirstOrDefault();
                if (choice.Card == null) choice = new EnemyPlannedAction(new CardInstance(WaitCard.Definition), Vector2Int.right);
                // 현재 슬롯을 확정하고 가상 위치·에너지를 갱신한 뒤 다음 슬롯을 고른다.
                // 세 슬롯의 모든 조합을 탐색하지 않으며, 플레이어 위치는 계획 시점의 target을 사용한다.
                selected.Add(choice.Card); plan[slot] = choice; state.Apply(context.GetPreparedAction(choice));
            }
            return plan;
        }
    }
}

