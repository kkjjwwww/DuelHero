using System;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Units;
using DuelHero.Run;
using DuelHero.Map;
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
        [SerializeField] private UtilityAiSettings utilitySettings = new UtilityAiSettings();
        [SerializeField] private string[] cardIds = { "move_up_001", "move_down_001", "move_left_001", "move_right_001", "guard_001", "breathing_001", "slash_001", "thrust_001", "sweep_001" };
        private CardInstance[] cards;
        private EnemyPlannedAction[] activePlan;
        private System.Collections.Generic.IReadOnlyList<CardDefinition> activeExecution;
        private int activeRound;
        private uint? standaloneSeed;
        public uint LastDecisionSeed { get; private set; }
        public EnemyPlanSimulation LastSimulation { get; private set; }
        private UtilityPlanSelector selector;
        public EnemySearchMetrics LastSearch => selector?.LastSearch ?? default;
        public EnemyDecisionTrace LastDecisionTrace => selector?.LastTrace;
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
            if (activePlan != null && activeRound == round) return activeExecution;
            if (database == null || stats == null || !stats.IsInitialized || movement == null || target == null || effects == null)
                throw new InvalidOperationException("적 AI의 유닛·카드·효과 실행기를 연결해주세요.");
            InitializeCards();
            var targetStats = target.GetComponent<UnitStats>();
            if (targetStats == null || !targetStats.IsInitialized)
                throw new InvalidOperationException("적 AI 대상의 공개 상태를 확인해주세요.");
            var supported = cards.Where(c => effects.Supports(actorId, c.Definition)).ToArray();
            var run = RunSessionStore.Current;
            bool runBattle = run?.PendingBattleNodeId != null;
            uint seed = runBattle ? run.Map.Seed : (standaloneSeed ??= RouteMapGenerator.CreateSeed());
            LastDecisionSeed = EnemyDecisionRandom.CreateSeed(seed, runBattle ? run.PendingBattleNodeId : null, actorId, round);
            var context = new EnemyDecisionContext(supported,
                new EnemyPlanningState(movement.GridPosition, stats.Energy, stats.MaxEnergy, movement.Grid.Map),
                target.GridPosition, round,
                new EnemyUnitSnapshot(stats.Health, stats.MaxHealth, stats.Energy, stats.MaxEnergy, stats.CaptureGuard()),
                new EnemyUnitSnapshot(targetStats.Health, targetStats.MaxHealth, targetStats.Energy, targetStats.MaxEnergy, targetStats.CaptureGuard()),
                LastDecisionSeed);
            selector ??= new UtilityPlanSelector(utilitySettings ??= new UtilityAiSettings());
            activePlan = selector.Select(context);
            LastSimulation = EnemyPlanningState.SimulatePlan(context, activePlan);
            activeExecution = Array.AsReadOnly(activePlan.Select(action => context.GetPreparedAction(action).ExecutionCard).ToArray());
            activeRound = round;
            return activeExecution;
        }
        public void CompletePlan(int round, int executedSlots)
        {
            if (activePlan == null) return;
            for (int i = 0; i < Math.Min(executedSlots, activePlan.Length); i++) activePlan[i].Card.MarkUsed(round);
            activePlan = null;
            activeExecution = null;
            CardsChanged?.Invoke();
        }
    }
}

