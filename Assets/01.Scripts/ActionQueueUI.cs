using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionQueueUI : MonoBehaviour
{
    [SerializeField] private Text[] actionLabels = new Text[3];
    [SerializeField] private Image[] slotImages = new Image[3];

    private readonly Color normalColor = new Color(0.16f, 0.16f, 0.16f, 0.65f);

    private readonly Color executingColor = new Color(0.55f, 0.55f, 0.55f, 0.80f);

    public void Refresh(IReadOnlyList<Vector2Int> queue, int activeIndex = -1)
    {
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
