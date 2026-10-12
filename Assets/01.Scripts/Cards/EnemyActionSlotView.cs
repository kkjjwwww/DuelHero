using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    // Displays one action; never generates plans or executes effects.
    public sealed class EnemyActionSlotView : MonoBehaviour
    {
        [SerializeField] private GameObject frontFace;
        [SerializeField] private GameObject backFace;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text costLabel;
        [SerializeField] private Text effectLabel;
        [SerializeField] private CanvasGroup cardCanvasGroup;
        [SerializeField] private GameObject executionHighlight;
        [SerializeField, Range(0f, 1f)] private float completedAlpha = 0.45f;
        private CardDefinition card;
        public bool IsRevealed { get; private set; }
        public bool IsExecuting { get; private set; }
        public bool IsCompleted { get; private set; }

        public void Bind(CardDefinition value)
        {
            card = value;
            IsRevealed = IsExecuting = IsCompleted = false;
            nameLabel.text = card?.name ?? "대기";
            costLabel.text = card == null ? "" : $"에너지 {card.energyCost}";
            effectLabel.text = card?.ResolvedDescription() ?? "";
            RefreshAppearance();
        }
        public void Reveal()
        {
            IsRevealed = true;
            RefreshAppearance();
        }
        public void BeginExecution()
        {
            IsRevealed = IsExecuting = true;
            IsCompleted = false;
            RefreshAppearance();
        }
        public void CompleteExecution()
        {
            IsExecuting = false;
            IsCompleted = true;
            RefreshAppearance();
        }
        private void RefreshAppearance()
        {
            frontFace.SetActive(IsRevealed);
            backFace.SetActive(!IsRevealed);
            executionHighlight.SetActive(IsExecuting);
            cardCanvasGroup.alpha = IsCompleted ? completedAlpha : 1f;
        }
    }
}
