using System;
using System.Diagnostics;
using DuelHero.Cards;

namespace DuelHero.Battle
{
    public readonly struct EnemySearchMetrics
    {
        public int ActionPreviews { get; }
        public int ExpandedPlans { get; }
        public int PeakRetainedPlans { get; }
        public double ElapsedMilliseconds { get; }
        public long AllocatedBytes { get; }
        internal EnemySearchMetrics(int previews, int expanded, int retained, long ticks, long bytes)
        {
            ActionPreviews = previews; ExpandedPlans = expanded; PeakRetainedPlans = retained;
            ElapsedMilliseconds = ticks * 1000d / Stopwatch.Frequency; AllocatedBytes = bytes;
        }
    }

    // Bounded three-slot search. Work to scan cards/directions grows with the deck;
    // the number of prefixes carried into another slot is capped independently.
    public sealed class UtilityPlanSelector : IEnemyPlanSelector
    {
        private enum Preparation { Other, Attack, Approach, Recover }
        private sealed class Candidate
        {
            internal readonly EnemyPlannedAction[] Plan = new EnemyPlannedAction[3];
            internal readonly EnemyPlanningState State = new EnemyPlanningState();
            internal int RawDamage;
            internal float Score, AttackBurden;
            internal Preparation Kind;
            internal int Order;
            internal void CopyFrom(Candidate source, bool copyShields = true)
            {
                Array.Copy(source.Plan, Plan, 3);
                if (copyShields) State.CopyFrom(source.State); else State.CopyValuesFrom(source.State);
                RawDamage = source.RawDamage; Score = source.Score; AttackBurden = source.AttackBurden;
                Kind = source.Kind; Order = source.Order;
            }
            internal bool Used(CardInstance card, int count)
            {
                for (int i = 0; i < count; i++) if (Plan[i].Card == card) return true;
                return false;
            }
        }

        private readonly UtilityAiSettings settings;
        private readonly EnemyPlanEvaluator evaluator;
        private readonly Candidate scratch = new Candidate();
        private readonly EnemyPlannedAction wait = new EnemyPlannedAction(new CardInstance(WaitCard.Definition), UnityEngine.Vector2Int.right);
        private Candidate[] current, next, actions;
        public EnemySearchMetrics LastSearch { get; private set; }

        public UtilityPlanSelector(UtilityAiSettings settings)
        { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); evaluator = new EnemyPlanEvaluator(settings); }

