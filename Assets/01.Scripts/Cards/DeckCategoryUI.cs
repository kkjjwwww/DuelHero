using System;
using UnityEngine;
using UnityEngine.UI;
namespace DuelHero.Cards
{
    [ExecuteAlways]
    public sealed class DeckCategoryUI : MonoBehaviour
    {
        [SerializeField] private DeckListUI deckList;
        [SerializeField] private Button[] buttons = new Button[5];
        [SerializeField] private PlayerDeck playerDeck;
        [SerializeField] private SpriteNumberUI[] counts = new SpriteNumberUI[5];
        [SerializeField] private Button centerButton;
        [SerializeField] private SpriteNumberUI totalCount;
        [SerializeField] private EnemyCardListUI allCardList;
        [SerializeField, Min(0f)] private float selectedOutlineWidth = 1.5f;
        private readonly int[] categoryCounts = new int[5];
        private static readonly string[] Categories = { "공격", "이동", "방어", "회복", "특수" };
        private readonly UnityEngine.Events.UnityAction[] listeners = new UnityEngine.Events.UnityAction[5];
        private void OnEnable()
        {
            if (deckList == null) return;
            deckList.ViewChanged += Refresh;
            if (allCardList != null) allCardList.OpenChanged += Refresh;
            if (centerButton != null) centerButton.onClick.AddListener(ToggleAllCards);
            for (int i = 0; i < buttons.Length && i < Categories.Length; i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                listeners[i] = () => deckList.ToggleCategory(Categories[index]);
                buttons[i].onClick.AddListener(listeners[i]);
            }
            Refresh();
        }
        private void OnDisable()
        {
            if (deckList != null) deckList.ViewChanged -= Refresh;
            if (allCardList != null) allCardList.OpenChanged -= Refresh;
            if (centerButton != null) centerButton.onClick.RemoveListener(ToggleAllCards);
            for (int i = 0; i < buttons.Length && i < listeners.Length; i++) if (buttons[i] != null && listeners[i] != null) buttons[i].onClick.RemoveListener(listeners[i]);
        }
        public void Refresh()
        {
            if (deckList == null) return;
            Array.Clear(categoryCounts, 0, categoryCounts.Length);
            if (playerDeck != null)
                foreach (var card in playerDeck.Cards)
                {
                    int index = Array.IndexOf(Categories, card.Definition.actionType);
                    if (index >= 0) categoryCounts[index]++;
                }
            for (int i = 0; i < buttons.Length && i < Categories.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].interactable = !deckList.IsInputLocked;
                    buttons[i].targetGraphic.color = deckList.SelectedCategory == Categories[i] ? Color.white : new Color(.8f, .8f, .8f, 1f);
                    RefreshButton(buttons[i], deckList.SelectedCategory == Categories[i]);
                }
                if (i < counts.Length && counts[i] != null) counts[i].SetValue(categoryCounts[i]);
            }
            if (totalCount != null) totalCount.SetValue(playerDeck == null ? 0 : playerDeck.Cards.Count);
            if (centerButton != null) centerButton.interactable = !deckList.IsInputLocked;
            if (deckList.IsInputLocked && allCardList != null && allCardList.IsOpen) allCardList.Close();
            if (centerButton != null) RefreshButton(centerButton, allCardList != null && allCardList.IsOpen);
        }
        private void RefreshButton(Button button, bool selected)
        {
            if (!Application.isPlaying) return;
            var scale = button.GetComponent<ButtonHoverScale>();
            if (scale != null) { scale.SetSelected(selected); scale.RefreshInteractability(); }
            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(selectedOutlineWidth, -selectedOutlineWidth);
                outline.enabled = selected;
            }
            if (!button.IsInteractable()) button.GetComponent<DelayedTooltipTrigger>()?.Cancel();
        }
        private void ToggleAllCards()
        {
            if (!deckList.IsInputLocked && allCardList != null) allCardList.Toggle();
        }
    }
}
