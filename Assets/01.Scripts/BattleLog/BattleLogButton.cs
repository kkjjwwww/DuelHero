using DG.Tweening;
using DuelHero.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Logging
{
    public sealed class BattleLogButton : MonoBehaviour
    {
        [SerializeField] private BattleLogUI panel;
        [SerializeField] private Sprite handsSprite;
        [SerializeField] private Sprite arrowSprite;
        private Tween rotation;
        private RectTransform arrow;
        private GameObject buttonObject;

        private void Awake()
        {
            if (panel == null || handsSprite == null || arrowSprite == null) return;
            panel.gameObject.SetActive(false);

            buttonObject = new GameObject("Battle Log Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.SetParent(transform, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.02f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.sizeDelta = new Vector2(32f, 32f);
            buttonRect.anchoredPosition = new Vector2(12f, -136f);
            buttonObject.GetComponent<Image>().color = Color.clear;

            var visual = new GameObject("Clock", typeof(RectTransform)).GetComponent<RectTransform>();
            Stretch(visual, buttonRect);
            arrow = CreateLayer(visual, "Arrow", arrowSprite);
            CreateLayer(visual, "Hands", handsSprite);

            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(panel.TogglePanel);
            buttonObject.AddComponent<ButtonHoverScale>().ConfigureTargets(visual);
            buttonObject.AddComponent<DelayedTooltipTrigger>().Configure("전투 기록", 1f);

            var trigger = buttonObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => SpinArrow());
            trigger.triggers.Add(enter);
        }

        private void OnEnable()
        {
            if (buttonObject != null) buttonObject.SetActive(true);
        }

        private void OnDisable()
        {
            rotation?.Kill();
            rotation = null;
            if (arrow != null) arrow.localRotation = Quaternion.identity;
            if (buttonObject != null) buttonObject.SetActive(false);
        }

        private void SpinArrow()
        {
            rotation?.Kill();
            arrow.localRotation = Quaternion.identity;
            rotation = arrow.DOLocalRotate(new Vector3(0f, 0f, 360f), .45f, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private static RectTransform CreateLayer(RectTransform parent, string layerName, Sprite sprite)
        {
            var image = new GameObject(layerName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Stretch(image.rectTransform, parent);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private static void Stretch(RectTransform rect, RectTransform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
