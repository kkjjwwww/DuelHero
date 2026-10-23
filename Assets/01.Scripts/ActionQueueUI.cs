using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DuelHero.Cards;
using DG.Tweening;

public class ActionQueueUI : MonoBehaviour
{
    [SerializeField] private Text[] actionLabels = new Text[3];
    [SerializeField] private Image[] slotImages = new Image[3];
    [SerializeField] private CardReservationQueue reservationQueue;
    [SerializeField] private Button executeButton;
    [SerializeField] private CardActionExecutor actionExecutor;
    [SerializeField] private Text executionStatus;
    [SerializeField] private Color emptySlotColor = Color.clear;
    [Header("Next Turn Outline")]
    [SerializeField, Min(0f)] private float nextTurnOutlineThickness = 2f;
    [SerializeField, Min(.01f)] private float nextTurnBlinkHalfPeriod = .55f;
    [Header("Reserved Card Display")]
    [SerializeField] private CardView cardPrefab;
    [SerializeField] private CardDatabase cardDatabase;
    [Header("Execution Motion")]
    [SerializeField, Min(1f)] private float executionScale = 1.1f;
    [SerializeField, Min(0f)] private float executionMoveDistance = 12f;
    private CardView[] slotCards;
    private CardInstance[] boundCards;
    private bool[] slotExecuted;
    private Tween[] slotExecutionTweens;
    private Font cardFont;
    private KeywordTooltip keywordTooltip;
    private Tween nextTurnBlink;
    private CardReservationOutline nextTurnOutline;
    private CanvasGroup nextTurnOutlineGroup;
    private Text executeButtonLabel;
    private readonly List<UnityEngine.Events.UnityAction> cancelListeners = new();
    private readonly List<Button> reservationButtons = new();
    public bool UsesCardReservations => reservationQueue != null;
    private void OnEnable()
    {
        if (executeButton != null) executeButton.interactable = false;
        if (reservationQueue == null) return;
        reservationQueue.Changed += RefreshCards;
        if (actionExecutor != null) actionExecutor.PhaseChanged += RefreshCards;
        if (actionExecutor != null) actionExecutor.SlotPresenting += BeginSlotExecution;
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
        if (keywordTooltip != null) keywordTooltip.Hide();
        if (reservationQueue != null) reservationQueue.Changed -= RefreshCards;
        if (actionExecutor != null) actionExecutor.PhaseChanged -= RefreshCards;
        if (actionExecutor != null) actionExecutor.SlotPresenting -= BeginSlotExecution;
        if (slotExecutionTweens != null)
            for (int i = 0; i < slotExecutionTweens.Length; i++) StopSlotMotion(i);
        StopNextTurnBlink();
        if (executeButton != null) executeButton.onClick.RemoveListener(ExecuteCards);
        for (int i = 0; i < reservationButtons.Count; i++) if (reservationButtons[i] != null) reservationButtons[i].onClick.RemoveListener(cancelListeners[i]);
        reservationButtons.Clear(); cancelListeners.Clear();
    }
    public void RefreshCards()
    {
        if (actionExecutor != null && actionExecutor.IsInputLocked && keywordTooltip != null) keywordTooltip.Hide();
        bool energyValid = reservationQueue != null && reservationQueue.HasEnoughEnergy(out _);
        bool battleEnded = actionExecutor != null && actionExecutor.Outcome != DuelHero.Battle.BattleOutcome.Running;
        bool nextTurn = actionExecutor != null && actionExecutor.AwaitingNextTurn;
        if (executeButton != null)
        {
            executeButton.interactable = !battleEnded && (nextTurn || (reservationQueue != null && !reservationQueue.IsExecuting && reservationQueue.Reservations.Count == 3 && energyValid));
            if (executeButtonLabel == null) executeButtonLabel = executeButton.GetComponentInChildren<Text>(true);
            if (executeButtonLabel != null) executeButtonLabel.text = nextTurn ? "다음 턴 진행" : "행동 실행";
            if (nextTurn && nextTurnBlink == null)
            {
                if (nextTurnOutline == null)
                {
                    var go = new GameObject("NextTurnOutline", typeof(RectTransform), typeof(CardReservationOutline), typeof(CanvasGroup));
                    go.transform.SetParent(executeButton.transform, false);
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    nextTurnOutline = go.GetComponent<CardReservationOutline>();
                    nextTurnOutline.color = Color.white;
                    nextTurnOutline.raycastTarget = false;
                    nextTurnOutlineGroup = go.GetComponent<CanvasGroup>();
                    nextTurnOutlineGroup.blocksRaycasts = nextTurnOutlineGroup.interactable = false;
                }
                nextTurnOutline.Thickness = nextTurnOutlineThickness;
                nextTurnOutline.gameObject.SetActive(true);
                nextTurnOutlineGroup.alpha = 1f;
                nextTurnBlink = nextTurnOutlineGroup.DOFade(0f, nextTurnBlinkHalfPeriod).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            else if (!nextTurn) StopNextTurnBlink();
        }
        if (reservationQueue == null) return;
        for (int i = 0; i < actionLabels.Length; i++)
        {
            bool occupied = i < reservationQueue.Reservations.Count;
            if (actionLabels[i] != null)
            {
                actionLabels[i].text = occupied ? reservationQueue.Reservations[i].Definition.name : "";
                actionLabels[i].gameObject.SetActive(cardPrefab == null);
            }
            if (i < slotImages.Length && slotImages[i] != null)
                slotImages[i].color = occupied ? normalColor : emptySlotColor;
            RefreshSlotCard(i, occupied);
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
            } : nextTurn ? "행동 완료 · 다음 턴 진행 대기" : reservationQueue.IsExecuting ? "행동 실행 중" : energyValid ? "턴 " + reservationQueue.CurrentRound : reason;
        }
    }

