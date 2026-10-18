using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Battle
{
    // Shows the net energy change after reserved cards and the hovered candidate resolve in order.
    public sealed class EnergyCostPreviewUI : MonoBehaviour
    {
        [SerializeField] private BattlePreviewPresenter preview;
        [SerializeField] private Image costSegment;
        [SerializeField] private Image recoverySegment;
        [SerializeField] private Color costColor = new Color(.95f, .95f, .95f, .75f);
        [SerializeField] private Color recoveryColor = new Color(.65f, .65f, .65f, .9f);
        [SerializeField, Range(0f, 1f)] private float dimAlpha = .12f;
        [SerializeField, Min(.1f)] private float pulseHalfPeriod = .4f;
        private SegmentView costView;
        private SegmentView recoveryView;

        internal sealed class SegmentView
        {
            private readonly Image image;
            private Tween pulse;
            private float shownMin = -1, shownMax = -1, shownMaximum = -1;

            public SegmentView(Image image) { this.image = image; }

            public void Show(float min, float max, float maximum,
                Color color, float dimAlpha, float pulseHalfPeriod)
            {
                if (image == null) return;
                if (max <= min || maximum <= 0) { Hide(); return; }
                if (image.gameObject.activeSelf && shownMin == min && shownMax == max &&
                    shownMaximum == maximum) return;
                pulse?.Kill(); pulse = null;
                shownMin = min; shownMax = max; shownMaximum = maximum;
                SetRange(image, min, max, maximum);
                image.color = color;
                pulse = image.DOFade(dimAlpha, pulseHalfPeriod).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }

            public void Hide()
            {
                pulse?.Kill(); pulse = null;
                shownMin = shownMax = shownMaximum = -1;
                if (image != null) image.gameObject.SetActive(false);
            }
            public static void SetRange(Image image, float min, float max, float maximum)
            {
                if (image == null) return;
                image.gameObject.SetActive(max > min && maximum > 0);
                if (!image.gameObject.activeSelf) return;
                image.type = Image.Type.Simple;
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(min / maximum, 0f);
                rect.anchorMax = new Vector2(max / maximum, 1f);
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                image.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            costView = new SegmentView(costSegment);
            recoveryView = new SegmentView(recoverySegment);
            if (preview != null) preview.EnergyPreviewChanged += Show;
            Show(preview == null ? null : preview.EnergyPreview);
        }
        private void OnDisable()
        {
            if (preview != null) preview.EnergyPreviewChanged -= Show;
            costView?.Hide();
            recoveryView?.Hide();
        }
        public void Show(TurnEnergyPreview result)
        {
            if (result == null || result.MaxEnergy <= 0)
            { costView?.Hide(); recoveryView?.Hide(); return; }
            if (result.IsRecovery)
            {
                costView?.Hide();
                recoveryView?.Show(result.SegmentMin, result.SegmentMax, result.MaxEnergy,
                    recoveryColor, dimAlpha, pulseHalfPeriod);
            }
            else
            {
                recoveryView?.Hide();
                costView?.Show(result.SegmentMin, result.SegmentMax, result.MaxEnergy,
                    costColor, dimAlpha, pulseHalfPeriod);
            }
        }
    }
}
