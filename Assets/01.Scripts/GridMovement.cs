using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class GridMovement : MonoBehaviour, DuelHero.Battle.IBattleActionSource
{
    [SerializeField] private Vector2Int gridPosition;
    public Vector2Int GridPosition => gridPosition;
    public float CellSize => cellSize;
    [SerializeField] private DuelHero.Battle.BoardOccupancy board;
    public DuelHero.Battle.BoardOccupancy Board => board;
    public void AttachBoard(DuelHero.Battle.BoardOccupancy value) => board = value;
    private void OnEnable() { if (board != null) board.RegisterUnit(this); }
    private void OnDisable() { if (board != null) board.UnregisterUnit(this); }
    [SerializeField] private string actorId;
    public event System.Action<DuelHero.Battle.BattleActionResult> ActionResolved;
    [SerializeField] private int width = 4;
    [SerializeField] private int height = 3;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private ActionQueueUI queueUI;

    private readonly List<Vector2Int> moveQueue = new();
    private bool isExecuting;

    private IEnumerator ExecuteMoves()
    {
        isExecuting = true;

        for (int i = 0; i < moveQueue.Count; i++)
        {
            queueUI.Refresh(moveQueue, i);
            TryMove(moveQueue[i]);

            yield return new WaitForSeconds(0.4f);
        }

        moveQueue.Clear();
        isExecuting = false;
        queueUI?.Refresh(moveQueue);
    }
    private void Start()
    {
        UpdateWorldPosition();
        queueUI?.Refresh(moveQueue);
    }

    private void Update()
    {
        if (queueUI == null || queueUI.UsesCardReservations) return;
        if (isExecuting) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (moveQueue.Count < 3)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame)
                moveQueue.Add(Vector2Int.up);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                moveQueue.Add(Vector2Int.down);
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
                moveQueue.Add(Vector2Int.left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                moveQueue.Add(Vector2Int.right);
        }
        if (keyboard.backspaceKey.wasPressedThisFrame && moveQueue.Count > 0)
        {
            moveQueue.RemoveAt(moveQueue.Count - 1);
        }

        queueUI?.Refresh(moveQueue);

        if (keyboard.enterKey.wasPressedThisFrame && moveQueue.Count == 3)
        {
            StartCoroutine(ExecuteMoves());
        }
    }

    public bool TryMove(Vector2Int direction, string cardId = null, int round = 0)
    {
        Vector2Int from = gridPosition;
        Vector2Int next = gridPosition + direction;

        // 전장 밖으로 이동하려 하면 현재 위치를 유지합니다.
        if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
        {
            ActionResolved?.Invoke(new DuelHero.Battle.BattleActionResult(DuelHero.Battle.BattleActionKind.Movement, actorId, round, cardId, DuelHero.Battle.BattleActionOutcome.BoundaryBlocked, from, from));
            return false;
        }

        gridPosition = next;
        UpdateWorldPosition();
        ActionResolved?.Invoke(new DuelHero.Battle.BattleActionResult(DuelHero.Battle.BattleActionKind.Movement, actorId, round, cardId, DuelHero.Battle.BattleActionOutcome.Success, from, gridPosition, 1));
        return true;
    }

    private void UpdateWorldPosition()
    {
        transform.position = new Vector3(gridPosition.x * cellSize, 0f, gridPosition.y * cellSize);
    }
}