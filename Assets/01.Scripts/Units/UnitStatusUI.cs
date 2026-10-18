using UnityEngine;
using UnityEngine.UI;
using DuelHero.Battle;
namespace DuelHero.Units
{
    public sealed class UnitStatusUI : MonoBehaviour
    {
        [SerializeField] private UnitStats source;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image energyFill;
        [SerializeField] private Text healthLabel;
        [SerializeField] private Text energyLabel;
        [SerializeField] private Image guardFill;
        [SerializeField] private Image guardPreviewSegment;
        [SerializeField] private BattlePreviewPresenter preview;
        private EnergyCostPreviewUI.SegmentView guardPreviewView;
        private CardPreviewResult guardPreview;
        private IUnitStatus status;
        private void OnEnable()
        {
            status = source;
            guardPreviewView = new EnergyCostPreviewUI.SegmentView(guardPreviewSegment);
            if (preview != null) preview.GuardPreviewChanged += ShowGuardPreview;
            guardPreview = preview == null ? null : preview.GuardPreview;
            if (status != null) status.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (status != null) status.Changed -= Refresh;
            if (preview != null) preview.GuardPreviewChanged -= ShowGuardPreview;
            guardPreviewView?.Hide();
            guardPreview = null;
            status = null;
        }
        public void Refresh()
        {
            bool ready = status != null && status.IsInitialized;
            int health = ready ? status.Health : 0, shield = ready ? status.GuardReduction : 0;
            int gain = ready && guardPreview != null ? guardPreview.GuardPreviewGain : 0;
            float shieldEnd = (float)health + shield;
            float maximum = ready ? Mathf.Max(status.MaxHealth, shieldEnd + gain) : 0;
            if (healthLabel != null) healthLabel.text = ready ? $"체력 {status.Health} / {status.MaxHealth}" +
                (shield > 0 || gain > 0 ? $" · 보호막 {(long)shield + gain}" : "") : "체력 --";
            if (energyLabel != null) energyLabel.text = ready ? $"에너지 {status.Energy} / {status.MaxEnergy}" : "에너지 --";
            EnergyCostPreviewUI.SegmentView.SetRange(healthFill, 0, health, maximum);
            if (energyFill != null) energyFill.fillAmount = ready && status.MaxEnergy > 0 ? (float)status.Energy / status.MaxEnergy : 0;
            EnergyCostPreviewUI.SegmentView.SetRange(guardFill, health, shieldEnd, maximum);
            if (gain > 0)
                guardPreviewView?.Show(shieldEnd, shieldEnd + gain, maximum,
                    new Color(.15f, .65f, 1f, .85f), .12f, .4f);
            else guardPreviewView?.Hide();
        }
        private void ShowGuardPreview(CardPreviewResult result) { guardPreview = result; Refresh(); }
    }
}
