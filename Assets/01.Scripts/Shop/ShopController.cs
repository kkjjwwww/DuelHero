using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Run;
using DuelHero.UI;
using UnityEngine;
using DuelHero.Map;
using UnityEngine.UI;

namespace DuelHero.Shop
{
    public sealed class ShopController : MonoBehaviour
    {
        [SerializeField] private CardDatabase database;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private ButtonHoverScale cardHoverTemplate;
        [SerializeField] private RectTransform content;
        [SerializeField] private Camera shopCamera;
        [SerializeField] private Transform[] cardAnchors;
        [SerializeField] private Transform[] artifactAnchors;
        [SerializeField] private ArtifactDatabase artifactDatabase;
        [SerializeField] private Material artifactOutlineMaterial;
        [SerializeField] private ShopConfig shopConfig;
        [SerializeField] private Text goldLabel, message;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button ownedCardsButton;
        [SerializeField] private EnemyCardListUI ownedCardsView;
        [SerializeField] private SpriteNumberUI ownedCardsCount;
        [SerializeField] private string mapScenePath = "Assets/00.Scenes/MapScene.unity";
        private RunSession run;
        private string nodeId;
        private IReadOnlyList<ShopOffer> offers = Array.Empty<ShopOffer>();
        private bool transitioning, completed;
        private KeywordTooltip tooltip;
        private CardRangePreview rangePreview;
        private RectTransform overlay;
        private CurrencyLabel goldDisplay;
        private Sprite priceIcon;
        private readonly List<CardView> cardViews = new();
        private readonly List<CurrencyLabel> priceLabels = new();
        private readonly List<ButtonHoverScale> hoverEffects = new();
        private readonly List<ArtifactDisplay> artifactDisplays = new();
        public string LastError { get; private set; }

        private sealed class ArtifactDisplay
        {
            public ArtifactDefinition Definition;
            public Transform Anchor;
            public Button Button;
            public CurrencyLabel Price;
            public DelayedTooltipTrigger Tooltip;
            public UIWhiteOutline Outline;
        }

        private void Awake() { run = RunSessionStore.Current; nodeId = run?.PendingShopNodeId; offers = run?.ShopOffers ?? Array.Empty<ShopOffer>(); }
        private void OnEnable()
        {
            if (ownedCardsView != null) ownedCardsView.DeckPopupSoundsEnabled = true;
            if (exitButton != null) exitButton.onClick.AddListener(Exit);
            if (ownedCardsButton != null) ownedCardsButton.onClick.AddListener(ToggleOwnedCards);
            if (ownedCardsView != null) ownedCardsView.OpenChanged += HandleDeckVisibility;
            SetHoverEnabled(!transitioning && !completed);
        }
        private void OnDisable()
        {
            if (exitButton != null) exitButton.onClick.RemoveListener(Exit);
            if (ownedCardsButton != null) ownedCardsButton.onClick.RemoveListener(ToggleOwnedCards);
            if (ownedCardsView != null)
            {
                ownedCardsView.OpenChanged -= HandleDeckVisibility;
                ownedCardsView.CloseSilently();
            }
            SetHoverEnabled(false);
            foreach (var display in artifactDisplays)
            {
                display.Tooltip.Cancel();
                UpdateArtifactOutline(display);
            }
            if (tooltip != null) tooltip.Hide();
        }
        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            if (artifactOutlineMaterial != null)
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            overlay = (RectTransform)canvas.transform;
            goldDisplay = CurrencyLabel.CreateGoldBalance(goldLabel);
            priceIcon = Resources.Load<Sprite>("UI/Currency/gold-coin");
            tooltip = KeywordTooltip.Create(canvas);
            rangePreview = CardRangePreview.GetOrCreate(canvas);
            if (run == null || nodeId == null)
            { message.text = "진행 중인 상점 방문이 없습니다. 맵에서 상점에 진입해주세요."; Refresh(); return; }
            if (database == null || cardPrefab == null || cardHoverTemplate == null) { message.text = "상점 카드 데이터와 호버 설정을 연결해주세요."; Refresh(); return; }
            if (shopCamera != null && (cardAnchors == null || cardAnchors.Length < offers.Count || cardAnchors.Take(offers.Count).Any(a => a == null)))
            { message.text = "상점 카메라와 카드 진열 위치를 연결해주세요."; Refresh(); return; }
            CreateCardOffers();
            if (shopCamera != null && artifactDatabase != null && artifactAnchors != null)
            {
                var slots = artifactAnchors.Where(anchor => anchor != null).ToArray();
                var stock = ShopOfferSelector.SelectArtifacts(artifactDatabase.artifacts, slots.Length, run.Map.Seed, nodeId);
                for (int i = 0; i < stock.Length; i++) CreateArtifact(slots[i], stock[i]);
            }
            message.text = offers.Count == 0 ? "판매 가능한 카드가 없습니다." : "카드를 클릭하면 구매합니다.";
            Refresh();
        }
        private void CreateCardOffers()
        {
            if (shopCamera == null && content != null && content.TryGetComponent<GridLayoutGroup>(out var layout))
                layout.constraintCount = Math.Max(1, Math.Min(4, offers.Count));
            for (int i = 0; i < offers.Count; i++) CreateOffer(i);
        }

