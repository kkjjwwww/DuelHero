using System.Collections.Generic;
using DuelHero.Battle;
using UnityEngine;

namespace DuelHero.Cards
{
    public sealed class EnemyActionQueueUI : MonoBehaviour
    {
        [SerializeField] private BattleTurnController source;
        [SerializeField] private EnemyActionSlotView[] slots;

        private void OnEnable()
        {
            if (source == null) return;
            source.EnemyPlanPrepared += ShowPlan;
            source.SlotStarted += BeginSlot;
            source.SlotCompleted += CompleteSlot;
            // Recover the current display when this UI is re-enabled.
            ShowPlan(source.PreparedEnemyCards);
            for (int i = 0; i < source.CompletedSlots; i++)
            {
                RevealSlot(i);
                CompleteSlot(i);
            }
        }
        private void OnDisable()
        {
            if (source == null) return;
            source.EnemyPlanPrepared -= ShowPlan;
            source.SlotStarted -= BeginSlot;
            source.SlotCompleted -= CompleteSlot;
        }
        public void ShowPlan(IReadOnlyList<CardDefinition> plan)
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i].Bind(plan != null && i < plan.Count ? plan[i] : null);
        }
        public void BeginSlot(int index)
        {
            if (IsValid(index)) slots[index].BeginExecution();
        }
        public void CompleteSlot(int index)
        {
            if (IsValid(index)) slots[index].CompleteExecution();
        }
        // Independent entry point for future preparation-phase reveal effects.
        public void RevealSlot(int index)
        {
            if (IsValid(index)) slots[index].Reveal();
        }
        private bool IsValid(int index) => index >= 0 && index < slots.Length && slots[index] != null;
    }
}
