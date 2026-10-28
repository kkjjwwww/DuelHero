using System.Collections.Generic;
using DuelHero.Battle;
using UnityEngine;

namespace DuelHero.Cards
{
    public sealed class EnemyActionQueueUI : MonoBehaviour
    {
        [SerializeField] private BattleTurnController source;
        [SerializeField] private EnemyActionSlotView[] slots;
        private EnemyCardListUI cardList;
        private BattlePreviewPresenter preview;

        private void OnEnable()
        {
            if (source == null) return;
            source.EnemyPlanPrepared += ShowPlan;
            source.SlotStarted += BeginSlot;
            source.SlotPresenting += PresentSlot;
            source.SlotRevealing += RevealAnimated;
            source.SlotCompleted += CompleteSlot;
            source.PhaseChanged += RefreshInspection;
            cardList = FindFirstObjectByType<EnemyCardListUI>();
            preview = FindFirstObjectByType<BattlePreviewPresenter>();
            // Recover the current display when this UI is re-enabled.
            ShowPlan(source.PreparedEnemyCards);
            for (int i = 0; i < source.CompletedSlots; i++)
            {
                BeginSlot(i);
                CompleteSlot(i);
            }
            RefreshInspection();
        }
        private void OnDisable()
        {
            if (source == null) return;
            source.EnemyPlanPrepared -= ShowPlan;
            source.SlotStarted -= BeginSlot;
            source.SlotPresenting -= PresentSlot;
            source.SlotRevealing -= RevealAnimated;
            source.SlotCompleted -= CompleteSlot;
            source.PhaseChanged -= RefreshInspection;
            foreach (var slot in slots) if (slot != null) slot.SetResultInspection(false, null);
        }
        public void ShowPlan(IReadOnlyList<CardDefinition> plan)
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i].Bind(plan != null && i < plan.Count ? plan[i] ?? WaitCard.Definition : null);
            if (plan != null && source != null && IsValid(source.PreparedEnemyRevealSlotIndex))
                slots[source.PreparedEnemyRevealSlotIndex].ShowTypeOnly();
            RefreshInspection();
        }
        private void RefreshInspection()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                if (slots[i] != null)
                    slots[i].SetResultInspection(source != null && source.ResultsAvailable, OpenCardList,
                        () => preview?.FocusEnemyResult(source, slot),
                        () => preview?.ClearEnemyResult(source, slot),
                        preparation: source != null && !source.IsInputLocked && !source.ResultsAvailable);
            }
        }
        private void OpenCardList() { if (cardList != null) cardList.Open(); }
        public void BeginSlot(int index)
        {
            if (IsValid(index)) slots[index].BeginExecution();
        }
        private void PresentSlot(int index)
        {
            if (IsValid(index)) slots[index].Present(source.CardPresentationDuration);
        }
        private void RevealAnimated(int index)
        {
            if (IsValid(index)) slots[index].Reveal(source.EnemyCardRevealDuration);
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
