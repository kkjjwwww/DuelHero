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
        [SerializeField, Min(0.01f)] private float slotDelay = 0.4f;
        public string LastError { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public event Action<BattleActionResult> ActionResolved;
        private IEnemyActionPlan enemyPlan;
        private void Start() { if (playerQueue != null) Publish(BattleActionKind.TurnStarted); }
        private void Publish(BattleActionKind kind, string actorId = null, string cardId = null,
            BattleActionOutcome outcome = BattleActionOutcome.Success) =>
            ActionResolved?.Invoke(new BattleActionResult(kind, actorId ?? playerId, playerQueue.CurrentRound, cardId, outcome));
        public bool TryExecute()
        {
            if (!isActiveAndEnabled || playerQueue == null || playerQueue.IsExecuting || playerQueue.Reservations.Count != 3 || Outcome != BattleOutcome.Running) return false;
            if (effects == null || combatEffects == null || !combatEffects.CanResolve(playerId) || !combatEffects.CanResolve(enemyId) ||
                !(enemyPlanSource is IEnemyActionPlan provider) || !enemyPlanSource.isActiveAndEnabled)
            { LastError = "양측 유닛과 실행기, 적 행동 계획을 연결해주세요."; return false; }
            if (!playerQueue.HasEnoughEnergy(out var reason)) { LastError = reason; return false; }
            var playerCards = playerQueue.Reservations.Select(c => c.Definition).ToArray();
            CardDefinition[] enemyCards;
            try { enemyCards = provider.CreatePlan(playerQueue.CurrentRound).ToArray(); }
            catch (Exception error) { LastError = error.Message; return false; }
            if (enemyCards.Length != 3 || playerCards.Any(c => !effects.Supports(playerId, c)) || enemyCards.Any(c => !effects.Supports(enemyId, c)))
            {
                LastError = "행동 계획에 지원하지 않는 효과가 있습니다.";
                Publish(BattleActionKind.ExecutionBlocked, outcome: BattleActionOutcome.UnsupportedEffect); return false;
            }
            combatEffects.TryGetUnit(enemyId, out _, out var enemyStats);
            var energyCards = enemyCards.Select(c => new CardInstance(c ?? new CardDefinition { effects = Array.Empty<CardEffectDefinition>() })).ToArray();
            if (!CardEnergyRules.CanExecute(energyCards, enemyStats.Energy, enemyStats.MaxEnergy, out reason)) { LastError = "적 " + reason; return false; }
            if (!playerQueue.BeginExecution()) return false;
            enemyPlan = provider; LastError = null;
            StartCoroutine(Execute(playerCards, enemyCards)); return true;
        }
        private IEnumerator Execute(CardDefinition[] playerCards, CardDefinition[] enemyCards)
        {
            combatEffects.TryGetUnit(playerId, out _, out var playerStats);
            combatEffects.TryGetUnit(enemyId, out _, out var enemyStats);
            int round = playerQueue.CurrentRound;
            int executedSlots = 0;
            for (int slot = 0; slot < 3; slot++)
            {
                playerQueue.SetActiveSlot(slot);
                if (playerStats.Health <= 0 || enemyStats.Health <= 0)
                { Outcome = EvaluateOutcome(playerStats.Health, enemyStats.Health); break; }
                var pc = playerCards[slot]; var ec = enemyCards[slot];
                if (playerStats.Energy < (pc?.energyCost ?? 0) || enemyStats.Energy < (ec?.energyCost ?? 0))
                { LastError = "실행 중 에너지가 부족해졌습니다."; enemyPlan.CompletePlan(round, executedSlots); playerQueue.AbortExecution(); yield break; }
                playerStats.TrySpendEnergy(pc?.energyCost ?? 0); enemyStats.TrySpendEnergy(ec?.energyCost ?? 0);
                if (pc != null) Publish(BattleActionKind.CardExecuted, playerId, pc.id);
                if (ec != null) Publish(BattleActionKind.CardExecuted, enemyId, ec.id);
                effects.ExecutePreparation(playerId, pc, round);
                effects.ExecutePreparation(enemyId, ec, round);
                var attacks = new List<CombatEffectResolver.AttackRequest>();
                AddAttacks(attacks, playerId, pc, false);
                AddAttacks(attacks, enemyId, ec, true);
                var damage = combatEffects.CalculateDamageBatch(attacks, round);
                yield return new WaitForSeconds(slotDelay);
                combatEffects.ApplyDamageBatch(damage);
                // Once per shared slot, after BOTH attacks.
                combatEffects.EndSlot(round);
                executedSlots++;
                Outcome = EvaluateOutcome(playerStats.Health, enemyStats.Health);
                if (Outcome != BattleOutcome.Running) break;
            }
            enemyPlan.CompletePlan(round, executedSlots);
            Publish(BattleActionKind.TurnEnded);
            if (Outcome != BattleOutcome.Running) Publish(BattleActionKind.BattleEnded);
            // Notify UI only after final outcome has been determined.
            playerQueue.CompleteExecution(executedSlots);
            if (Outcome == BattleOutcome.Running) Publish(BattleActionKind.TurnStarted);
        }
        private static void AddAttacks(List<CombatEffectResolver.AttackRequest> requests, string actor, CardDefinition card, bool mirror)
        {
            if (card == null) return;
            foreach (var effect in card.effects.OrderBy(e => e.resolutionOrder))
                if (effect.effectType == "damage") requests.Add(new CombatEffectResolver.AttackRequest(actor, card.id, effect, mirror));
        }
        public static BattleOutcome EvaluateOutcome(int playerHealth, int enemyHealth) => playerHealth <= 0
            ? BattleOutcome.Defeat
            : enemyHealth <= 0 ? BattleOutcome.Victory : BattleOutcome.Running;
        private void OnDisable()
        {
            StopAllCoroutines();
            if (playerQueue != null && playerQueue.IsExecuting) playerQueue.AbortExecution();
        }
    }
}
