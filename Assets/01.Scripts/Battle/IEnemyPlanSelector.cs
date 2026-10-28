namespace DuelHero.Battle
{
    public interface IEnemyPlanSelector
    {
        EnemyPlannedAction[] Select(EnemyDecisionContext context);
    }
}

