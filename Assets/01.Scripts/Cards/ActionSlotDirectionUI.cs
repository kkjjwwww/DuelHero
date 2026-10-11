using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DuelHero.Battle;

namespace DuelHero.Cards
{
    public sealed class ActionSlotDirectionUI : MonoBehaviour
    {
        [SerializeField] private CardReservationQueue queue;
        [SerializeField, Range(0, 2)] private int slot;
        [SerializeField] private GameObject arrowRoot;
        // Right, up, left, down.
        [SerializeField] private Button[] buttons = new Button[4];
        [SerializeField] private Color normalColor = new Color(.18f, .18f, .18f, .9f);
        [SerializeField] private Color selectedColor = new Color(.75f, .75f, .75f, 1f);
        private BattlePreviewPresenter preview;
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        private readonly UnityAction[] listeners = new UnityAction[4];
        private void OnEnable()
        {
            preview = GetComponent<ActionSlotPreviewHover>()?.Preview;
            if (queue == null) return;
            queue.Changed += Refresh;
            for (int i = 0; i < buttons.Length && i < Directions.Length; i++)
            {
                int index = i;
                listeners[i] = () => queue.TrySetDirection(slot, Directions[index]);
                if (buttons[i] == null) continue;
                buttons[i].onClick.AddListener(listeners[i]);
                var hover = buttons[i].GetComponent<DirectionArrowPreviewHover>();
                if (hover == null) hover = buttons[i].gameObject.AddComponent<DirectionArrowPreviewHover>();
                hover.Configure(this, index);
            }
            Refresh();
        }
        private void OnDisable()
        {
            if (queue != null) queue.Changed -= Refresh;
            for (int i = 0; i < buttons.Length && i < Directions.Length; i++)
                preview?.ClearDirection(slot, Directions[i]);
            for (int i = 0; i < buttons.Length && i < listeners.Length; i++)
                if (buttons[i] != null && listeners[i] != null) buttons[i].onClick.RemoveListener(listeners[i]);
        }
        internal void PreviewDirection(int index)
        {
            if (preview == null || queue == null || queue.IsExecuting || index < 0 || index >= Directions.Length ||
                index >= buttons.Length || buttons[index] == null || !buttons[index].interactable) return;
            preview.FocusDirection(slot, Directions[index]);
        }
        internal void ClearPreviewDirection(int index)
        {
            if (index >= 0 && index < Directions.Length) preview?.ClearDirection(slot, Directions[index]);
        }
        public void Refresh()
        {
            var card = queue != null && slot < queue.Reservations.Count ? queue.Reservations[slot] : null;
            bool visible = card != null && CardDirectionRules.CanSelect(card.Definition);
            if (arrowRoot != null) arrowRoot.SetActive(visible);
            for (int i = 0; i < buttons.Length && i < Directions.Length; i++)
            {
                var button = buttons[i];
                if (button == null) continue;
                button.interactable = visible && !queue.IsExecuting;
                button.GetComponent<ButtonHoverScale>()?.RefreshInteractability();
                if (button.targetGraphic != null) button.targetGraphic.color = visible && card.SelectedDirection == Directions[i] ? selectedColor : normalColor;
                var label = button.GetComponentInChildren<Text>();
                if (label != null) label.color = visible && card.SelectedDirection == Directions[i] ? Color.black : Color.white;
            }
        }
    }
}
