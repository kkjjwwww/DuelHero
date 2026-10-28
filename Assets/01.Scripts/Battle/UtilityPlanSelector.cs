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
        public double ElapsedMilliseconds { get; }
        // -1 when the runtime does not expose the allocation counter.
        public long AllocatedBytes { get; }
        internal EnemySearchMetrics(int previews, int applied, long ticks, long bytes)
        {
            ActionPreviews = previews; AppliedActions = applied;
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
            internal Vector2Int Position;
            internal int Energy, TargetHealth, RawDamage, TraceIndex;
            internal bool SameOutcome(Candidate other) => Action.Card == other.Action.Card && Position == other.Position &&
                Energy == other.Energy && TargetHealth == other.TargetHealth && RawDamage == other.RawDamage;
        }
        private readonly Candidate[] candidates = new Candidate[3];
        private readonly UtilityAiSettings settings;
        private readonly EnemyPlanEvaluator evaluator;
        private readonly EnemyPlanningState state = new EnemyPlanningState();
        private readonly EnemyPlanningState preview = new EnemyPlanningState();
        private readonly EnemyPlannedAction wait = new EnemyPlannedAction(new CardInstance(WaitCard.Definition), UnityEngine.Vector2Int.right);
        public EnemySearchMetrics LastSearch { get; private set; }
        public static bool CaptureScores { get; set; }
        public EnemyDecisionTrace LastTrace { get; private set; }

        public UtilityPlanSelector(UtilityAiSettings settings)
        { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); evaluator = new EnemyPlanEvaluator(settings); }

        public EnemyPlannedAction[] Select(EnemyDecisionContext context)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            settings.Validate();
            state.Reset(context);
            LastTrace = null;
            var trace = CaptureScores ? new EnemyDecisionTrace(context.Round, context.DecisionSeed) : null;
            var random = new EnemyDecisionRandom(context.DecisionSeed);
            var plan = new EnemyPlannedAction[3];
            int previews = 0, applied = 0;
            float attackBurden = 0;
            for (int slot = 0; slot < plan.Length; slot++)
            {
                var debugSlot = trace?.BeginSlot(slot, state);
                if (state.TargetHealth <= 0 || context.Actor.Health <= 0)
                {
                    plan[slot] = wait;
                    if (debugSlot != null) debugSlot.Note = "전투 종료로 실행하지 않는 슬롯입니다.";
                    continue;
                }

                int candidateCount = 0;
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
                        float burden = attackBurden + (prepared.HasDamage ? evaluator.AttackCostPenalty(prepared.EnergyCost, state.Energy) : 0);
                        plan[slot] = action;
                        var score = evaluator.Evaluate(context, preview, plan, slot + 1, burden);
                        debugSlot?.Add(cardIndex, card, action.Direction, state, preview, score);
                        previews++;
                        Retain(new Candidate { Action = action, Score = score.Total, Burden = burden,
                            Position = preview.Position, Energy = preview.Energy, TargetHealth = preview.TargetHealth,
                            RawDamage = rawDamage, TraceIndex = (debugSlot?.Candidates.Count ?? 0) - 1 }, ref candidateCount);
                    }
                }
                var choice = wait;
                if (candidateCount > 0)
                {
                    float best = candidates[0].Score;
                    float minimum = best - settings.randomScoreTolerance * Math.Max(1f, Math.Abs(best));
                    int eligible = 1;
                    while (eligible < candidateCount && candidates[eligible].Score >= minimum) eligible++;
                    var chosen = candidates[eligible > 1 ? random.Next(eligible) : 0];
                    choice = chosen.Action; attackBurden = chosen.Burden;
                    if (debugSlot != null)
                    {
                        debugSlot.BestScore = best; debugSlot.MinimumScore = minimum; debugSlot.SelectedIndex = chosen.TraceIndex;
                        for (int i = 0; i < eligible; i++) debugSlot.AddEligible(candidates[i].TraceIndex);
                    }
                }
                // Wait remains a fallback only when every owned action is unavailable.
                plan[slot] = choice;
                if (debugSlot != null && debugSlot.SelectedIndex < 0)
                    debugSlot.Note = "사용 가능한 카드가 없어 대기를 선택했습니다. 점수 비교는 하지 않았습니다.";
                state.Step(context, choice, slot);
                applied++;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            LastSearch = new EnemySearchMetrics(previews, applied, Stopwatch.GetTimestamp() - start,
                bytes == 0 && after == 0 ? -1 : after - bytes);
            LastTrace = trace;
            return plan;
        }

        private void Retain(Candidate candidate, ref int count)
        {
            // Equivalent directions of the same card must not receive extra lottery entries.
            for (int i = 0; i < count; i++) if (candidate.SameOutcome(candidates[i])) return;
            int index = 0;
            while (index < count && candidates[index].Score >= candidate.Score) index++;
            if (index >= settings.randomCandidateCount) return;
            for (int i = Math.Min(count, settings.randomCandidateCount - 1); i > index; i--) candidates[i] = candidates[i - 1];
            candidates[index] = candidate;
            count = Math.Min(count + 1, settings.randomCandidateCount);
        }

        private static bool Used(EnemyPlannedAction[] plan, CardInstance card, int count)
        {
            for (int i = 0; i < count; i++) if (plan[i].Card == card) return true;
            return false;
        }
    }
}
