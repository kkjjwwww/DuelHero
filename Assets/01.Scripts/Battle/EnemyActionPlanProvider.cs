using System;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;
namespace DuelHero.Battle
{
    public sealed class EnemyActionPlanProvider : MonoBehaviour, IEnemyActionPlan
    {
        [SerializeField] private CardDatabase database;
        [SerializeField] private UnitStats stats;
        [SerializeField] private GridMovement movement;
        [SerializeField] private GridMovement target;
        [SerializeField] private CardEffectExecutor effects;
        [SerializeField] private string actorId;
        [SerializeField] private string[] cardIds = { "move_up_001", "move_down_001", "move_left_001", "move_right_001", "guard_001", "breathing_001", "slash_001", "thrust_001", "sweep_001" };
        private CardInstance[] cards;
        private CardInstance[] activePlan;
        private readonly EnemyActionPlanner planner = new();
        public event Action CardsChanged;
        public CardDatabase Database => database;
        public System.Collections.Generic.IReadOnlyList<CardInstance> Cards
        {
            get { InitializeCards(); return Array.AsReadOnly(cards); }
        }
        private void InitializeCards()
        {
            if (cards != null) return;
            if (database == null) throw new InvalidOperationException("적 카드 데이터베이스를 연결해주세요.");
            cards = cardIds.Select(id => new CardInstance(database.cards.FirstOrDefault(c => c.id == id)
                ?? throw new InvalidOperationException("적 AI 카드 ID 오류: " + id))).ToArray();
        }
        public System.Collections.Generic.IReadOnlyList<CardDefinition> CreatePlan(int round)
        {
            if (database == null || stats == null || !stats.IsInitialized || movement == null || target == null || effects == null)
                throw new InvalidOperationException("적 AI의 유닛·카드·효과 실행기를 연결해주세요.");
            InitializeCards();
            var supported = cards.Where(c => effects.Supports(actorId, c.Definition)).ToArray();
            activePlan = planner.CreatePlan(supported, new EnemyPlanningState(movement.GridPosition, stats.Energy, stats.MaxEnergy, movement.Width, movement.Height), target.GridPosition, round);
            return activePlan.Select(c => CardDirectionRules.ForExecution(c.Definition, Vector2Int.left)).ToArray();
        }
        public void CompletePlan(int round, int executedSlots)
        {
            if (activePlan == null) return;
            for (int i = 0; i < Math.Min(executedSlots, activePlan.Length); i++) activePlan[i].MarkUsed(round);
            activePlan = null;
            CardsChanged?.Invoke();
        }
    }
}

