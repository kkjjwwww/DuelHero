using System;
namespace DuelHero.Units
{
    public interface IUnitStatus
    {
        bool IsInitialized { get; }
        int Health { get; }
        int MaxHealth { get; }
        int Energy { get; }
        int MaxEnergy { get; }
        event Action Changed;
    }
}
