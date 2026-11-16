using System.Collections.Generic;
using DuelHero.Battle;
using UnityEngine;

namespace DuelHero.Cards
{
    public sealed class EnemyActionQueueUI : MonoBehaviour
    {
        [SerializeField] private BattleTurnController source;
        [SerializeField] private EnemyActionSlotView[] slots;
        [SerializeField] private EnemyCardListUI cardList;
        [SerializeField] private GameObject foresightRevealVfx;
        [SerializeField, Min(0.01f)] private float foresightRevealScale = 0.5f;
        private BattlePreviewPresenter preview;
        private int lastFlashRound = -1;
        private readonly HashSet<int> flashedSlots = new();

        private void OnEnable()
        {
            if (source == null) return;
            source.EnemyPlanPrepared += ShowPlan;
            source.EnemySlotsChanged += RefreshStun;
            source.SlotStarted += BeginSlot;
            source.SlotPresenting += PresentSlot;
            source.SlotRevealing += RevealAnimated;
            source.SlotCompleted += CompleteSlot;
            source.PhaseChanged += RefreshInspection;
            if (cardList == null) cardList = FindFirstObjectByType<EnemyCardListUI>();
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
            source.EnemySlotsChanged -= RefreshStun;
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
            if (plan != null && source != null && IsValid(source.PreparedEnemyRevealSlotIndex) &&
                !StunCard.IsStun(plan[source.PreparedEnemyRevealSlotIndex]))
                slots[source.PreparedEnemyRevealSlotIndex].ShowTypeOnly();
            for (int i = 0; i < slots.Length; i++)
                if (source != null && plan != null && i < plan.Count && source.IsForesightSlot(i))
                {
                    RevealSlot(i);
                    if (source.CanInspectSlots && !source.ResultsAvailable) PlayForesightReveal(i);
                }
            RefreshInspection();
        }
        private void RefreshStun()
        {
            for (int i = 0; i < slots.Length && i < source.PreparedEnemyCards.Count; i++)
                if (StunCard.IsStun(source.PreparedEnemyCards[i])) slots[i].ShowStun();
        }
        private void RefreshInspection()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                if (slots[i] != null)
                    slots[i].SetResultInspection(source != null && source.CanInspectSlots && (source.ResultsAvailable || source.IsForesightSlot(i)), OpenCardList,
                        () => preview?.FocusEnemyResult(source, slot),
                        () => preview?.ClearEnemyResult(source, slot),
                        preparation: source != null && source.CanInspectSlots && !source.ResultsAvailable);
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
        public void RevealSlot(int index)
        {
            if (IsValid(index)) slots[index].Reveal();
        }
        private void PlayForesightReveal(int index)
        {
            if (foresightRevealVfx == null || !IsValid(index)) return;
            if (lastFlashRound != source.ForesightRound)
            {
                lastFlashRound = source.ForesightRound;
                flashedSlots.Clear();
            }
            if (!flashedSlots.Add(index)) return;

            var rect = (RectTransform)slots[index].transform;
            var instance = Instantiate(foresightRevealVfx, rect, false);
            instance.transform.localPosition = rect.rect.center;
            instance.transform.localScale *= rect.rect.width * foresightRevealScale;
            Destroy(instance, 1f);
        }

        private bool IsValid(int index) => index >= 0 && index < slots.Length && slots[index] != null;
    }
}
