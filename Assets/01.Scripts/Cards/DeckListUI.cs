using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DuelHero.Battle;

namespace DuelHero.Cards
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasGroup))]
    public class DeckListUI : MonoBehaviour
    {
        [SerializeField] private PlayerDeck playerDeck;
        [SerializeField] private CardReservationQueue reservationQueue;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text detailLabel;
        [SerializeField] private BattlePreviewPresenter battlePreview;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private float minimumCardWidth = 132f;
        [SerializeField] private float cardHeight = 184.38f;
        [Header("Effect Value Colors")]
        [SerializeField] private Color damageColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] private Color guardColor = new Color(0.45f, 0.7f, 1f);
        [SerializeField] private Color energyRestoreColor = new Color(0.45f, 0.9f, 0.6f);
        [SerializeField] private Color moveColor = new Color(1f, 0.85f, 0.4f);
        private readonly List<CardInstance> deck = new();
        private readonly List<(CardInstance card, CardView view)> views = new();
        private Font font;
        private CanvasGroup panelVisibility;
        public IReadOnlyList<CardInstance> Deck => deck;
        public string SelectedCategory { get; private set; }
        public event Action ViewChanged;

        private void OnEnable()
        {
            panelVisibility = GetComponent<CanvasGroup>();
            RefreshVisibility();
            if (playerDeck != null) playerDeck.Changed += Rebuild;
            if (reservationQueue != null) reservationQueue.Changed += RefreshStates;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorApplication.delayCall += EditorRebuild; return; }
#endif
            Rebuild();
        }
        private void OnDisable()
        {
            if (playerDeck != null) playerDeck.Changed -= Rebuild;
            if (reservationQueue != null) reservationQueue.Changed -= RefreshStates;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= EditorRebuild;
#endif
        }
#if UNITY_EDITOR
        private void EditorRebuild() { if (this != null && isActiveAndEnabled) Rebuild(); }
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= EditorRebuild;
            UnityEditor.EditorApplication.delayCall += EditorRebuild;
        }
#endif
        public int CountCategory(string category) => playerDeck == null ? 0 : playerDeck.Cards.Count(c => c.Definition.actionType == category);
        public void ToggleCategory(string category)
        {
            if (!new[] { "공격", "이동", "방어", "회복", "특수" }.Contains(category)) return;
            SelectedCategory = SelectedCategory == category ? null : category;
            RefreshVisibility();
            Rebuild();
            var scroll = content == null ? null : content.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.horizontalNormalizedPosition = 0;
        }
        private void RefreshVisibility()
        {
            if (panelVisibility == null) panelVisibility = GetComponent<CanvasGroup>();
            if (panelVisibility == null) return;
            bool visible = SelectedCategory != null;
            panelVisibility.alpha = visible ? 1 : 0;
            panelVisibility.interactable = visible;
            panelVisibility.blocksRaycasts = visible;
        }
        [ContextMenu("Rebuild deck preview")]
        public void Rebuild()
        {
            if (playerDeck == null || content == null) return;
            Vector2 oldPosition = content.anchoredPosition;
            playerDeck.Changed -= Rebuild;
            playerDeck.Initialize();
            if (isActiveAndEnabled) playerDeck.Changed += Rebuild;
            deck.Clear(); deck.AddRange(playerDeck.Cards); views.Clear();
            foreach (Transform child in content.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 16);
            RefreshVisibility();
            foreach (var instance in deck.Where(c => SelectedCategory != null && c.Definition.actionType == SelectedCategory)
                .OrderByDescending(c => c.Definition.isBasicAction).ThenBy(c => c.Definition.actionType)) CreateCard(content, instance);
            RefreshStates(); Canvas.ForceUpdateCanvases(); content.anchoredPosition = oldPosition;
            if (detailLabel != null) { detailLabel.font = font; detailLabel.text = "카드에 마우스를 올리면 설명과 상태를 확인할 수 있습니다."; }
            ViewChanged?.Invoke();
        }
        private string UnavailableReason(CardInstance instance)
        {
            if (reservationQueue == null) return "예약 관리자 미연결";
            if (reservationQueue.IsExecuting) return "행동 실행 중";
            if (instance.ReservedSlot >= 0) return "예약 슬롯 " + (instance.ReservedSlot + 1);
            int cooldown = instance.RemainingCooldown(reservationQueue.CurrentRound);
            if (cooldown > 0) return "재사용 대기 " + cooldown + "턴";
            if (reservationQueue.Reservations.Count >= 3) return "행동 슬롯 가득 참";
            return null;
        }
        private void RefreshStates()
        {
            foreach (var view in views)
            {
                string reason = UnavailableReason(view.card);
                int cooldown = reservationQueue == null ? 0 : view.card.RemainingCooldown(reservationQueue.CurrentRound);
                view.view.SetState(reason ?? "사용 가능 · 미예약", cooldown, reason == null);
            }
        }
        private void CreateCard(Transform parent, CardInstance instance)
        {
            if (cardPrefab == null) return;
            var card = instance.Definition;
            CardView view = Instantiate(cardPrefab, parent, false);
            view.name = "Card_" + card.id;
            LayoutElement layout = view.GetComponent<LayoutElement>();
            layout.preferredWidth = minimumCardWidth;
            layout.preferredHeight = cardHeight;
            view.SetFont(font);
            view.Bind(card, playerDeck.Database.keywords);
            views.Add((instance, view));
            view.SetClickAction(() => { if (Application.isPlaying && reservationQueue != null) reservationQueue.TryReserve(instance); });
            var hover = view.gameObject.GetComponent<CardHoverInfo>() ?? view.gameObject.AddComponent<CardHoverInfo>();
            Action showCardInfo = () =>
            {
                if (Application.isPlaying) battlePreview?.FocusCard(instance);
                if (detailLabel == null) return;
                string reason = UnavailableReason(instance);
                detailLabel.text = card.name + " — " + card.ResolvedDescription() + (reason == null ? "" : " · " + reason);
            };
            hover.ShowInfo = showCardInfo;
            hover.HideInfo = () => { if (Application.isPlaying) battlePreview?.ClearCard(instance); };
            view.SetKeywordHoverActions(
                keyword => { if (detailLabel != null) detailLabel.text = $"[{keyword.name}] — {keyword.description}"; },
                showCardInfo);
        }
    }
}
