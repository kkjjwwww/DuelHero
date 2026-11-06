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
        private PlayerDeck targetDeck;
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
            var profile = stats != null ? stats.GetComponent<UnitInitializer>()?.Encounter : null;
            var ids = cardIds.AsEnumerable();
            if (profile != null)
            {
                var run = RunSessionStore.Current;
                uint seed = run?.PendingBattleNodeId != null ? run.Map.Seed : (standaloneSeed ??= RouteMapGenerator.CreateSeed());
                var random = new EnemyDecisionRandom(EnemyDecisionRandom.CreateSeed(seed,
                    run?.PendingBattleNodeId, profile.unitId, 0));
                ids = ids.Where(id => database.cards.Any(c => c.id == id && c.isBasicAction))
                    .Concat(profile.fixedCards)
                    .Append(profile.bonusCandidates[random.Next(profile.bonusCandidates.Length)]);
            }
            cards = ids.Select(id => new CardInstance(database.cards.FirstOrDefault(c => c.id == id)
                ?? throw new InvalidOperationException("적 AI 카드 ID 오류: " + id))).ToArray();
        }
        public System.Collections.Generic.IReadOnlyList<CardDefinition> CreatePlan(int round)
        {
            if (activePlan != null && activeRound == round) return activeExecution;
            var context = CreateContext(round);
            selector ??= new UtilityPlanSelector(utilitySettings ??= new UtilityAiSettings());
            SetPlan(context, selector.Select(context));
            return activeExecution;
        }

        private EnemyDecisionContext CreateContext(int round)
        {
            if (database == null || stats == null || !stats.IsInitialized || movement == null || target == null || effects == null)
                throw new InvalidOperationException("적 AI의 유닛·카드·효과 실행기를 연결해주세요.");
            InitializeCards();
            var targetStats = target.GetComponent<UnitStats>();
            if (targetStats == null || !targetStats.IsInitialized)
                throw new InvalidOperationException("적 AI 대상의 공개 상태를 확인해주세요.");
            if (targetDeck == null)
            {
                targetDeck = target.GetComponent<PlayerDeck>();
                if (targetDeck == null)
                    foreach (var queue in FindObjectsByType<CardReservationQueue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (queue.UnitStats != targetStats) continue;
                        var deck = queue.GetComponent<PlayerDeck>();
                        if (deck == null) continue;
                        if (targetDeck != null && targetDeck != deck)
                            throw new InvalidOperationException("적 AI 대상에 연결된 PlayerDeck이 여러 개입니다.");
                        targetDeck = deck;
                    }
            }
            if (targetDeck == null || targetDeck.Database == null)
                throw new InvalidOperationException("적 AI 대상의 PlayerDeck·CardDatabase 연결을 확인해주세요.");
            targetDeck.Initialize();
            var supported = cards.Where(c => effects.Supports(actorId, c.Definition)).ToArray();
            var run = RunSessionStore.Current;
            bool runBattle = run?.PendingBattleNodeId != null;
            uint seed = runBattle ? run.Map.Seed : (standaloneSeed ??= RouteMapGenerator.CreateSeed());
            LastDecisionSeed = EnemyDecisionRandom.CreateSeed(seed, runBattle ? run.PendingBattleNodeId : null, actorId, round);
            return new EnemyDecisionContext(supported,
                new EnemyPlanningState(movement.GridPosition, stats.Energy, stats.MaxEnergy, movement.Grid.Map),
                target.GridPosition, round,
                new EnemyUnitSnapshot(stats.Health, stats.MaxHealth, stats.Energy, stats.MaxEnergy, stats.CaptureGuard(), stats.Stun, stats.Bleed),
                new EnemyUnitSnapshot(targetStats.Health, targetStats.MaxHealth, targetStats.Energy, targetStats.MaxEnergy, targetStats.CaptureGuard(), targetStats.Stun, targetStats.Bleed),
                LastDecisionSeed, targetDeck.Cards);
        }

        private void SetPlan(EnemyDecisionContext context, EnemyPlannedAction[] plan)
        {
            var simulation = EnemyPlanningState.SimulatePlan(context, plan);
            var execution = Array.AsReadOnly(plan.Select(action => context.GetPreparedAction(action).ExecutionCard).ToArray());
            activePlan = plan;
            activeExecution = execution;
            LastSimulation = simulation;
            activeRound = context.Round;
        }

#if UNITY_EDITOR
        internal void SetPlanForDebug(int round, int slot, CardInstance card, Vector2Int direction)
        {
            if (activePlan == null || activeRound != round || slot < 0 || slot >= activePlan.Length ||
                slot < stats.Stun || StunCard.IsStun(activePlan[slot].Card.Definition))
                throw new InvalidOperationException("현재 슬롯을 변경할 수 없습니다.");
            var plan = (EnemyPlannedAction[])activePlan.Clone();
            plan[slot] = new EnemyPlannedAction(card ?? new CardInstance(WaitCard.Definition), direction);
            SetPlan(CreateContext(round), plan);
            selector = null;
        }
#endif
        public void CancelSlot(int slot)
        {
            if (activePlan == null || slot < 0 || slot >= activePlan.Length) return;
            activePlan[slot] = new EnemyPlannedAction(new CardInstance(StunCard.Definition), Vector2Int.right);
        }
        public void CompletePlan(int round, int executedSlots)
        {
            if (activePlan == null) return;
            for (int i = 0; i < Math.Min(executedSlots, activePlan.Length); i++)
                if (!StunCard.IsStun(activePlan[i].Card.Definition)) activePlan[i].Card.MarkUsed(round);
            activePlan = null;
            activeExecution = null;
            CardsChanged?.Invoke();
        }
    }
}

