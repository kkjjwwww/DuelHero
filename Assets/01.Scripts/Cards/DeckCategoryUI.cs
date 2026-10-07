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
        [SerializeField] private Text[] labels = new Text[5];
        [SerializeField] private Text centerLabel;
        private static readonly string[] Categories = { "공격", "이동", "방어", "회복", "특수" };
        private readonly UnityEngine.Events.UnityAction[] listeners = new UnityEngine.Events.UnityAction[5];
        private void OnEnable()
        {
            if (deckList == null) return;
            deckList.ViewChanged += Refresh;
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
            for (int i = 0; i < buttons.Length && i < listeners.Length; i++) if (buttons[i] != null && listeners[i] != null) buttons[i].onClick.RemoveListener(listeners[i]);
        }
        public void Refresh()
        {
            if (deckList == null) return;
            for (int i = 0; i < buttons.Length && i < Categories.Length; i++)
            {
                if (buttons[i] != null) buttons[i].targetGraphic.color = deckList.SelectedCategory == Categories[i] ? new Color(0.65f, 0.65f, 0.65f, 0.95f) : new Color(0.16f, 0.16f, 0.16f, 0.85f);
                if (i < labels.Length && labels[i] != null) labels[i].text = Categories[i] + "\n" + deckList.CountCategory(Categories[i]);
            }
            if (centerLabel != null) centerLabel.text = deckList.SelectedCategory == null ? "카테고리\n선택" : deckList.SelectedCategory + "\n" + deckList.CountCategory(deckList.SelectedCategory);
        }
    }
}
