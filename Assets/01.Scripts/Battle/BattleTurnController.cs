using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using UnityEngine;
namespace DuelHero.Battle
{
    public enum BattleOutcome { Running, Victory, Defeat, Draw }
    public sealed class BattleTurnController : MonoBehaviour, IBattleActionSource
    {
        [SerializeField] private CardReservationQueue playerQueue;
        [SerializeField] private string playerId;
        [SerializeField] private string enemyId;
        [SerializeField] private MonoBehaviour enemyPlanSource;
        [SerializeField] private CardEffectExecutor effects;
        [SerializeField] private CombatEffectResolver combatEffects;
        [SerializeField] private BattlePresentation presentation;
        public bool IsInputLocked => (playerQueue != null && playerQueue.IsExecuting) ||
            (presentation != null && presentation.IsActive);
        [SerializeField, Min(0.01f)] private float slotDelay = 0.4f;
        [SerializeField, Min(0f)] private float enemyCardRevealDuration = 0.4f;
        [SerializeField, Min(0f)] private float postRevealDelay = 0.5f;
        [SerializeField, Min(0f)] private float cardPresentationDuration = 0.12f;
        public float CardPresentationDuration => cardPresentationDuration;
        public float EnemyCardRevealDuration => enemyCardRevealDuration;
        public string LastError { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public event Action<BattleActionResult> ActionResolved;
        public event Action<BattleOutcome> BattleFinished;
        public event Action<IReadOnlyList<CardDefinition>> EnemyPlanPrepared;
        public event Action<int> SlotStarted;
        public event Action<int> SlotPresenting;
        public event Action<int> SlotRevealing;
        public event Action<int> SlotCompleted;
        public event Action PhaseChanged;
        public bool ResultsAvailable { get; private set; }
        public bool AwaitingNextTurn => ResultsAvailable && Outcome == BattleOutcome.Running;
        private CardDefinition[] preparedEnemyCards;
        public IReadOnlyList<CardDefinition> PreparedEnemyCards =>
            preparedEnemyCards == null ? Array.Empty<CardDefinition>() : Array.AsReadOnly(preparedEnemyCards);
        public int CompletedSlots => executedSlots;
        private IEnemyActionPlan enemyPlan;
        private int executedSlots;
        private int executionRound;
        private bool restoring;
        private void Start()
        {
            if (playerQueue == null) return;
            PrepareEnemyPlan();
            Publish(BattleActionKind.TurnStarted);
        }
        private bool PrepareEnemyPlan()
        {
            preparedEnemyCards = null;
            if (!(enemyPlanSource is IEnemyActionPlan provider) || !enemyPlanSource.isActiveAndEnabled)
            { LastError = "적 행동 계획을 연결해주세요."; return false; }
            try
            {
                var cards = provider.CreatePlan(playerQueue.CurrentRound).ToArray();
                if (cards.Length != 3) throw new InvalidOperationException("적 행동 계획은 세 슬롯이어야 합니다.");
                enemyPlan = provider;
                preparedEnemyCards = cards;
                executedSlots = 0;
                EnemyPlanPrepared?.Invoke(PreparedEnemyCards);
                return true;
            }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        private void Publish(BattleActionKind kind, string actorId = null, string cardId = null,
            BattleActionOutcome outcome = BattleActionOutcome.Success) =>
            ActionResolved?.Invoke(new BattleActionResult(kind, actorId ?? playerId, playerQueue.CurrentRound, cardId, outcome));
        public bool TryExecute()
        {
            if (!isActiveAndEnabled || playerQueue == null || playerQueue.IsExecuting ||
                playerQueue.Reservations.Count != 3 || Outcome != BattleOutcome.Running)
                return false;

            if (effects == null || combatEffects == null ||
                !combatEffects.CanResolve(playerId) || !combatEffects.CanResolve(enemyId) ||
                !(enemyPlanSource is IEnemyActionPlan provider) || !enemyPlanSource.isActiveAndEnabled)
            {
                LastError = "양측 유닛과 실행기, 적 행동 계획을 연결해주세요.";
                return false;
            }

            if (!playerQueue.HasEnoughEnergy(out var reason))
            {
                LastError = reason;
                return false;
            }

            var playerCards = playerQueue.Reservations.Select(c => CardDirectionRules.ForExecution(c.Definition,c.SelectedDirection)).ToArray();

            if (preparedEnemyCards == null && !PrepareEnemyPlan())
                return false;

            var enemyCards = preparedEnemyCards;
            if (enemyCards.Length != 3 || playerCards.Any(c => !effects.Supports(playerId, c)) || enemyCards.Any(c => !effects.Supports(enemyId, c)))
            {
                LastError = "행동 계획에 지원하지 않는 효과가 있습니다.";
                Publish(BattleActionKind.ExecutionBlocked,outcome: BattleActionOutcome.UnsupportedEffect);
                return false;
            }

            combatEffects.TryGetUnit(enemyId, out _, out var enemyStats);
            var energyCards = enemyCards
                .Select(c => new CardInstance(c ?? new CardDefinition
                {
                    effects = Array.Empty<CardEffectDefinition>()
                }))
                .ToArray();

            if (!CardEnergyRules.CanExecute(
                energyCards, enemyStats.Energy, enemyStats.MaxEnergy, out reason))
            {
                LastError = "적 " + reason;
                return false;
            }

            if (!playerQueue.BeginExecution())
                return false;

            enemyPlan = provider;
            LastError = null;
            executedSlots = 0;
            executionRound = playerQueue.CurrentRound;

            StartCoroutine(Execute(playerCards, enemyCards));
            return true;
        }
        private IEnumerator Execute(CardDefinition[] playerCards, CardDefinition[] enemyCards)
        {
            if (presentation != null) yield return presentation.Animate(true);
            combatEffects.TryGetUnit(playerId, out _, out var playerStats);
            combatEffects.TryGetUnit(enemyId, out _, out var enemyStats);
            int round = executionRound;
            for (int slot = 0; slot < 3; slot++)
            {
                playerQueue.SetActiveSlot(slot);
                if (playerStats.Health <= 0 || enemyStats.Health <= 0)
                { Outcome = EvaluateOutcome(playerStats.Health, enemyStats.Health); break; }
                var pc = playerCards[slot]; var ec = enemyCards[slot];
                if (playerStats.Energy < (pc?.energyCost ?? 0) || enemyStats.Energy < (ec?.energyCost ?? 0))
                { LastError = "실행 중 에너지가 부족해졌습니다."; FinishExecution(); yield break; }
                SlotPresenting?.Invoke(slot);
                if (cardPresentationDuration > 0f) yield return new WaitForSeconds(cardPresentationDuration);
                SlotRevealing?.Invoke(slot);
                if (enemyCardRevealDuration > 0f) yield return new WaitForSeconds(enemyCardRevealDuration);
                if (postRevealDelay > 0f) yield return new WaitForSeconds(postRevealDelay);
                SlotStarted?.Invoke(slot);
                playerStats.TrySpendEnergy(pc?.energyCost ?? 0); enemyStats.TrySpendEnergy(ec?.energyCost ?? 0);
                if (pc != null) Publish(BattleActionKind.CardExecuted, playerId, pc.id);
                if (ec != null) Publish(BattleActionKind.CardExecuted, enemyId, ec.id);
                effects.ExecutePreparation(playerId, pc, round);
                effects.ExecutePreparation(enemyId, ec, round);

                var attacks = new List<CombatEffectResolver.AttackRequest>();
                AddAttacks(attacks, playerId, pc);
                AddAttacks(attacks, enemyId, ec);

                var damage = combatEffects.CalculateDamageBatch(attacks, round);
                combatEffects.ApplyDamageBatch(damage);
                
                combatEffects.EndSlot(round);
                executedSlots++;
                Outcome = EvaluateOutcome(playerStats.Health, enemyStats.Health);
                // Keep the executed cards visible while the result is being shown.
                yield return new WaitForSeconds(slotDelay);
                SlotCompleted?.Invoke(slot);
                if (Outcome != BattleOutcome.Running) break;
            }
            FinishExecution();
        }
        private void FinishExecution()
        {
            if (playerQueue == null || !playerQueue.IsExecuting || ResultsAvailable || restoring) return;
            enemyPlan?.CompletePlan(executionRound, executedSlots);
            Publish(BattleActionKind.TurnEnded);
            if (Outcome != BattleOutcome.Running) Publish(BattleActionKind.BattleEnded);
            ResultsAvailable = true;
            playerQueue.SetActiveSlot(-1);
            if (Outcome != BattleOutcome.Running)
            {
                playerQueue.CompleteExecution(executedSlots);
                BattleFinished?.Invoke(Outcome);
            }
            PhaseChanged?.Invoke();
        }
        public bool TryAdvanceTurn()
        {
            if (!isActiveAndEnabled || !AwaitingNextTurn) return false;
            restoring = true;
            ResultsAvailable = false;
            PhaseChanged?.Invoke();
            StartCoroutine(AdvanceTurn());
            return true;
        }
        private IEnumerator AdvanceTurn()
        {
            if (presentation != null) yield return presentation.Animate(false);
            restoring = false;
            playerQueue.CompleteExecution(executedSlots);
            if (PrepareEnemyPlan()) Publish(BattleActionKind.TurnStarted);
            PhaseChanged?.Invoke();
        }
        private static void AddAttacks(List<CombatEffectResolver.AttackRequest> requests, string actor, CardDefinition card)
        {
            if (card == null) return;
            foreach (var effect in card.effects.OrderBy(e => e.resolutionOrder))
            {
                if (effect.effectType == "damage") requests.Add(new CombatEffectResolver.AttackRequest(actor, card.id, effect));

            }
        }
        public static BattleOutcome EvaluateOutcome(int playerHealth, int enemyHealth) => playerHealth <= 0
            ? BattleOutcome.Defeat
            : enemyHealth <= 0 ? BattleOutcome.Victory : BattleOutcome.Running;
        private void OnDisable()
        {
            StopAllCoroutines();
            FinishExecution();
        }
    }
}
