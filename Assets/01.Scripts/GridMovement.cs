using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class GridMovement : MonoBehaviour
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private int width = 4;
    [SerializeField] private int height = 3;
    [SerializeField] private float cellSize = 1f;

    private readonly List<Vector2Int> moveQueue = new();
    private bool isExecuting;

    private IEnumerator ExecuteMoves()
    {
        isExecuting = true;

        foreach (Vector2Int direction in moveQueue)
        {
            TryMove(direction);
            yield return new WaitForSeconds(0.4f);
        }

        moveQueue.Clear();
        isExecuting = false;
    }
    private void Start()
    {
        UpdateWorldPosition();
    }

    private void Update()
    {
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

        if (keyboard.enterKey.wasPressedThisFrame &&
            moveQueue.Count == 3)
        {
            StartCoroutine(ExecuteMoves());
        }
    }

    public bool TryMove(Vector2Int direction)
    {
        Vector2Int next = gridPosition + direction;

        // 전장 밖으로 이동하려 하면 현재 위치를 유지합니다.
        if (next.x < 0 || next.x >= width ||
            next.y < 0 || next.y >= height)
        {
            return false;
        }

        gridPosition = next;
        UpdateWorldPosition();
        return true;
    }

    private void UpdateWorldPosition()
    {
        transform.position = new Vector3(
            gridPosition.x * cellSize,
            0f,
            gridPosition.y * cellSize
        );
    }
}