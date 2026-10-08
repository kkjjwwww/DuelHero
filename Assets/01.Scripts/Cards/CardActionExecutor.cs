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
        public event Action<BattleActionResult> ActionResolved;
        private void OnEnable() { if (turnController != null) turnController.ActionResolved += Forward; }
        private void OnDisable() { if (turnController != null) turnController.ActionResolved -= Forward; }
        private void Forward(BattleActionResult result) => ActionResolved?.Invoke(result);
        public bool TryExecute() => isActiveAndEnabled && turnController != null && turnController.TryExecute();
    }
}