        private void CreateOffer(int index)
        {
            var offer = offers[index];
            var definition = database.cards.FirstOrDefault(c => c.id == offer.CardId);
            const float cardWidth = 132f;
            bool onStand = shopCamera != null;
            var root = new GameObject("Offer_" + index, typeof(RectTransform));
            var holder = (RectTransform)root.transform;
            var size = new Vector2(cardWidth, cardWidth * 88f / 63f);
            if (onStand)
            {
                var anchor = cardAnchors[index];
                holder.SetParent(anchor, false);
                holder.SetPositionAndRotation(anchor.position, shopCamera.transform.rotation);
                holder.localScale = Vector3.one * (.70f / cardWidth);
                holder.sizeDelta = size;
                holder.pivot = new Vector2(.5f, 0);
                var display = root.AddComponent<Canvas>();
                display.renderMode = RenderMode.WorldSpace;
                display.worldCamera = shopCamera;
                root.AddComponent<GraphicRaycaster>();
            }
            else holder.SetParent(content, false);
            var card = Instantiate(cardPrefab, holder, false);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, onStand ? 0 : .5f);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * (onStand ? 1f : 1.65f);
            rect.anchoredPosition3D = onStand ? Vector3.zero : new Vector3(0, 35, 0);
            card.SetFont(message.font);
            if (definition != null)
            {
                card.Bind(definition, database.keywords); card.SetFrontVisible(true); card.SetState(string.Empty, 0, true);
                var scale = Instantiate(cardHoverTemplate, card.transform, false);
                scale.name = "CardHoverScale";
                scale.ConfigureTargets(onStand ? rect : card.VisualRoot);
                scale.gameObject.SetActive(true);
                hoverEffects.Add(scale);
                card.SetClickAction(() => Buy(index));
                var hover = card.gameObject.GetComponent<CardHoverInfo>() ?? card.gameObject.AddComponent<CardHoverInfo>();
                hover.ShowInfo = () =>
                {
                    if (transitioning || completed || IsDeckOpen) return;
                    scale.OnPointerEnter(null);
                    rangePreview.Show(card, shopCamera);
                };
                hover.HideInfo = () =>
                {
                    if (scale != null) scale.OnPointerExit(null);
                    if (tooltip != null) tooltip.Hide();
                    if (rangePreview != null) rangePreview.Hide(card);
                };
                card.SetKeywordHoverActions((k, p) => tooltip.Show(k, p), () => { if (tooltip != null) tooltip.Hide(); }, p => tooltip.Move(p));
            }
            cardViews.Add(card);
            var price = CreatePriceLabel("Price_" + index, onStand ? overlay : holder);
            if (onStand) price.Position.pivot = new Vector2(.5f, 1);
            else price.Position.anchoredPosition = new Vector2(0, -155);
            priceLabels.Add(price);
        }

        private CurrencyLabel CreatePriceLabel(string name, RectTransform parent)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            label.transform.SetParent(parent, false);
            var text = label.GetComponent<Text>(); text.font = message.font; text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter; text.color = new Color(1, .85f, .46f); text.raycastTarget = false;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.sizeDelta = new Vector2(180, 34);
            return new CurrencyLabel(text, priceIcon, 22f);
        }

        private void CreateArtifact(Transform anchor, ArtifactDefinition artifact)
        {
            var root = new GameObject("Artifact_" + artifact.id, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)root.transform;
            rect.SetParent(overlay, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(104, 104);
            var icon = root.GetComponent<Image>();
            icon.sprite = Resources.Load<Sprite>("UI/Artifacts/" + artifact.iconKey);
            if (icon.sprite == null)
            {
                icon.color = new Color(.24f, .2f, .3f);
                var fallback = new GameObject("ArtifactName", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                fallback.transform.SetParent(rect, false);
                fallback.rectTransform.anchorMax = Vector2.one;
                fallback.rectTransform.offsetMin = fallback.rectTransform.offsetMax = Vector2.zero;
                fallback.font = message.font;
                fallback.alignment = TextAnchor.MiddleCenter;
                fallback.text = artifact.name;
                fallback.raycastTarget = false;
            }
            icon.preserveAspect = true;
            UIWhiteOutline outline = null;
            if (icon.sprite != null && artifactOutlineMaterial != null)
            {
                icon.material = artifactOutlineMaterial;
                outline = root.AddComponent<UIWhiteOutline>();
                outline.enabled = false;
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(2f, -2f);
            }
            var button = root.GetComponent<Button>();
            button.targetGraphic = icon;
            button.onClick.AddListener(() => BuyArtifact(artifact));
            var hover = root.AddComponent<DelayedTooltipTrigger>();
            string description = artifact.description;
            if (artifact.effectKey == ArtifactDefinition.Heal)
                description = description.Replace("{value}", artifact.effectValue.ToString());
            hover.Configure($"<b>{artifact.name}</b>\n{description}", .15f, showWhenDisabled: true);
            var price = CreatePriceLabel("ArtifactPrice", rect);
            price.Position.anchorMin = price.Position.anchorMax = new Vector2(.5f, 0);
            price.Position.pivot = new Vector2(.5f, 1);
            price.Position.anchoredPosition = Vector2.down * 6f;
            artifactDisplays.Add(new ArtifactDisplay
            {
                Definition = artifact, Anchor = anchor, Button = button, Price = price, Tooltip = hover, Outline = outline
            });
        }

        private void LateUpdate()
        {
            if (shopCamera == null) return;
            for (int i = 0; i < priceLabels.Count; i++)
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,
                    shopCamera.WorldToScreenPoint(cardAnchors[i].position), null, out var position))
                    priceLabels[i].Position.anchoredPosition = position + Vector2.down * 6f;
            foreach (var display in artifactDisplays)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,
                    shopCamera.WorldToScreenPoint(display.Anchor.position), null, out var artifactPosition))
                    ((RectTransform)display.Button.transform).anchoredPosition = artifactPosition;
                UpdateArtifactOutline(display);
            }
        }

        private void UpdateArtifactOutline(ArtifactDisplay display)
        {
            if (display.Outline != null)
                display.Outline.enabled = display.Tooltip.IsHovered && display.Button.IsInteractable() && !IsDeckOpen;
        }
        private bool IsCurrent => run != null && ReferenceEquals(run, RunSessionStore.Current);
        private bool IsDeckOpen => ownedCardsView != null && ownedCardsView.IsOpen;
        private bool CanRefreshStock => shopConfig != null && cardAnchors != null &&
            shopConfig.cardOfferCount <= cardAnchors.Length &&
            cardAnchors.Take(shopConfig.cardOfferCount).All(anchor => anchor != null);

        private void ToggleOwnedCards()
        {
            if (transitioning || !IsCurrent || database == null || ownedCardsView == null) return;
            ownedCardsView.ToggleRunDeck(database, run.CardIds);
        }

        private void HandleDeckVisibility()
        {
            SetHoverEnabled(!IsDeckOpen && !transitioning && !completed);
            if (IsDeckOpen)
            {
                tooltip?.Hide();
                foreach (var display in artifactDisplays) display.Tooltip.Cancel();
            }
        }
        private void BuyArtifact(ArtifactDefinition artifact)
        {
            if (transitioning || completed || IsDeckOpen || !IsCurrent) return;
            if (artifact.effectKey == ArtifactDefinition.RefreshShop && !CanRefreshStock) return;
            if (!run.TryBuyShopArtifact(nodeId, artifact, database.cards, shopConfig)) return;
            if (artifact.effectKey == ArtifactDefinition.RefreshShop)
            {
                foreach (var card in cardViews)
                {
                    rangePreview.Hide(card);
                    var holder = card.transform.parent.gameObject;
                    holder.SetActive(false);
                    Destroy(holder);
                }
                foreach (var price in priceLabels)
                {
                    price.Position.gameObject.SetActive(false);
                    Destroy(price.Position.gameObject);
                }
                cardViews.Clear();
                priceLabels.Clear();
                hoverEffects.Clear();
                tooltip.Hide();
                offers = run.ShopOffers;
                CreateCardOffers();
                message.text = "진열 카드를 새로 뽑았습니다.";
            }
            else message.text = $"체력 {run.PlayerHealth}/{run.PlayerMaxHealth}";
            LastError = null;
            Refresh();
        }


        public bool Buy(int index)
        {
            if (transitioning || completed || IsDeckOpen || !IsCurrent || run.PendingShopNodeId != nodeId) return false;
            if (index < 0 || index >= offers.Count || database == null || !database.cards.Any(c => c.id == offers[index].CardId)) return false;
            if (!run.TryBuyShopOffer(nodeId, index))
            { message.text = "골드가 부족하거나 구매할 수 없는 상품입니다."; Refresh(); return false; }
            tooltip.Hide(); message.text = "카드를 덱에 추가했습니다."; LastError = null; Refresh(); return true;
        }
        private void Refresh()
        {
            goldDisplay.SetAmount(IsCurrent ? run.Gold : 0);
            if (ownedCardsCount != null) ownedCardsCount.SetValue(IsCurrent ? run.CardIds.Count : 0);
            if (ownedCardsButton != null) ownedCardsButton.interactable = !transitioning && IsCurrent && database != null;
            for (int i = 0; i < cardViews.Count; i++)
            {
                var offer = offers[i];
                bool valid = database.cards.Any(c => c.id == offer.CardId);
                cardViews[i].SetState(string.Empty, 0, valid && !offer.Sold && IsCurrent && !transitioning && !completed, grayscale: offer.Sold);
                if (!valid || offer.Sold) priceLabels[i].SetStatus(valid ? "품절" : "카드 데이터 없음");
                else priceLabels[i].SetAmount(offer.Price);
            }
            foreach (var display in artifactDisplays)
            {
                bool sold = IsCurrent && run.IsShopArtifactSold(display.Definition.id);
                display.Button.interactable = IsCurrent && !transitioning && !completed &&
                    run.CanBuyShopArtifact(nodeId, display.Definition) &&
                    (display.Definition.effectKey != ArtifactDefinition.RefreshShop || CanRefreshStock);
                UpdateArtifactOutline(display);
                if (sold) display.Price.SetStatus("품절");
                else display.Price.SetAmount(display.Definition.shopPrice);
            }
            exitButton.interactable = !transitioning;
        }
        private void SetHoverEnabled(bool value)
        {
            foreach (var hover in hoverEffects) if (hover != null) hover.enabled = value;
            if (!value && rangePreview != null) foreach (var card in cardViews) rangePreview.Hide(card);
        }
        public void Exit()
        {
            if (transitioning || IsDeckOpen) return;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(mapScenePath)) throw new InvalidOperationException("맵 씬을 불러올 수 없습니다.");
                if (nodeId != null)
                {
                    if (!IsCurrent) throw new InvalidOperationException("상점을 방문한 런이 변경되었습니다.");
                    if (!completed) { if (!run.TryCompleteShop(nodeId)) throw new InvalidOperationException("상점 방문 완료 실패"); completed = true; }
                }
                transitioning = true; SetHoverEnabled(false); tooltip?.Hide(); Refresh();
                MapSceneFade.Load(mapScenePath, RestoreExit);
            }
            catch (Exception error) { RestoreExit(error); }

            void RestoreExit(Exception error)
            {
                transitioning = false; SetHoverEnabled(!completed); LastError = error.Message;
                message.text = LastError + " 나가기를 눌러 재시도하세요."; Refresh();
            }
        }
    }
}
