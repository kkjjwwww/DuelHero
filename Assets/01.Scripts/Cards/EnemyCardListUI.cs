using System.Collections.Generic;
using System.Linq;
using DuelHero.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    // Reads owned instances only; selected actions are never exposed here.
    public sealed class EnemyCardListUI : MonoBehaviour
    {
        [SerializeField] private EnemyActionPlanProvider source;
        [SerializeField] private CardReservationQueue roundSource;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Font font;
        [SerializeField] private CardView cardPrefab;
        [Header("Category Layout")]
        [SerializeField, Min(.1f)] private float cardScale = 1f;
        [SerializeField, Min(0f)] private float cardSpacing = 12f;
        [SerializeField, Min(0f)] private float rowSpacing = 16f;
        [SerializeField, Min(0f)] private float categorySpacing = 24f;
        [SerializeField, Min(1f)] private float hoverScale = 1.55f;
        [SerializeField, Min(0f)] private float hoverDuration = .15f;
        private static readonly string[] CategoryOrder = { "공격", "방어", "이동", "회복", "특수" };
        private sealed class Entry
        {
            public CardInstance Card;
            public CardView View;
            public RectTransform Container;
            public int Cooldown = int.MinValue;
        }
        private sealed class Category
        {
            public Text Title;
            public CardWrapLayout Layout;
            public readonly List<RectTransform> Items = new();
        }
        private readonly Dictionary<CardInstance, Entry> entries = new();
        private readonly Stack<Entry> pool = new();
        private readonly Dictionary<string, Category> categories = new();
        private readonly List<CardInstance> removed = new();
        private RectTransform poolRoot;
        private CardInspectionPresenter inspection;
        private ScrollRect scroll;
        private bool initialized;
        private bool layoutSettingsDirty;
        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (source != null) source.CardsChanged += OnCardsChanged;
            if (roundSource != null) roundSource.Changed += RefreshStates;
            Close();
        }
        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (source != null) source.CardsChanged -= OnCardsChanged;
            if (roundSource != null) roundSource.Changed -= RefreshStates;
            Close();
        }
        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || source == null || roundSource == null || panel == null || content == null || cardPrefab == null) return;
            Initialize();
            Synchronize();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            foreach (var category in categories.Values)
                if (category.Layout.gameObject.activeSelf) category.Layout.Reflow();
            Canvas.ForceUpdateCanvases();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }
        public void Close()
        {
            HideInspection();
            if (panel != null) panel.SetActive(false);
        }
        private void Initialize()
        {
            if (initialized) return;
            if (font == null) font = DuelHero.UI.GameFonts.UI;
            inspection = CardInspectionPresenter.GetOrCreate(GetComponentInParent<Canvas>().rootCanvas);
            if (detailLabel != null) detailLabel.gameObject.SetActive(false);
            var vertical = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.childControlWidth = vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true; vertical.childForceExpandHeight = false;
            vertical.spacing = categorySpacing;
            vertical.padding = new RectOffset(8, 8, 8, 8);
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var go = new GameObject("PooledEnemyCards", typeof(RectTransform));
            poolRoot = (RectTransform)go.transform;
            poolRoot.SetParent(transform, false); poolRoot.gameObject.SetActive(false);
            scroll = content.GetComponentInParent<ScrollRect>(true);
            if (scroll != null) scroll.onValueChanged.AddListener(OnScroll);
            initialized = true;
        }
        private void OnScroll(Vector2 _) => HideInspection();
        private void HideInspection()
        {
            if (inspection == null || inspection.Owner == null) return;
            if (inspection.Owner.IsChildOf(content)) inspection.Hide();
        }
        private void OnCardsChanged()
        {
            if (initialized) Synchronize();
        }
        private void OnValidate() => layoutSettingsDirty = true;
        private void LateUpdate()
        {
            if (!initialized || !layoutSettingsDirty) return;
            layoutSettingsDirty = false;
            content.GetComponent<VerticalLayoutGroup>().spacing = categorySpacing;
            foreach (var entry in entries.Values) UpdateCardSize(entry);
            foreach (var entry in pool) UpdateCardSize(entry);
            ArrangeCategories();
        }
        private void UpdateCardSize(Entry entry)
        {
            var rect = (RectTransform)entry.View.transform;
            rect.localScale = Vector3.one * cardScale;
            var element = entry.Container.GetComponent<LayoutElement>();
            var size = rect.rect.size * cardScale + Vector2.one * 8f;
            element.minWidth = element.preferredWidth = size.x;
            element.minHeight = element.preferredHeight = size.y;
        }
        private void Synchronize()
        {
            var cards = source.Cards;
            var owned = new HashSet<CardInstance>(cards);
            bool changed = false;
            removed.Clear();
            foreach (var pair in entries) if (!owned.Contains(pair.Key)) removed.Add(pair.Key);
            if (removed.Count > 0) HideInspection();
            foreach (var card in removed)
            {
                var entry = entries[card];
                entries.Remove(card);
                entry.Card = null;
                entry.Container.gameObject.SetActive(false);
                entry.Container.SetParent(poolRoot, false);
                pool.Push(entry); changed = true;
            }
            foreach (var card in cards)
            {
                if (entries.ContainsKey(card)) continue;
                var entry = pool.Count > 0 ? pool.Pop() : CreateEntry();
                entry.Card = card;
                entry.Cooldown = int.MinValue;
                entry.View.Bind(card.Definition, source.Database.keywords);
                entry.View.SetFrontVisible(true);
                entry.Container.gameObject.SetActive(true);
                entries.Add(card, entry); changed = true;
            }
            if (titleLabel != null) titleLabel.text = "적 보유 카드 · " + cards.Count + "장";
            if (changed) ArrangeCategories();
            RefreshStates();
        }
        private Entry CreateEntry()
        {
            var go = new GameObject("EnemyOwnedCard", typeof(RectTransform), typeof(LayoutElement));
            var entry = new Entry { Container = (RectTransform)go.transform };
            entry.Container.SetParent(poolRoot, false);
            entry.View = Instantiate(cardPrefab, entry.Container, false);
            entry.View.name = "Card";
            entry.View.SetFont(font);
            entry.View.SetClickAction(null);
            var rect = (RectTransform)entry.View.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one * cardScale;
            UpdateCardSize(entry);
            var hover = entry.View.GetComponent<CardHoverInfo>() ?? entry.View.gameObject.AddComponent<CardHoverInfo>();
            hover.ShowInfo = () =>
            {
                if (IsOpen && entry.Card != null)
                    inspection.Show(entry.Container, cardPrefab, entry.Card.Definition, source.Database, font,
                        entry.Card.RemainingCooldown(roundSource.CurrentRound), scale: hoverScale, duration: hoverDuration,
                        scroll: eventData => { if (scroll != null) scroll.OnScroll(eventData); });
            };
            // The presenter keeps the preview open while the pointer crosses into it.
            hover.HideInfo = null;
            entry.View.SetKeywordHoverActions((keyword, position) => hover.ShowInfo?.Invoke(), null);
            return entry;
        }
        private void ArrangeCategories()
        {
            HideInspection();
            foreach (var category in categories.Values) category.Items.Clear();
            var ordered = entries.Values.OrderBy(e => CategoryIndex(e.Card.Definition.actionType)).ThenBy(e => e.Card.Definition.name);
            foreach (var entry in ordered)
            {
                string name = CategoryOrder.Contains(entry.Card.Definition.actionType) ? entry.Card.Definition.actionType : "특수";
                if (!categories.TryGetValue(name, out var category))
                {
                    var go = new GameObject("Category_" + name, typeof(RectTransform), typeof(LayoutElement), typeof(CardWrapLayout));
                    go.transform.SetParent(content, false);
                    var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
                    titleGo.transform.SetParent(go.transform, false);
                    var title = titleGo.GetComponent<Text>();
                    title.font = font; title.fontSize = 18; title.color = Color.white;
                    title.alignment = TextAnchor.MiddleLeft; title.raycastTarget = false;
                    var rect = title.rectTransform;
                    rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-8f, 28f);
                    category = new Category { Title = title, Layout = go.GetComponent<CardWrapLayout>() };
                    category.Layout.LayoutChanged += HideInspection;
                    categories.Add(name, category);
                }
                category.Items.Add(entry.Container);
            }
            var cellSize = ((RectTransform)cardPrefab.transform).rect.size * cardScale + Vector2.one * 8f;
            foreach (string name in CategoryOrder)
            {
                if (!categories.TryGetValue(name, out var category)) continue;
                category.Layout.transform.SetAsLastSibling();
                category.Layout.gameObject.SetActive(category.Items.Count > 0);
                category.Title.text = name + " · " + category.Items.Count + "장";
                category.Layout.SetItems(category.Items, cellSize, cardSpacing, rowSpacing);
            }
        }
        private static int CategoryIndex(string category)
        {
            int index = System.Array.IndexOf(CategoryOrder, category);
            return index < 0 ? CategoryOrder.Length : index;
        }
        private void RefreshStates()
        {
            if (roundSource == null) return;
            foreach (var entry in entries.Values)
            {
                int cooldown = entry.Card.RemainingCooldown(roundSource.CurrentRound);
                if (entry.Cooldown == cooldown) continue;
                entry.Cooldown = cooldown;
                entry.View.SetState(string.Empty, cooldown, true);
                if (inspection != null) inspection.RefreshState(entry.Container, cooldown);
            }
        }
        private void OnDestroy()
        {
            if (scroll != null) scroll.onValueChanged.RemoveListener(OnScroll);
        }
    }
}
