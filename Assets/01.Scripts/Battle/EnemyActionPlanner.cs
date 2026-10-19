namespace DuelHero.Battle
{
    // Compose a matching FSM and selector without coupling the provider to their state type.
    public interface IEnemyActionPlanner
    {
        EnemyPlannedAction[] CreatePlan(EnemyDecisionContext context);
    }

    public sealed class EnemyActionPlanner<TState> : IEnemyActionPlanner where TState : struct, System.Enum
    {
        private readonly IEnemyStateMachine<TState> stateMachine;
        private readonly IEnemyPlanSelector<TState> selector;
        public TState CurrentState { get; private set; }

        public EnemyActionPlanner(IEnemyStateMachine<TState> stateMachine, IEnemyPlanSelector<TState> selector)
        { this.stateMachine = stateMachine; this.selector = selector; }

        public EnemyPlannedAction[] CreatePlan(EnemyDecisionContext context)
        {
            // Evaluate once per round, then use the same state for the full three-action plan.
            CurrentState = stateMachine.Evaluate(context);
            return selector.Select(context, CurrentState);
        }
    }
}
