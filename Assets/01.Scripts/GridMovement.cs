using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class GridMovement : MonoBehaviour
{
    [SerializeField] private Vector2Int gridPosition;
    public Vector2Int GridPosition => gridPosition;
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
        queueUI.Refresh(moveQueue);
    }
    private void Start()
    {
        UpdateWorldPosition();
        queueUI.Refresh(moveQueue);
    }

    private void Update()
    {
        if (queueUI != null && queueUI.UsesCardReservations) return;
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

        queueUI.Refresh(moveQueue);

        if (keyboard.enterKey.wasPressedThisFrame && moveQueue.Count == 3)
        {
            StartCoroutine(ExecuteMoves());
        }
    }

    public bool TryMove(Vector2Int direction)
    {
        Vector2Int next = gridPosition + direction;

        // 전장 밖으로 이동하려 하면 현재 위치를 유지합니다.
        if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
        {
            return false;
        }

        gridPosition = next;
        UpdateWorldPosition();
        return true;
    }

    private void UpdateWorldPosition()
    {
        transform.position = new Vector3(gridPosition.x * cellSize, 0f, gridPosition.y * cellSize);
    }
}