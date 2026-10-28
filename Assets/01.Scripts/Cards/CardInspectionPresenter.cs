using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace DuelHero.Cards
{
    // One cached inspection card per canvas, shared by slots and card lists.
    public sealed class CardInspectionPresenter : MonoBehaviour, IScrollHandler
    {
        private Canvas canvas;
        private RectTransform owner;
        private CardView view;
        private CardDefinition definition;
        private CardDatabase database;
        private bool informationMasked;
        private KeywordTooltip tooltip;
        private Action onClick;
        private Action onHidden;
        private Action<PointerEventData> onScroll;
        private readonly Vector3[] corners = new Vector3[4];
        public CardView View => view;
        public RectTransform Owner => owner;
        public bool IsVisible => view != null && view.gameObject.activeSelf;

        public static CardInspectionPresenter GetOrCreate(Canvas canvas)
        {
            canvas = canvas.rootCanvas;
            var existing = canvas.GetComponentInChildren<CardInspectionPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("CardInspectionPresenter", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var presenter = go.AddComponent<CardInspectionPresenter>();
            presenter.canvas = canvas;
            return presenter;
        }

        public void Show(RectTransform source, CardView prefab, CardDefinition card, CardDatabase data,
            Font font, int cooldown = 0, Action click = null, float scale = 1.55f, float duration = .15f,
            Action<PointerEventData> scroll = null, Action hidden = null, bool typeOnly = false)
        {
            if (source == null || prefab == null || card == null || data == null) return;
            if (owner == source && IsVisible && definition == card && database == data && informationMasked == typeOnly) return;
            Hide();
            if (view == null)
            {
                view = Instantiate(prefab, transform, false);
                view.name = "CardInspection";
                view.SetFont(font);
                view.SetClickAction(() =>
                {
                    if (onClick == null) return;
                    var action = onClick;
                    Hide();
                    action();
                });
                view.SetKeywordHoverActions((keyword, position) =>
                {
                    if (tooltip == null) tooltip = KeywordTooltip.Create(canvas, font);
                    tooltip.Show(keyword, position);
                }, () => { if (tooltip != null) tooltip.Hide(); },
                position => { if (tooltip != null) tooltip.Move(position); });
            }
            owner = source;
            onClick = click;
            onScroll = scroll;
            onHidden = hidden;
            if (definition != card || database != data || informationMasked != typeOnly)
            {
                view.Bind(card, data.keywords, typeOnly);
                definition = card;
                database = data;
                informationMasked = typeOnly;
            }
            view.SetFrontVisible(true);
            view.SetState(string.Empty, cooldown, true);
            view.gameObject.SetActive(true);
            var rect = (RectTransform)view.transform;
            var canvasRect = (RectTransform)canvas.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            source.GetWorldCorners(corners);
            Vector3 bottom = canvasRect.InverseTransformPoint((corners[0] + corners[3]) * .5f);
            Vector2 half = rect.rect.size * scale * .5f;
            Rect bounds = canvasRect.rect;
            rect.anchoredPosition = new Vector2(
                ClampToBounds(bottom.x, bounds.xMin + half.x + 8f, bounds.xMax - half.x - 8f),
                ClampToBounds(bottom.y - half.y + 10f, bounds.yMin + half.y + 8f, bounds.yMax - half.y - 8f));
            rect.localRotation = Quaternion.identity;
            transform.SetAsLastSibling();
            rect.SetAsLastSibling();
            rect.localScale = Vector3.one * scale * .9f;
            if (duration <= 0f) rect.localScale = Vector3.one * scale;
            else rect.DOScale(scale, duration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private static float ClampToBounds(float value, float min, float max) =>
            max < min ? (min + max) * .5f : Mathf.Clamp(value, min, max);

        public void RefreshState(RectTransform source, int cooldown)
        {
            if (owner == source && IsVisible) view.SetState(string.Empty, cooldown, true);
        }

        public bool ContainsPointer(Vector2 position)
        {
            if (!IsVisible || owner == null) return false;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(owner, position, camera) ||
                RectTransformUtility.RectangleContainsScreenPoint((RectTransform)view.transform, position, camera);
        }

        private void LateUpdate()
        {
            if (!IsVisible) return;
            if (owner == null || !owner.gameObject.activeInHierarchy ||
                (Mouse.current != null && !ContainsPointer(Mouse.current.position.ReadValue()))) Hide();
        }

        public void Hide(RectTransform source)
        {
            if (owner == source) Hide();
        }
        public void OnScroll(PointerEventData eventData)
        {
            var action = onScroll;
            Hide();
            action?.Invoke(eventData);
        }
        public void Hide()
        {
            var hidden = onHidden;
            onHidden = null;
            owner = null;
            onClick = null;
            onScroll = null;
            if (view != null)
            {
                view.transform.DOKill();
                view.gameObject.SetActive(false);
            }
            if (tooltip != null) tooltip.Hide();
            hidden?.Invoke();
        }
        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            if (view != null) Destroy(view.gameObject);
            if (tooltip != null) Destroy(tooltip.gameObject);
        }
    }
}
