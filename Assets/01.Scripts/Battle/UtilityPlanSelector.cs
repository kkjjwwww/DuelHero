using System;
using System.Diagnostics;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Battle
{
    public readonly struct EnemySearchMetrics
    {
        public int ActionPreviews { get; }
        public int AppliedActions { get; }
        public int RejectedAttacks { get; }
        public double ElapsedMilliseconds { get; }
        // -1 when the runtime does not expose the allocation counter.
        public long AllocatedBytes { get; }
        internal EnemySearchMetrics(int previews, int applied, long ticks, long bytes, int rejectedAttacks = 0)
        {
            ActionPreviews = previews; AppliedActions = applied;
            RejectedAttacks = rejectedAttacks;
            ElapsedMilliseconds = ticks * 1000d / Stopwatch.Frequency; AllocatedBytes = bytes;
        }
    }

    // Select one action at a time. Only the chosen action advances the shared prediction.
    public sealed class UtilityPlanSelector : IEnemyPlanSelector
    {
        private struct Candidate
        {
            internal EnemyPlannedAction Action;
            internal float Score, Burden;
            internal EnemyActionScore Breakdown;
            internal Vector2Int Position;
            internal int Energy, TargetHealth, RawDamage, TraceIndex;
            internal bool SameOutcome(Candidate other) => Action.Card == other.Action.Card && Position == other.Position &&
                Energy == other.Energy && TargetHealth == other.TargetHealth && RawDamage == other.RawDamage;
        }
        private readonly Candidate[] candidates = new Candidate[2];
        private readonly Vector2Int[] visitedPositions = new Vector2Int[4];
        private readonly UtilityAiSettings settings;
        private readonly EnemyPlanEvaluator evaluator;
        private readonly EnemyPlanningState state = new EnemyPlanningState();
        private readonly EnemyPlanningState preview = new EnemyPlanningState();
        private readonly EnemyPlannedAction wait = new EnemyPlannedAction(new CardInstance(WaitCard.Definition), UnityEngine.Vector2Int.right);
        public EnemySearchMetrics LastSearch { get; private set; }
        public static bool CaptureScores { get; set; }
        public EnemyDecisionTrace LastTrace { get; private set; }
        public EnemyRoundStyle LastStyle { get; private set; }

        public UtilityPlanSelector(UtilityAiSettings settings)
        { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); evaluator = new EnemyPlanEvaluator(settings); }

        public EnemyPlannedAction[] Select(EnemyDecisionContext context)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            settings.Validate();
            var style = new EnemyRoundStyle(context.Style, settings);
            LastStyle = style;
            state.Reset(context);
            visitedPositions[0] = state.Position;
            LastTrace = null;
            var trace = CaptureScores ? new EnemyDecisionTrace(context.Round, context.DecisionSeed, style) : null;
            var random = new EnemyDecisionRandom(context.DecisionSeed);
            var plan = new EnemyPlannedAction[3];
            int previews = 0, applied = 0, rejectedAttacks = 0;
            float attackBurden = 0;
            EnemyActionScore prefix = default;
            for (int slot = 0; slot < plan.Length; slot++)
            {
                if (slot < context.Actor.Stun)
                {
                    plan[slot] = new EnemyPlannedAction(new CardInstance(StunCard.Definition), Vector2Int.right);
                    prefix = AddForcedRisk(context, prefix);
                    state.Step(context, plan[slot], slot);
                    visitedPositions[slot + 1] = state.Position;
                    continue;
                }
                var debugSlot = trace?.BeginSlot(slot, state);
                if (state.TargetHealth <= 0 || context.Actor.Health <= 0)
                {
                    plan[slot] = wait;
                    if (debugSlot != null) debugSlot.Note = "전투 종료로 실행하지 않는 슬롯입니다.";
                    continue;
                }

                int candidateCount = 0;
                context.PrepareAttackDistances(state.Energy, plan, slot);
                for (int cardIndex = 0; cardIndex < context.Cards.Count; cardIndex++)
                {
                    var card = context.Cards[cardIndex];
                    if (WaitCard.IsWait(card.Definition) || !card.CanReserve(context.Round) || Used(plan, card, slot) ||
                        card.Definition.energyCost < 0 || card.Definition.energyCost > state.Energy) continue;
                    for (int direction = 0; direction < EnemyPlanningRules.DirectionCount(card); direction++)
                    {
                        var action = EnemyPlanningRules.Action(card, direction);
                        var prepared = context.GetPreparedAction(action);
                        preview.CopyValuesFrom(state);
                        int rawDamage = preview.PreviewAction(context, prepared, state);
                        if (prepared.HasMovement && preview.Position == state.Position &&
                            Array.TrueForAll(prepared.Effects, effect => effect.effectType == "move"))
                            continue;
                        float burden = attackBurden + (prepared.HasDamage ? evaluator.AttackCostPenalty(prepared.EnergyCost, state.Energy) * style.Preparation : 0);
                        plan[slot] = action;
                        bool revisited = false;
                        for (int i = 0; i <= slot; i++)
                            if (visitedPositions[i] == preview.Position) { revisited = true; break; }
                        var score = evaluator.Evaluate(context, preview, plan, slot + 1, burden, state, prefix, style,
                            revisited, rawDamage);
                        previews++;
                        // A missed attack may still be useful as a preparation card, but
                        // never keep an empty swing merely because other candidates score poorly.
                        bool missedAttack = prepared.HasDamage && !prepared.CanHit(preview.Position, context.Target, preview.MovementPath);
                        bool usefulMovement = preview.Position != state.Position && !score.UnproductiveMove;
                        bool usefulGuard = score.BlockedDamage > Math.Min(score.RiskDamage, state.GuardAmount);
                        if (missedAttack && !usefulMovement && preview.Energy <= state.Energy && !usefulGuard)
                        { rejectedAttacks++; continue; }
                        debugSlot?.Add(cardIndex, card, action.Direction, state, preview, score);
                        Retain(new Candidate { Action = action, Score = score.Total, Burden = burden, Breakdown = score,
                            Position = preview.Position, Energy = preview.Energy, TargetHealth = preview.TargetHealth,
                            RawDamage = rawDamage, TraceIndex = (debugSlot?.Candidates.Count ?? 0) - 1 }, ref candidateCount);
                    }
                }
                var choice = wait;
                if (candidateCount > 0)
                {
                    float best = candidates[0].Score;
                    double probability = candidateCount == 1 ? 1d :
                        SoftmaxBestProbability(best, candidates[1].Score, settings.softmaxTemperature);
                    var chosen = candidates[probability >= 1d || random.NextUnit() < probability ? 0 : 1];
                    choice = chosen.Action; attackBurden = chosen.Burden; prefix = chosen.Breakdown;
                    if (debugSlot != null)
                    {
                        debugSlot.BestScore = best; debugSlot.Temperature = settings.softmaxTemperature; debugSlot.SelectedIndex = chosen.TraceIndex;
                        debugSlot.AddEligible(candidates[0].TraceIndex, probability);
                        if (candidateCount == 2) debugSlot.AddEligible(candidates[1].TraceIndex, 1d - probability);
                    }
                }
                // Wait remains a fallback only when every owned action is unavailable.
                plan[slot] = choice;
                if (debugSlot != null && debugSlot.SelectedIndex < 0)
                    debugSlot.Note = "사용 가능한 유효 후보가 없어 대기를 선택했습니다. 명중하지 않고 다른 실익도 없는 공격은 제외합니다.";
                if (candidateCount == 0) prefix = AddForcedRisk(context, prefix);
                state.Step(context, choice, slot);
                visitedPositions[slot + 1] = state.Position;
                applied++;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            LastSearch = new EnemySearchMetrics(previews, applied, Stopwatch.GetTimestamp() - start,
                bytes == 0 && after == 0 ? -1 : after - bytes, rejectedAttacks);
            LastTrace = trace;
            return plan;
        }

        private void Retain(Candidate candidate, ref int count)
        {
            // Equivalent directions of the same card must not receive extra lottery entries.
            for (int i = 0; i < count; i++) if (candidate.SameOutcome(candidates[i])) return;
            int index = 0;
            while (index < count && candidates[index].Score >= candidate.Score) index++;
            if (index >= candidates.Length) return;
            for (int i = Math.Min(count, candidates.Length - 1); i > index; i--) candidates[i] = candidates[i - 1];
            candidates[index] = candidate;
            count = Math.Min(count + 1, candidates.Length);
        }

        public static double SoftmaxBestProbability(float best, float second, float temperature) =>
            temperature <= 0 ? 1d : 1d / (1d + Math.Exp(((double)second - best) / temperature));

        private EnemyActionScore AddForcedRisk(EnemyDecisionContext context, EnemyActionScore prefix)
        {
            int risk = state.TargetHealth > 0 ? context.RiskAt(state.Position) : 0;
            return new EnemyActionScore(prefix.Damage, prefix.Opportunity, prefix.Energy, prefix.CostPenalty,
                prefix.RiskPenalty + settings.riskWeight * LastStyle.Preparation * Math.Max(0, risk - state.GuardAmount),
                prefix.IneffectivePenalty, movementPenalty: prefix.MovementPenalty);
        }

        private static bool Used(EnemyPlannedAction[] plan, CardInstance card, int count)
        {
            for (int i = 0; i < count; i++) if (plan[i].Card == card) return true;
            return false;
        }
    }
}
