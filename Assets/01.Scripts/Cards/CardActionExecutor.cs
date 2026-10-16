using System;
using DuelHero.Battle;
using UnityEngine;
namespace DuelHero.Cards
{
    // Existing UI/log entry point; both units execute through the shared controller.
    public sealed class CardActionExecutor : MonoBehaviour, IBattleActionSource
    {
        [SerializeField] private BattleTurnController turnController;
        public string LastError => turnController == null ? "공통 턴 진행기를 연결해주세요." : turnController.LastError;
        public BattleOutcome Outcome => turnController == null ? BattleOutcome.Running : turnController.Outcome;
        public bool AwaitingNextTurn => turnController != null && turnController.AwaitingNextTurn;
        public bool IsInputLocked => turnController != null && turnController.IsInputLocked;
        public int CompletedSlots => turnController != null ? turnController.CompletedSlots : 0;
        public float CardPresentationDuration => turnController != null ? turnController.CardPresentationDuration : 0f;
        public event Action<int> SlotPresenting;
        public event Action PhaseChanged;
        public event Action<BattleActionResult> ActionResolved;
        private void OnEnable() { if (turnController != null) { turnController.ActionResolved += Forward; turnController.PhaseChanged += ForwardPhase; turnController.SlotPresenting += ForwardSlot; } }
        private void OnDisable() { if (turnController != null) { turnController.ActionResolved -= Forward; turnController.PhaseChanged -= ForwardPhase; turnController.SlotPresenting -= ForwardSlot; } }
        private void ForwardSlot(int slot) => SlotPresenting?.Invoke(slot);
        private void ForwardPhase() => PhaseChanged?.Invoke();
        public bool TryAdvanceTurn() => isActiveAndEnabled && turnController != null && turnController.TryAdvanceTurn();
        private void Forward(BattleActionResult result) => ActionResolved?.Invoke(result);
        public bool TryExecute() => isActiveAndEnabled && turnController != null && turnController.TryExecute();
    }
}
