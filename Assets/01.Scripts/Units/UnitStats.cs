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
        private readonly GuardState guard = new();
        public int GuardReduction => guard.Reduction;
        public System.Collections.Generic.IReadOnlyList<GuardEffectSnapshot> CaptureGuard() => guard.Capture();
        public void ApplyGuard(int amount, int durationSlots)
        {
            Validate(amount);
            guard.Apply(amount, durationSlots);
            Changed?.Invoke();
        }
        public void EndSlot()
        {
            if (guard.EndSlot()) Changed?.Invoke();
        }
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
            ApplyResolvedDamage(Math.Max(0, amount - GuardReduction), Math.Min(amount, GuardReduction));
        }
        // Shared-slot hits have already allocated each target's available shield.
        internal void ApplyResolvedDamage(int amount, int absorbed = 0)
        {
            Validate(amount);
            int beforeGuard = GuardReduction;
            guard.ReduceDamage(absorbed);
            int next = amount >= Health ? 0 : Health - amount;
            if (next == Health && beforeGuard == GuardReduction) return;
            Health = next; Changed?.Invoke();
        }
        public void Heal(int amount)
        {
            Validate(amount);
            int next = Health + Math.Min(amount, MaxHealth - Health);
            if (next == Health) return;
            Health = next; Changed?.Invoke();
        }
#if UNITY_EDITOR
        public void SetHealthForDebug(int value)
        {
            Validate(0);
            if (value < 0 || value > MaxHealth) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == Health) return;
            Health = value; Changed?.Invoke();
        }
#endif
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
