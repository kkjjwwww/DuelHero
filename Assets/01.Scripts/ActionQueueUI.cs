using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DuelHero.Cards;

public class ActionQueueUI : MonoBehaviour
{
    [SerializeField] private Text[] actionLabels = new Text[3];
    [SerializeField] private Image[] slotImages = new Image[3];
    [SerializeField] private CardReservationQueue reservationQueue;
    [SerializeField] private Button executeButton;
    [SerializeField] private CardActionExecutor actionExecutor;
    [SerializeField] private Text executionStatus;
    private readonly List<UnityEngine.Events.UnityAction> cancelListeners = new();
    private readonly List<Button> reservationButtons = new();
    public bool UsesCardReservations => reservationQueue != null;
    private void OnEnable()
    {
        if (executeButton != null) executeButton.interactable = false;
        if (reservationQueue == null) return;
        reservationQueue.Changed += RefreshCards;
        if (executeButton != null) executeButton.onClick.AddListener(ExecuteCards);
        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] == null) continue;
            int slot = i;
            var button = slotImages[i].GetComponent<Button>();
            if (button == null) button = slotImages[i].gameObject.AddComponent<Button>();
            button.targetGraphic = slotImages[i];
            UnityEngine.Events.UnityAction listener = () => reservationQueue.CancelAt(slot);
            button.onClick.AddListener(listener); cancelListeners.Add(listener); reservationButtons.Add(button);
        }
        RefreshCards();
    }
    private void OnDisable()
    {
        if (reservationQueue != null) reservationQueue.Changed -= RefreshCards;
        if (executeButton != null) executeButton.onClick.RemoveListener(ExecuteCards);
        for (int i = 0; i < reservationButtons.Count; i++) if (reservationButtons[i] != null) reservationButtons[i].onClick.RemoveListener(cancelListeners[i]);
        reservationButtons.Clear(); cancelListeners.Clear();
    }
    public void RefreshCards()
    {
        bool energyValid = reservationQueue != null && reservationQueue.HasEnoughEnergy(out _);
        bool battleEnded = actionExecutor != null && actionExecutor.Outcome != DuelHero.Battle.BattleOutcome.Running;
        if (executeButton != null) executeButton.interactable = reservationQueue != null && !battleEnded && !reservationQueue.IsExecuting && reservationQueue.Reservations.Count == 3 && energyValid;
        if (reservationQueue == null) return;
        for (int i = 0; i < actionLabels.Length; i++)
        {
            if (actionLabels[i] != null) actionLabels[i].text = i < reservationQueue.Reservations.Count ? reservationQueue.Reservations[i].Definition.name : "--";
            if (i < slotImages.Length && slotImages[i] != null) slotImages[i].color = i == reservationQueue.ActiveSlot ? executingColor : normalColor;
        }
        foreach (var button in reservationButtons) button.interactable = !reservationQueue.IsExecuting;
        if (executionStatus != null)
        {
            reservationQueue.HasEnoughEnergy(out string reason);
            executionStatus.text = battleEnded ? actionExecutor.Outcome switch
            {
                DuelHero.Battle.BattleOutcome.Victory => "승리",
                DuelHero.Battle.BattleOutcome.Defeat => "패배",
                _ => "무승부"
            } : reservationQueue.IsExecuting ? "행동 실행 중" : energyValid ? "턴 " + reservationQueue.CurrentRound : reason;
        }
    }
    private void ExecuteCards()
    {
        if (actionExecutor != null && !actionExecutor.TryExecute() && executionStatus != null)
            executionStatus.text = actionExecutor.LastError ?? "실행할 수 없습니다.";
    }

    private readonly Color normalColor = new Color(0.16f, 0.16f, 0.16f, 0.65f);

    private readonly Color executingColor = new Color(0.55f, 0.55f, 0.55f, 0.80f);

    public void Refresh(IReadOnlyList<Vector2Int> queue, int activeIndex = -1)
    {
        if (UsesCardReservations) { RefreshCards(); return; }
        for (int i = 0; i < actionLabels.Length; i++)
        {
            actionLabels[i].text =
                i < queue.Count ? GetLabel(queue[i]) : "--";

            slotImages[i].color =
                i == activeIndex ? executingColor : normalColor;
        }
    }

    private string GetLabel(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return "UP";
        if (direction == Vector2Int.down) return "DOWN";
        if (direction == Vector2Int.left) return "LEFT";
        if (direction == Vector2Int.right) return "RIGHT";
        return "--";
    }
}
