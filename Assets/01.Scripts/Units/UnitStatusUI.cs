using UnityEngine;
using UnityEngine.UI;
namespace DuelHero.Units
{
    public sealed class UnitStatusUI : MonoBehaviour
    {
        [SerializeField] private UnitStats source;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image energyFill;
        [SerializeField] private Text healthLabel;
        [SerializeField] private Text energyLabel;
        private IUnitStatus status;
        private void OnEnable()
        {
            status = source;
            if (status != null) status.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (status != null) status.Changed -= Refresh;
            status = null;
        }
        public void Refresh()
        {
            bool ready = status != null && status.IsInitialized;
            if (healthLabel != null) healthLabel.text = ready ? $"체력 {status.Health} / {status.MaxHealth}" : "체력 --";
            if (energyLabel != null) energyLabel.text = ready ? $"에너지 {status.Energy} / {status.MaxEnergy}" : "에너지 --";
            if (healthFill != null) healthFill.fillAmount = ready && status.MaxHealth > 0 ? (float)status.Health / status.MaxHealth : 0;
            if (energyFill != null) energyFill.fillAmount = ready && status.MaxEnergy > 0 ? (float)status.Energy / status.MaxEnergy : 0;
        }
    }
}
