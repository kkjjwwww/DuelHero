using System;
using UnityEngine;
using UnityEngine.UI;
namespace DuelHero.Battle
{
    public sealed class BattleResultUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Text actionLabel;
        public event Action ActionRequested;
        private void OnEnable()
        {
            if (restartButton != null) restartButton.onClick.AddListener(RequestAction);
        }
        private void OnDisable()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(RequestAction);
        }
        public void Show(BattleOutcome outcome, string buttonText, bool actionEnabled)
        {
            bool visible = outcome != BattleOutcome.Running;
            if (panel != null)
            {
                panel.alpha = visible ? 1 : 0;
                panel.interactable = visible;
                panel.blocksRaycasts = visible;
                if (visible) panel.transform.SetAsLastSibling();
            }
            if (resultLabel != null) resultLabel.text = outcome == BattleOutcome.Victory ? "승리" : "패배";
            if (actionLabel != null) actionLabel.text = buttonText;
            if (restartButton != null) restartButton.interactable = visible && actionEnabled;
        }
        private void RequestAction() => ActionRequested?.Invoke();
    }
}