    private void RefreshSlotCard(int index, bool occupied)
    {
        if (cardPrefab == null || cardDatabase == null || index >= slotImages.Length || slotImages[index] == null) return;
        if (slotCards == null || slotCards.Length != slotImages.Length)
            slotCards = new CardView[slotImages.Length];
        if (boundCards == null || boundCards.Length != slotImages.Length)
            boundCards = new CardInstance[slotImages.Length];
        if (slotExecuted == null || slotExecuted.Length != slotImages.Length)
            slotExecuted = new bool[slotImages.Length];
        if (slotExecutionTweens == null || slotExecutionTweens.Length != slotImages.Length)
            slotExecutionTweens = new Tween[slotImages.Length];
        Transform parent = slotImages[index].transform;
        foreach (Transform child in parent)
            if (child.name == "SlotNumber" || child.name.StartsWith("EmptySlotEdge_")) child.gameObject.SetActive(!occupied);
        if (!occupied)
        {
            StopSlotMotion(index);
            slotExecuted[index] = false;
            if (slotCards[index] != null) slotCards[index].gameObject.SetActive(false);
            boundCards[index] = null;
            return;
        }
        CardInstance instance = reservationQueue.Reservations[index];
        CardView view = slotCards[index];
        if (view == null)
        {
            view = Instantiate(cardPrefab, parent, false);
            view.name = "ReservedCard";
            view.transform.SetAsFirstSibling();
            slotCards[index] = view;
            if (cardFont == null) cardFont = DuelHero.UI.GameFonts.UI;
            view.SetFont(cardFont);
            int slot = index;
            view.SetClickAction(() => { if (!reservationQueue.IsExecuting) reservationQueue.CancelAt(slot); });
            view.SetKeywordHoverActions((keyword, position) =>
            {
                if (actionExecutor != null && actionExecutor.IsInputLocked) return;
                if (keywordTooltip == null) keywordTooltip = KeywordTooltip.Create(GetComponentInParent<Canvas>().rootCanvas, cardFont);
                keywordTooltip.Show(keyword, position);
            }, () => { if (keywordTooltip != null) keywordTooltip.Hide(); },
            position => { if (keywordTooltip != null) keywordTooltip.Move(position); });
        }
        if (!ReferenceEquals(boundCards[index], instance))
        {
            StopSlotMotion(index);
            slotExecuted[index] = false;
            if (keywordTooltip != null) keywordTooltip.Hide();
            view.Bind(instance.Definition, cardDatabase.keywords);
            boundCards[index] = instance;
        }
        var rect = (RectTransform)view.transform;
        Rect slotRect = ((RectTransform)parent).rect;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localRotation = Quaternion.identity;
        if (!reservationQueue.IsExecuting) slotExecuted[index] = false;
        else if (actionExecutor != null && index < actionExecutor.CompletedSlots) slotExecuted[index] = true;
        if (slotExecutionTweens[index] == null || !slotExecutionTweens[index].IsActive())
        {
            rect.anchoredPosition = slotExecuted[index] ? Vector2.up * executionMoveDistance : Vector2.zero;
            rect.localScale = Vector3.one * Mathf.Min(slotRect.width / rect.rect.width, slotRect.height / rect.rect.height)
                * (slotExecuted[index] ? executionScale : 1f);
        }
        view.gameObject.SetActive(true);
        view.SetState(string.Empty, 0, true);
        var hoverScale = view.GetComponent<ButtonHoverScale>();
        if (hoverScale == null)
        {
            hoverScale = view.gameObject.AddComponent<ButtonHoverScale>();
            hoverScale.ConfigureTargets(view.VisualRoot);
        }
        hoverScale.enabled = !reservationQueue.IsExecuting;
        if (hoverScale.enabled) hoverScale.RefreshInteractability();
    }
    private void BeginSlotExecution(int index)
    {
        if (index < 0 || index >= slotImages.Length) return;
        if (slotCards == null || slotCards[index] == null) RefreshCards();
        if (slotCards == null || slotCards[index] == null) return;
        StopSlotMotion(index);
        slotExecuted[index] = true;
        var rect = (RectTransform)slotCards[index].transform;
        var bounds = ((RectTransform)slotImages[index].transform).rect;
        var scale = Vector3.one * Mathf.Min(bounds.width / rect.rect.width, bounds.height / rect.rect.height) * executionScale;
        var position = Vector2.up * executionMoveDistance;
        float duration = actionExecutor != null ? actionExecutor.CardPresentationDuration : 0f;
        if (duration <= 0f)
        {
            rect.localScale = scale; rect.anchoredPosition = position;
        }
        else slotExecutionTweens[index] = DOTween.Sequence()
            .Join(rect.DOScale(scale, duration).SetEase(Ease.OutQuad))
            .Join(rect.DOAnchorPos(position, duration).SetEase(Ease.OutQuad));
    }
    private void StopSlotMotion(int index)
    {
        if (slotExecutionTweens == null || index >= slotExecutionTweens.Length) return;
        slotExecutionTweens[index]?.Kill();
        slotExecutionTweens[index] = null;
    }

    private void OnDestroy()
    {
        if (keywordTooltip != null) Destroy(keywordTooltip.gameObject);
    }
    private void ExecuteCards()
    {
        if (actionExecutor != null && actionExecutor.AwaitingNextTurn) { actionExecutor.TryAdvanceTurn(); return; }
        if (actionExecutor != null && !actionExecutor.TryExecute() && executionStatus != null)
            executionStatus.text = actionExecutor.LastError ?? "실행할 수 없습니다.";
    }
    private void StopNextTurnBlink()
    {
        nextTurnBlink?.Kill(); nextTurnBlink = null;
        if (nextTurnOutline != null) nextTurnOutline.gameObject.SetActive(false);
    }

    private readonly Color normalColor = new Color(0.16f, 0.16f, 0.16f, 0.65f);


    public void Refresh(IReadOnlyList<Vector2Int> queue, int activeIndex = -1)
    {
        if (UsesCardReservations) { RefreshCards(); return; }
        for (int i = 0; i < actionLabels.Length; i++)
        {
            actionLabels[i].text =
                i < queue.Count ? GetLabel(queue[i]) : "--";

            slotImages[i].color = normalColor;
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
