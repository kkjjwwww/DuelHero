using System;
using UnityEngine;
namespace DuelHero.Units
{
    public sealed class UnitStats : MonoBehaviour, IUnitStatus
    {
        public bool IsInitialized { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }
        public event Action Changed;
        public bool Initialize(int health, int maxHealth, int energy, int maxEnergy)
        {
            if (IsInitialized) return false;
            if (maxHealth <= 0 || health < 0 || health > maxHealth || maxEnergy < 0 || energy < 0 || energy > maxEnergy)
                throw new ArgumentOutOfRangeException(nameof(health), "유닛의 시작값과 최대값을 확인해주세요.");
            Health = health; MaxHealth = maxHealth; Energy = energy; MaxEnergy = maxEnergy;
            IsInitialized = true; Changed?.Invoke(); return true;
        }
        public void TakeDamage(int amount)
        {
            Validate(amount);
            int next = amount >= Health ? 0 : Health - amount;
            if (next == Health) return;
            Health = next; Changed?.Invoke();
        }
        public void Heal(int amount)
        {
            Validate(amount);
            int next = Health + Math.Min(amount, MaxHealth - Health);
            if (next == Health) return;
            Health = next; Changed?.Invoke();
        }
        public bool TrySpendEnergy(int amount)
        {
            Validate(amount);
            if (amount > Energy) return false;
            if (amount > 0) { Energy -= amount; Changed?.Invoke(); }
            return true;
        }
        public void RestoreEnergy(int amount)
        {
            Validate(amount);
            int next = Energy + Math.Min(amount, MaxEnergy - Energy);
            if (next == Energy) return;
            Energy = next; Changed?.Invoke();
        }
        private void Validate(int amount)
        {
            if (!IsInitialized) throw new InvalidOperationException("유닛이 아직 초기화되지 않았습니다.");
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        }
    }
}
