using UnityEngine;
using UnityEngine.UI;
namespace DuelHero.Battle
{
    public sealed class BattleResultUI : MonoBehaviour
    {
        [SerializeField] private BattleTurnController source;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private BattleRestartController restartController;
        private void OnEnable()
        {
            if (source != null) source.BattleFinished += Show;
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            Show(source == null ? BattleOutcome.Running : source.Outcome);
        }
        private void OnDisable()
        {
            if (source != null) source.BattleFinished -= Show;
            if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        }
        private void Show(BattleOutcome outcome)
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
            if (restartButton != null) restartButton.interactable = visible && restartController != null;
        }
        private void Restart()
        {
            if (restartController == null) return;
            if (restartButton != null) restartButton.interactable = false;
            restartController.Restart();
        }
    }
}

