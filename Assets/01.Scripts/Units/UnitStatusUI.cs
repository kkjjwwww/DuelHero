using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using DuelHero.Battle;
namespace DuelHero.Units
{
    public sealed class UnitStatusUI : MonoBehaviour
    {
        [SerializeField] private UnitStats source;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image damageTrailFill;
        [SerializeField] private Image frameFlash;
        [SerializeField] private Image energyFill;
        [SerializeField] private Text healthLabel;
        [SerializeField] private Text energyLabel;
        [SerializeField] private Image guardFill;
        [SerializeField] private Image guardPreviewSegment;
        [SerializeField] private BattlePreviewPresenter preview;
        [SerializeField] private bool fillFromRight;
        private EnergyCostPreviewUI.SegmentView guardPreviewView;
        private CardPreviewResult guardPreview;
        private IUnitStatus status;
        private Sequence healthMotion;
        private Sequence impactMotion;
        private Tween energyMotion;
        private float targetEnergy;
        private bool energyShown;
        private StatusBarHover healthHover, energyHover;
        private RectTransform impactRoot;
        private Vector2 restPosition;
        private Vector3 restScale;
        private Color restFillColor;
        private bool healthShown;
        private int targetHealth, shownShield, previewGain;
        private float displayedHealth, trailHealth, maximum;
        private void OnEnable()
        {
            status = source;
            healthHover = StatusBarHover.Bind(healthFill, healthLabel);
            energyHover = StatusBarHover.Bind(energyFill, energyLabel);
            if (energyFill != null)
                energyFill.fillOrigin = (int)(fillFromRight ? Image.OriginHorizontal.Right : Image.OriginHorizontal.Left);
            if (frameFlash != null)
            {
                impactRoot = (RectTransform)frameFlash.transform.parent;
                restPosition = impactRoot.anchoredPosition;
                restScale = impactRoot.localScale;
                restFillColor = healthFill.color;
                source.ShieldOnlyHit += ShowShieldHit;
            }
            guardPreviewView = new EnergyCostPreviewUI.SegmentView(guardPreviewSegment);
            if (preview != null) preview.GuardPreviewChanged += ShowGuardPreview;
            guardPreview = preview == null ? null : preview.GuardPreview;
            if (status != null) status.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (status != null) status.Changed -= Refresh;
            if (impactRoot != null) source.ShieldOnlyHit -= ShowShieldHit;
            ResetImpact();
            if (preview != null) preview.GuardPreviewChanged -= ShowGuardPreview;
            guardPreviewView?.Hide();
            healthMotion?.Kill();
            healthMotion = null;
            healthShown = false;
            energyMotion?.Kill();
            energyMotion = null;
            energyShown = false;
            if (healthHover != null) healthHover.enabled = false;
            if (energyHover != null) energyHover.enabled = false;
            EnergyCostPreviewUI.SegmentView.SetRange(damageTrailFill, 0, 0, 0);
            guardPreview = null;
            status = null;
        }
        public void Refresh()
        {
            bool ready = status != null && status.IsInitialized;
            int health = ready ? status.Health : 0, shield = ready ? status.GuardReduction : 0;
            int gain = ready && guardPreview != null ? guardPreview.GuardPreviewGain : 0;
            float shieldEnd = (float)health + shield;
            maximum = ready ? Mathf.Max(status.MaxHealth, shieldEnd + gain) : 0;
            shownShield = shield;
            previewGain = gain;
            if (healthLabel != null) healthLabel.text = ready ? $"체력 {status.Health} / {status.MaxHealth}" +
                (shield > 0 || gain > 0 ? $" · 보호막 {(long)shield + gain}" : "") : "체력 --";
            if (energyLabel != null) energyLabel.text = ready ? $"에너지 {status.Energy} / {status.MaxEnergy}" : "에너지 --";
            if (ready && source != null && source.Bleed > 0 && healthLabel != null)
                healthLabel.text += $" · 출혈 {source.Bleed}";
            UpdateEnergy(ready);
            if (gain > 0)
                guardPreviewView?.Show(shieldEnd, shieldEnd + gain, maximum,
                    new Color(.15f, .65f, 1f, .85f), .12f, .4f, fillFromRight);
            else guardPreviewView?.Hide();

            if (!ready || !healthShown)
            {
                healthMotion?.Kill();
                healthMotion = null;
                displayedHealth = targetHealth = health;
                trailHealth = 0;
                healthShown = ready;
            }
            else if (health != targetHealth)
            {
                bool damage = health < targetHealth;
                ShowImpact(damage ? Color.white : new Color(.35f, 1f, .48f), damage ? 4f : 0f, damage ? 1.04f : 1f, !damage);
                healthMotion?.Kill();
                trailHealth = damage ? Mathf.Max(trailHealth, displayedHealth) : 0;
                targetHealth = health;
                healthMotion = DOTween.Sequence().SetUpdate(true);
                healthMotion.Append(DOTween.To(() => displayedHealth, value => displayedHealth = value,
                    health, damage ? .15f : .3f).SetEase(Ease.OutQuad));
                if (damage)
                    healthMotion.Insert(.2f, DOTween.To(() => trailHealth, value => trailHealth = value,
                        health, .35f).SetEase(Ease.OutQuad));
                healthMotion.OnUpdate(RenderHealth);
                healthMotion.OnComplete(() =>
                {
                    displayedHealth = health;
                    trailHealth = 0;
                    healthMotion = null;
                    RenderHealth();
                });
            }
            RenderHealth();
        }

