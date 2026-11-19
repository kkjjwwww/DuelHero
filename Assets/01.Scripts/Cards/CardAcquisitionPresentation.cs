using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class CardAcquisitionPresentation : MonoBehaviour
    {
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Vector2 cardSize = new(132f, 184.38f);
        [SerializeField] private Vector2 centerAnchor = new(.5f, .5f);
        [SerializeField] private Vector2 centerOffset;
        [SerializeField] private Vector2 destinationAnchor = new(.5f, 0f);
        [SerializeField] private Vector2 destinationOffset = new(0f, -60f);
        [SerializeField, Min(.01f)] private float centerScale = 2.5f;
        [SerializeField, Min(0f)] private float destinationScale = .08f;
        [SerializeField, Min(.01f)] private float moveDuration = .3f;
        [SerializeField, Min(0f)] private float holdDuration = .2f;
        [SerializeField, Min(.01f)] private float collectDuration = .5f;
        [SerializeField] private float rotationDegrees = 360f;
        [SerializeField, Min(0f)] private float fadeDuration = .15f;

        private readonly Vector3[] sourceCorners = new Vector3[4];
        private Sequence motion;
        private GameObject overlay;
        public bool IsPlaying => motion != null;

        // Completion is called only after a full animation. Cancel removes the visual without completing acquisition again.
        public bool Play(CardView source, CardDefinition definition, CardDatabase database, Action onCompleted = null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (!isActiveAndEnabled || IsPlaying || source == null || cardPrefab == null ||
                definition == null || database == null || canvas == null) return false;
            canvas = canvas.rootCanvas;
            var sourceCanvas = source.GetComponentInParent<Canvas>();
            if (sourceCanvas == null) return false;
            Camera sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
            Camera destinationCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var canvasRect = (RectTransform)canvas.transform;
            source.VisualRoot.GetWorldCorners(sourceCorners);
            var points = new Vector2[4];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, sourceCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, destinationCamera, out points[i]))
                    return false;
            }

            overlay = new GameObject("Card Acquisition", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var root = (RectTransform)overlay.transform;
            root.SetParent(canvas.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.pivot = canvasRect.pivot;
            var overlayCanvas = overlay.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = short.MaxValue - 2;
            var group = overlay.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var copy = Instantiate(cardPrefab, root, false);
            copy.Bind(definition, database.keywords);
            copy.SetFrontVisible(true);
            copy.SetState(string.Empty, 0, false);
            copy.SetClickAction(null);
            foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = canvasRect.pivot;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = cardSize;
            rect.anchoredPosition = (points[0] + points[2]) * .5f;
            rect.localScale = new Vector3(Vector2.Distance(points[0], points[3]) / Mathf.Max(1f, cardSize.x),
                Vector2.Distance(points[0], points[1]) / Mathf.Max(1f, cardSize.y), 1f);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(points[3].y - points[0].y, points[3].x - points[0].x) * Mathf.Rad2Deg);

            Vector2 center = canvasRect.rect.min + Vector2.Scale(canvasRect.rect.size, centerAnchor) + centerOffset;
            Vector2 destination = canvasRect.rect.min + Vector2.Scale(canvasRect.rect.size, destinationAnchor) + destinationOffset;
            float duration = Mathf.Max(.01f, collectDuration);
            float fade = Mathf.Clamp(fadeDuration, 0f, duration);
            motion = DOTween.Sequence().SetUpdate(true);
            motion.Append(rect.DOAnchorPos(center, Mathf.Max(.01f, moveDuration)).SetEase(Ease.OutCubic));
            motion.Join(rect.DOScale(centerScale, Mathf.Max(.01f, moveDuration)).SetEase(Ease.OutCubic));
            motion.Join(rect.DOLocalRotate(Vector3.zero, Mathf.Max(.01f, moveDuration)));
            motion.AppendInterval(Mathf.Max(0f, holdDuration));
            float collectStart = motion.Duration();
            motion.Append(rect.DOAnchorPos(destination, duration).SetEase(Ease.InCubic));
            motion.Join(rect.DOScale(destinationScale, duration).SetEase(Ease.InCubic));
            motion.Join(rect.DOLocalRotate(new Vector3(0, 0, rotationDegrees), duration, RotateMode.FastBeyond360).SetEase(Ease.InOutSine));
            motion.Insert(collectStart + duration - fade, group.DOFade(0f, fade));
            motion.OnComplete(() =>
            {
                motion = null;
                RemoveOverlay();
                onCompleted?.Invoke();
            });
            return true;
        }

        public void Cancel()
        {
            motion?.Kill();
            motion = null;
            RemoveOverlay();
        }

        private void OnDisable() => Cancel();

        private void RemoveOverlay()
        {
            if (overlay == null) return;
            overlay.SetActive(false);
            Destroy(overlay);
            overlay = null;
        }
    }
}
