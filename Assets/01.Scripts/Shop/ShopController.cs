using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Run;
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
        [SerializeField] private Text goldLabel, message;
        [SerializeField] private Button exitButton;
        [SerializeField] private string mapScenePath = "Assets/00.Scenes/MapScene.unity";
        private RunSession run;
        private string nodeId;
        private IReadOnlyList<ShopOffer> offers = Array.Empty<ShopOffer>();
        private bool transitioning, completed;
        private KeywordTooltip tooltip;
        private CardRangePreview rangePreview;
        private readonly List<CardView> cardViews = new();
        private readonly List<Text> priceLabels = new();
        private readonly List<ButtonHoverScale> hoverEffects = new();
        public string LastError { get; private set; }

        private void Awake() { run = RunSessionStore.Current; nodeId = run?.PendingShopNodeId; offers = run?.ShopOffers ?? Array.Empty<ShopOffer>(); }
        private void OnEnable()
        {
            if (exitButton != null) exitButton.onClick.AddListener(Exit);
            SetHoverEnabled(!transitioning && !completed);
        }
        private void OnDisable()
        {
            if (exitButton != null) exitButton.onClick.RemoveListener(Exit);
            SetHoverEnabled(false);
            if (tooltip != null) tooltip.Hide();
        }
        private void Start()
        {
            tooltip = KeywordTooltip.Create(GetComponentInParent<Canvas>().rootCanvas);
            rangePreview = CardRangePreview.GetOrCreate(GetComponentInParent<Canvas>().rootCanvas);
            if (run == null || nodeId == null)
            { message.text = "진행 중인 상점 방문이 없습니다. 맵에서 상점에 진입해주세요."; Refresh(); return; }
            if (database == null || cardPrefab == null || cardHoverTemplate == null) { message.text = "상점 카드 데이터와 호버 설정을 연결해주세요."; Refresh(); return; }
            var layout = content.GetComponent<GridLayoutGroup>();
            if (layout != null) layout.constraintCount = Math.Max(1, Math.Min(4, offers.Count));
            for (int i = 0; i < offers.Count; i++) CreateOffer(i);
            message.text = offers.Count == 0 ? "판매 가능한 카드가 없습니다." : "카드를 클릭하면 구매합니다.";
            Refresh();
        }
        private void CreateOffer(int index)
        {
            var offer = offers[index];
            var definition = database.cards.FirstOrDefault(c => c.id == offer.CardId);
            var root = new GameObject("Offer_" + index, typeof(RectTransform)); root.transform.SetParent(content, false);
            var card = Instantiate(cardPrefab, root.transform, false);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(132, 132 * 88f / 63f); rect.localScale = Vector3.one * 1.65f;
            rect.anchoredPosition = new Vector2(0, 35);
            card.SetFont(message.font);
            if (definition != null)
            {
                card.Bind(definition, database.keywords); card.SetFrontVisible(true); card.SetState(string.Empty, 0, true);
                var scale = Instantiate(cardHoverTemplate, card.transform, false);
                scale.name = "CardHoverScale";
                scale.ConfigureTargets(card.VisualRoot);
                scale.gameObject.SetActive(true);
                hoverEffects.Add(scale);
                card.SetClickAction(() => Buy(index));
                var hover = card.gameObject.GetComponent<CardHoverInfo>() ?? card.gameObject.AddComponent<CardHoverInfo>();
                hover.ShowInfo = () =>
                {
                    if (transitioning || completed) return;
                    scale.OnPointerEnter(null);
                    rangePreview.Show(card);
                };
                hover.HideInfo = () => { scale.OnPointerExit(null); tooltip.Hide(); rangePreview.Hide(card); };
                card.SetKeywordHoverActions((k, p) => tooltip.Show(k, p), () => tooltip.Hide(), p => tooltip.Move(p));
            }
            cardViews.Add(card);
            var label = new GameObject("Price", typeof(RectTransform), typeof(Text)); label.transform.SetParent(root.transform, false);
            var text = label.GetComponent<Text>(); text.font = message.font; text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter; text.color = new Color(1, .85f, .46f); text.raycastTarget = false;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.sizeDelta = new Vector2(220, 48); labelRect.anchoredPosition = new Vector2(0, -155);
            priceLabels.Add(text);
        }
        private bool IsCurrent => run != null && ReferenceEquals(run, RunSessionStore.Current);
        public bool Buy(int index)
        {
            if (transitioning || completed || !IsCurrent || run.PendingShopNodeId != nodeId) return false;
            if (index < 0 || index >= offers.Count || database == null || !database.cards.Any(c => c.id == offers[index].CardId)) return false;
            if (!run.TryBuyShopOffer(nodeId, index))
            { message.text = "골드가 부족하거나 구매할 수 없는 상품입니다."; Refresh(); return false; }
            tooltip.Hide(); message.text = "카드를 덱에 추가했습니다."; LastError = null; Refresh(); return true;
        }
        private void Refresh()
        {
            goldLabel.text = "골드  " + (IsCurrent ? run.Gold : 0);
            for (int i = 0; i < cardViews.Count; i++)
            {
                var offer = offers[i];
                bool valid = database.cards.Any(c => c.id == offer.CardId);
                // Keep information hover and insufficient-funds feedback available after purchase.
                cardViews[i].SetState(string.Empty, 0, valid && IsCurrent && !transitioning && !completed);
                priceLabels[i].text = !valid ? "카드 데이터 없음" : offer.Sold ? "품절" : offer.Price + " 골드";
            }
            exitButton.interactable = !transitioning;
        }
        private void SetHoverEnabled(bool value)
        {
            foreach (var hover in hoverEffects) if (hover != null) hover.enabled = value;
            if (!value) foreach (var card in cardViews) rangePreview.Hide(card);
        }
        public void Exit()
        {
            if (transitioning) return;
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