        public EnemyPlannedAction[] Select(EnemyDecisionContext context)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            settings.Validate();
            EnsureBuffers();
            current[0].State.Reset(context);
            Array.Clear(current[0].Plan, 0, 3);
            current[0].AttackBurden = 0;
            int count = 1, previews = 0, expanded = 0, peak = 1, order = 0;
            for (int slot = 0; slot < 3; slot++)
            {
                int nextCount = 0;
                for (int parentIndex = 0; parentIndex < count; parentIndex++)
                {
                    var parent = current[parentIndex];
                    int actionCount = 0;
                    bool finished = parent.State.TargetHealth <= 0 || context.Actor.Health <= 0;
                    if (!finished)
                    {
                        for (int cardIndex = 0; cardIndex < context.Cards.Count; cardIndex++)
                        {
                            var card = context.Cards[cardIndex];
                            if (WaitCard.IsWait(card.Definition) || !card.CanReserve(context.Round) || parent.Used(card, slot) ||
                                card.Definition.energyCost < 0 || card.Definition.energyCost > parent.State.Energy) continue;
                            for (int direction = 0; direction < EnemyPlanningRules.DirectionCount(card); direction++)
                            {
                                var action = EnemyPlanningRules.Action(card, direction);
                                Preview(context, parent, action, slot, ++order);
                                previews++;
                                Insert(scratch, actions, ref actionCount, settings.actionsPerPlan, slot < 2, true, slot);
                            }
                        }
                    }
                    // Wait is a system fallback, never a tactical alternative to a usable card.
                    if (actionCount == 0)
                    {
                        Preview(context, parent, wait, slot, ++order, finished);
                        previews++;
                        Insert(scratch, actions, ref actionCount, settings.actionsPerPlan, false, true, slot);
                    }
                    Sort(actions, actionCount);
                    expanded += actionCount;
                    for (int i = 0; i < actionCount; i++)
                    {
                        // Only these bounded candidates clone shield layers and execute the
                        // full shared transition. Deck scanning above uses value-only previews.
                        actions[i].State.CopyFrom(parent.State);
                        if (!finished) actions[i].State.Step(context, actions[i].Plan[slot], slot);
                        actions[i].Score = evaluator.Evaluate(context, actions[i].State, actions[i].Plan,
                            slot + 1, actions[i].AttackBurden);
                        Insert(actions[i], next, ref nextCount, settings.retainedPlans, slot < 2, false, slot);
                    }
                }
                Sort(next, nextCount);
                var swap = current; current = next; next = swap;
                count = nextCount; peak = Math.Max(peak, count);
            }
            var result = new EnemyPlannedAction[3];
            Array.Copy(current[0].Plan, result, 3);
            LastSearch = new EnemySearchMetrics(previews, expanded, peak, Stopwatch.GetTimestamp() - start,
                bytes == 0 && GC.GetAllocatedBytesForCurrentThread() == 0 ? -1 : GC.GetAllocatedBytesForCurrentThread() - bytes);
            return result;
        }

        private void Preview(EnemyDecisionContext context, Candidate parent, EnemyPlannedAction action, int slot,
            int order, bool finished = false)
        {
            scratch.CopyFrom(parent, false); scratch.Plan[slot] = action; scratch.Order = order;
            scratch.Kind = Preparation.Other;
            if (!finished)
            {
                var prepared = context.GetPreparedAction(action);
                scratch.RawDamage = scratch.State.PreviewAction(context, prepared, parent.State);
                if (prepared.HasDamage)
                    scratch.AttackBurden += evaluator.AttackCostPenalty(action.Card.Definition.energyCost, parent.State.Energy);
                if (scratch.RawDamage > 0) scratch.Kind = Preparation.Attack;
                else if (scratch.State.Energy > parent.State.Energy) scratch.Kind = Preparation.Recover;
                else if (scratch.State.Position != parent.State.Position) scratch.Kind = Preparation.Approach;
            }
            scratch.Score = evaluator.Evaluate(context, scratch.State, scratch.Plan, slot + 1, scratch.AttackBurden, false);
        }

        private static bool Better(Candidate a, Candidate b) => a.Score > b.Score || (a.Score == b.Score && a.Order < b.Order);

        private static void Insert(Candidate candidate, Candidate[] buffer, ref int count, int limit,
            bool preservePreparation, bool mergeDirections, int slot)
        {
            if (mergeDirections)
                for (int i = 0; i < count; i++)
                    if (buffer[i].Plan[slot].Card == candidate.Plan[slot].Card &&
                        buffer[i].State.Position == candidate.State.Position && buffer[i].State.Energy == candidate.State.Energy &&
                        buffer[i].State.TargetHealth == candidate.State.TargetHealth &&
                        buffer[i].RawDamage == candidate.RawDamage) return;
            if (count < limit) { buffer[count++].CopyFrom(candidate, !mergeDirections); return; }
            var incoming = candidate.Kind;
            bool missing = incoming != Preparation.Other;
            for (int i = 0; i < count; i++) if (buffer[i].Kind == incoming) missing = false;
            int worst = -1;
            for (int i = 0; i < count; i++)
            {
                var category = buffer[i].Kind;
                if (preservePreparation && limit >= 3 && category != Preparation.Other && category != incoming)
                {
                    int same = 0;
                    for (int j = 0; j < count; j++) if (buffer[j].Kind == category) same++;
                    if (same == 1) continue;
                }
                if (worst < 0 || Better(buffer[worst], buffer[i])) worst = i;
            }
            if (worst >= 0 && (Better(candidate, buffer[worst]) || (preservePreparation && limit >= 3 && missing)))
                buffer[worst].CopyFrom(candidate, !mergeDirections);
        }

        private static void Sort(Candidate[] buffer, int count)
        {
            for (int i = 1; i < count; i++)
                for (int j = i; j > 0 && Better(buffer[j], buffer[j - 1]); j--)
                { var swap = buffer[j - 1]; buffer[j - 1] = buffer[j]; buffer[j] = swap; }
        }
        private void EnsureBuffers()
        {
            if (current == null || current.Length != settings.retainedPlans)
            { current = Create(settings.retainedPlans); next = Create(settings.retainedPlans); }
            if (actions == null || actions.Length != settings.actionsPerPlan) actions = Create(settings.actionsPerPlan);
        }
        private static Candidate[] Create(int count)
        {
            var result = new Candidate[count];
            for (int i = 0; i < count; i++) result[i] = new Candidate();
            return result;
        }
    }
}