        private void UpdateEnergy(bool ready)
        {
            if (energyFill == null) return;
            float next = ready && status.MaxEnergy > 0 ? (float)status.Energy / status.MaxEnergy : 0;
            if (!ready || !energyShown)
            {
                energyMotion?.Kill();
                energyMotion = null;
                energyFill.fillAmount = targetEnergy = next;
                energyShown = ready;
            }
            else if (next != targetEnergy)
            {
                targetEnergy = next;
                energyMotion?.Kill();
                energyMotion = energyFill.DOFillAmount(next, .25f).SetEase(Ease.OutQuad).SetUpdate(true)
                    .OnComplete(() => energyMotion = null);
            }
        }

        private void RenderHealth()
        {
            float shieldEnd = displayedHealth + shownShield;
            float scale = maximum > 0 ? Mathf.Max(maximum, shieldEnd + previewGain, trailHealth) : 0;
            EnergyCostPreviewUI.SegmentView.SetRange(damageTrailFill, displayedHealth, trailHealth, scale, fillFromRight);
            EnergyCostPreviewUI.SegmentView.SetRange(healthFill, 0, displayedHealth, scale, fillFromRight);
            EnergyCostPreviewUI.SegmentView.SetRange(guardFill, displayedHealth, shieldEnd, scale, fillFromRight);
            EnergyCostPreviewUI.SegmentView.SetRange(guardPreviewSegment, shieldEnd, shieldEnd + previewGain, scale, fillFromRight);
        }
        private void ShowGuardPreview(CardPreviewResult result) { guardPreview = result; Refresh(); }

        private void ShowShieldHit() => ShowImpact(new Color(.2f, .85f, 1f), 2f, 1f, false);

        private void ShowImpact(Color color, float shake, float scale, bool healing)
        {
            if (impactRoot == null) return;
            ResetImpact();
            frameFlash.color = new Color(color.r, color.g, color.b, 0f);
            impactMotion = DOTween.Sequence().SetUpdate(true);
            impactMotion.Append(frameFlash.DOFade(.85f, .08f));
            impactMotion.Append(frameFlash.DOFade(0f, .12f));
            if (shake > 0)
                impactMotion.Insert(0, impactRoot.DOShakeAnchorPos(.18f, new Vector2(shake, shake * .35f), 12, 30f));
            if (scale > 1f)
            {
                impactMotion.Insert(0, impactRoot.DOScale(restScale * scale, .06f));
                impactMotion.Insert(.06f, impactRoot.DOScale(restScale, .12f));
            }
            if (healing)
            {
                healthFill.color = color;
                impactMotion.Insert(0, healthFill.DOColor(restFillColor, .2f));
            }
            impactMotion.OnComplete(() => { impactMotion = null; ResetImpact(); });
        }

        private void ResetImpact()
        {
            impactMotion?.Kill();
            impactMotion = null;
            if (impactRoot == null) return;
            impactRoot.anchoredPosition = restPosition;
            impactRoot.localScale = restScale;
            healthFill.color = restFillColor;
            frameFlash.color = Color.clear;
        }
    }
}
