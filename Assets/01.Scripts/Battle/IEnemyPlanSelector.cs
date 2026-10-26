namespace DuelHero.Battle
{
    public interface IEnemyPlanSelector
    {
        EnemyPlannedAction[] Select(EnemyDecisionContext context);
    }

    public interface IEnemyPlanSelector<TState> where TState : struct, System.Enum
    {
        EnemyPlannedAction[] Select(EnemyDecisionContext context, TState state);
    }
}

