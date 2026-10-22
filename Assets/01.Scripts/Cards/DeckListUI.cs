using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DuelHero.Battle;
using DG.Tweening;

namespace DuelHero.Cards
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasGroup))]
    public class DeckListUI : MonoBehaviour
    {
        [SerializeField] private PlayerDeck playerDeck;
        [SerializeField] private CardReservationQueue reservationQueue;
        [SerializeField] private BattleTurnController turnController;
        [SerializeField] private RectTransform content;
        [SerializeField] private BattlePreviewPresenter battlePreview;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private float minimumCardWidth = 132f;
        [SerializeField] private float cardHeight = 184.38f;
        [Header("Category Animation")]
        [SerializeField, Min(0.05f)] private float enterDuration = 0.32f;
        [SerializeField, Min(0.05f)] private float exitDuration = 0.22f;
        [SerializeField, Min(0f)] private float exitLift = 24f;
        [SerializeField, Range(0.5f, 1f)] private float exitScale = 0.88f;
        [SerializeField, Range(0f, 45f)] private float exitTilt = 20f;

        private sealed class CachedCard
        {
            public CardInstance Instance;
            public CardView View;
        }

        private readonly List<CardInstance> deck = new();
        private readonly Dictionary<CardInstance, CachedCard> cachedCards = new();
        private readonly Stack<CachedCard> pool = new();
        private readonly List<CachedCard> visibleCards = new();
        private readonly List<CardView> visibleViews = new();
        private readonly HashSet<CardInstance> ownedCards = new();
        private readonly List<CardInstance> removedCards = new();
        private readonly List<CachedCard> outgoingCards = new();
        private Sequence categoryTween;
        private bool animateCategoryChange;
        public bool IsTransitioning { get; private set; }
        public bool IsInputLocked { get; private set; }
        private Font font;
        private CanvasGroup panelVisibility;
        private CardFanLayout fanLayout;
        private CachedCard hoveredCard;
        private KeywordTooltip keywordTooltip;
        private bool cacheInitialized;
        private bool synchronizing;
        public IReadOnlyList<CardInstance> Deck => deck;
        public string SelectedCategory { get; private set; }
        public event Action ViewChanged;

        private void OnEnable()
        {
            panelVisibility = GetComponent<CanvasGroup>();
            RefreshVisibility();
            if (turnController != null) turnController.PhaseChanged += RefreshStates;
            if (playerDeck != null)
            {
                playerDeck.CardsChanged += SynchronizeDeck;
                playerDeck.StateChanged += RefreshStates;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorApplication.delayCall += EditorRebuild; return; }
#endif
            SynchronizeDeck();
        }

        private void OnDisable()
        {
            CancelCategoryTransition();
            ClearHover();
            if (turnController != null) turnController.PhaseChanged -= RefreshStates;
            if (panelVisibility != null) panelVisibility.blocksRaycasts = panelVisibility.interactable = false;
            if (playerDeck != null)
            {
                playerDeck.CardsChanged -= SynchronizeDeck;
                playerDeck.StateChanged -= RefreshStates;
            }
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
        public int CountCategory(string category) => playerDeck == null ? 0 : 1 + playerDeck.Cards.Count(c => c.Definition.actionType == category);

        public void ToggleCategory(string category)
        {
            if (IsInputLocked) return;
            if (category != "공격" && category != "이동" && category != "방어" && category != "회복" && category != "특수") return;
            SelectedCategory = SelectedCategory == category ? null : category;
            animateCategoryChange = Application.isPlaying && isActiveAndEnabled;
            SynchronizeDeck();
        }

        private void RefreshVisibility()
        {
            if (panelVisibility == null) panelVisibility = GetComponent<CanvasGroup>();
            if (panelVisibility == null) return;
            bool visible = SelectedCategory != null || IsTransitioning;
            panelVisibility.alpha = visible ? 1 : 0;
            bool interactive = visible && !IsTransitioning && !IsInputLocked;
            panelVisibility.interactable = panelVisibility.blocksRaycasts = interactive;
            if (fanLayout != null) fanLayout.SetHoverEnabled(interactive && isActiveAndEnabled);
        }

        // Explicit content refresh for Inspector edits and sheet imports. State changes never call this.
        [ContextMenu("Refresh deck preview content")]
        public void Rebuild()
        {
            if (playerDeck == null || content == null) return;
            CancelCategoryTransition();
            ClearHover();
            foreach (var entry in cachedCards.Values)
            {
                if (entry.View == null) continue;
                entry.View.SetFont(GetFont());
                entry.View.Bind(entry.Instance.Definition, playerDeck.Database.keywords);
                ConfigureCardSize(entry.View);
            }
            SynchronizeDeck();
        }

        private void SynchronizeDeck()
        {
            if (synchronizing || playerDeck == null || content == null || cardPrefab == null) return;
            synchronizing = true;
            try
            {
                ConfigureFan();
                InitializeCache();
                bool animate = animateCategoryChange;
                animateCategoryChange = false;
                CancelCategoryTransition();
                playerDeck.Initialize();
                deck.Clear(); deck.Add(playerDeck.WaitAction); deck.AddRange(playerDeck.Cards);
                ownedCards.Clear();
                foreach (var instance in deck) ownedCards.Add(instance);
                removedCards.Clear();
                foreach (var instance in cachedCards.Keys)
                    if (!ownedCards.Contains(instance)) removedCards.Add(instance);

                ClearHover();
                fanLayout.SetCards(Array.Empty<CardView>());
                foreach (var instance in removedCards)
                {
                    var entry = cachedCards[instance];
                    entry.View.gameObject.SetActive(false);
                    entry.Instance = null;
                    cachedCards.Remove(instance);
                    pool.Push(entry);
                }
                foreach (var entry in visibleCards)
                    if (entry.View != null)
                    {
                        if (animate && entry.Instance != null) outgoingCards.Add(entry);
                        else entry.View.gameObject.SetActive(false);
                    }
                visibleCards.Clear(); visibleViews.Clear();

                foreach (var instance in deck.Where(c => SelectedCategory != null &&
                    (WaitCard.IsWait(c.Definition) || c.Definition.actionType == SelectedCategory))
                    .OrderByDescending(c => WaitCard.IsWait(c.Definition))
                    .ThenByDescending(c => c.Definition.isBasicAction))
                {
                    if (!cachedCards.TryGetValue(instance, out var entry) || outgoingCards.Contains(entry))
                    {
                        entry = AcquireCard(instance);
                        cachedCards[instance] = entry;
                    }
                    entry.View.gameObject.SetActive(true);
                    visibleCards.Add(entry);
                    visibleViews.Add(entry.View);
                }
                RefreshStates();
                fanLayout.SetCards(visibleViews);
                if (animate && (visibleViews.Count > 0 || outgoingCards.Count > 0)) StartCategoryTransition();
                RefreshVisibility();
                ViewChanged?.Invoke();
            }
            finally { synchronizing = false; }
        }

        private void InitializeCache()
        {
            if (cacheInitialized) return;
            // Recover from an Editor domain reload: previous preview objects have no runtime owners.
            foreach (Transform child in content.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            cacheInitialized = true;
        }

        private void RefreshStates()
        {
            bool locked = turnController != null && turnController.IsInputLocked;
            if (IsInputLocked != locked)
            {
                IsInputLocked = locked;
                ClearHover();
                RefreshVisibility();
                ViewChanged?.Invoke();
            }
            foreach (var entry in visibleCards)
            {
                int cooldown = reservationQueue == null ? 0 : entry.Instance.RemainingCooldown(reservationQueue.CurrentRound);
                bool interactable = reservationQueue != null && !reservationQueue.IsExecuting &&
                    reservationQueue.Reservations.Count < 3 && entry.Instance.ReservedSlot < 0 && cooldown == 0;
                entry.View.SetState(string.Empty, cooldown, interactable);
                entry.View.SetReservationSlot(entry.Instance.ReservedSlot);
            }
        }

        private CachedCard AcquireCard(CardInstance instance)
        {
            CachedCard entry;
            if (pool.Count > 0) entry = pool.Pop();
            else
            {
                entry = new CachedCard { View = Instantiate(cardPrefab, content, false) };
                ConfigureCardSize(entry.View);
                entry.View.SetFont(GetFont());
                ConfigureCallbacks(entry);
            }
            entry.Instance = instance;
            entry.View.name = "Card_" + instance.Definition.id;
            entry.View.SetFrontVisible(true);
            entry.View.Bind(instance.Definition, playerDeck.Database.keywords);
            return entry;
        }

        private void ConfigureCallbacks(CachedCard entry)
        {
            CardView view = entry.View;
            view.SetClickAction(() =>
            {
                if (Application.isPlaying && !IsInputLocked && !IsTransitioning && entry.Instance != null && reservationQueue != null)
                    reservationQueue.TryReserve(entry.Instance);
            });
            view.SetKeywordHoverActions((keyword, position) =>
            {
                if (IsInputLocked || IsTransitioning || fanLayout.Focused != view) return;
                if (keywordTooltip == null) keywordTooltip = KeywordTooltip.Create(GetComponentInParent<Canvas>().rootCanvas, GetFont());
                keywordTooltip.Show(keyword, position);
            }, () => { if (keywordTooltip != null) keywordTooltip.Hide(); },
            position => { if (keywordTooltip != null) keywordTooltip.Move(position); });
        }
        private void HandleFanFocusChanged(CardView view)
        {
            if (keywordTooltip != null) keywordTooltip.Hide();
            if (Application.isPlaying && hoveredCard?.Instance != null) battlePreview?.ClearCard(hoveredCard.Instance);
            hoveredCard = null;
            if (view == null) return;
            foreach (var entry in visibleCards)
                if (entry.View == view) { hoveredCard = entry; break; }
            if (Application.isPlaying && hoveredCard?.Instance != null) battlePreview?.FocusCard(hoveredCard.Instance);
        }

        private static CanvasGroup TransitionGroup(CardView view)
        {
            var group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        private static void ResetTransitionAppearance(CardView view)
        {
            if (view == null) return;
            view.VisualRoot.DOKill();
            view.VisualRoot.anchoredPosition = Vector2.zero;
            view.VisualRoot.localScale = Vector3.one;
            view.VisualRoot.localRotation = Quaternion.identity;
            var group = view.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }

        private void StartCategoryTransition()
        {
            IsTransitioning = true;
            categoryTween = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            foreach (var entry in outgoingCards)
            {
                var view = entry.View;
                var group = TransitionGroup(view);
                var visual = view.VisualRoot;
                // Incoming cards are ordered above this group by CardFanLayout.
                categoryTween.Insert(0f, group.DOFade(0f, exitDuration).SetEase(Ease.InQuad));
                categoryTween.Insert(0f, visual.DOAnchorPosY(exitLift, exitDuration).SetEase(Ease.InQuad));
                categoryTween.Insert(0f, visual.DOScale(exitScale, exitDuration).SetEase(Ease.InQuad));
                categoryTween.Insert(0f, visual.DOLocalRotate(new Vector3(exitTilt, 0f, 0f), exitDuration).SetEase(Ease.InQuad));
            }
            foreach (var view in visibleViews)
            {
                ResetTransitionAppearance(view);
                var group = TransitionGroup(view);
                var visual = view.VisualRoot;
                visual.anchoredPosition = Vector2.down * (cardHeight * 1.5f + 32f);
                group.alpha = 0f;
                categoryTween.Insert(0f, visual.DOAnchorPos(Vector2.zero, enterDuration).SetEase(Ease.OutCubic));
                categoryTween.Insert(0f, group.DOFade(1f, enterDuration * 0.65f).SetEase(Ease.OutQuad));
            }
            categoryTween.OnComplete(() =>
            {
                ReleaseOutgoingCards();
                foreach (var view in visibleViews) ResetTransitionAppearance(view);
                IsTransitioning = false;
                categoryTween = null;
                RefreshVisibility();
            });
        }

        private void CancelCategoryTransition()
        {
            if (!IsTransitioning && categoryTween == null) return;
            categoryTween?.Kill();
            categoryTween = null;
            ReleaseOutgoingCards();
            foreach (var view in visibleViews) ResetTransitionAppearance(view);
            IsTransitioning = false;
        }

        private void ReleaseOutgoingCards()
        {
            foreach (var entry in outgoingCards)
            {
                var view = entry.View;
                if (view == null) continue;
                view.gameObject.SetActive(false);
                ResetTransitionAppearance(view);
                if (!cachedCards.TryGetValue(entry.Instance, out var current) || !ReferenceEquals(current, entry))
                {
                    entry.Instance = null;
                    pool.Push(entry);
                }
            }
            outgoingCards.Clear();
        }

        private Font GetFont()
        {
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 16);
            return font;
        }

        private void ConfigureCardSize(CardView view)
        {
            var layout = view.GetComponent<LayoutElement>();
            if (layout != null) { layout.preferredWidth = minimumCardWidth; layout.preferredHeight = cardHeight; }
            ((RectTransform)view.transform).sizeDelta = new Vector2(minimumCardWidth, cardHeight);
        }

        private void ClearHover()
        {
            if (fanLayout != null)
            {
                fanLayout.ResetFocus();
            }
            if (keywordTooltip != null) keywordTooltip.Hide();
        }

        private void ConfigureFan()
        {
            var background = GetComponent<Image>();
            if (background != null) background.enabled = false;
            var scroll = GetComponent<ScrollRect>();
            if (scroll != null) scroll.enabled = false;
            var mask = content.parent.GetComponent<RectMask2D>();
            if (mask != null) mask.enabled = false;
            var group = content.GetComponent<HorizontalLayoutGroup>();
            if (group != null) group.enabled = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.pivot = new Vector2(0.5f, 0f);
            fanLayout = content.GetComponent<CardFanLayout>() ?? content.gameObject.AddComponent<CardFanLayout>();
            fanLayout.FocusChanged -= HandleFanFocusChanged;
            fanLayout.FocusChanged += HandleFanFocusChanged;
        }

        private void OnDestroy()
        {
            if (fanLayout != null) fanLayout.FocusChanged -= HandleFanFocusChanged;
            CancelCategoryTransition();
            if (keywordTooltip == null) return;
            if (Application.isPlaying) Destroy(keywordTooltip.gameObject); else DestroyImmediate(keywordTooltip.gameObject);
        }
    }
}
