using System;
using DG.Tweening;
using DuelHero.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Map
{
    public enum RouteNodeState { Locked, Available, Completed, Current }

    [RequireComponent(typeof(RectTransform), typeof(Button))]
    public sealed class RouteNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite battleIcon;
        [SerializeField] private Sprite restIcon;
        [SerializeField] private Sprite shopIcon;
        [SerializeField] private Sprite bossIcon;
        private string nodeId;
        private RouteNodeState state;
        private DelayedTooltipTrigger tooltip;
        private Tween iconMotion;
        private bool hovered;
        private const float EnlargedScale = 1.1f;
        public event Action<string> Clicked;

        private void Awake() => tooltip = gameObject.AddComponent<DelayedTooltipTrigger>();

        private void OnEnable()
        {
            button.onClick.AddListener(HandleClick);
            if (state == RouteNodeState.Available) StartPulse();
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(HandleClick);
            iconMotion?.Kill();
            iconMotion = null;
            hovered = false;
            if (state == RouteNodeState.Available) icon.rectTransform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (state != RouteNodeState.Available) return;
            iconMotion?.Kill();
            iconMotion = icon.rectTransform.DOScale(EnlargedScale, .13f).SetEase(Ease.OutSine).SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (state != RouteNodeState.Available) return;
            iconMotion?.Kill();
            iconMotion = icon.rectTransform.DOScale(1f, .18f).SetEase(Ease.OutSine)
                .SetUpdate(true).OnComplete(StartPulse);
        }

        private void StartPulse()
        {
            iconMotion = icon.rectTransform.DOScale(EnlargedScale, .65f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        public void Bind(string id, RouteNodeType type)
        {
            nodeId = id;
            Render(type);
        }

        internal void SetNodeId(string id) => nodeId = id;

        internal void SetIconAngle(float degrees)
        {
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(icon.rectTransform);
#endif
        }

        private void HandleClick()
        {
            if (nodeId != null && button.IsInteractable()) Clicked?.Invoke(nodeId);
        }

        public void Render(RouteNodeType type)
        {
            Sprite sprite;
            string nodeName;
            switch (type)
            {
                case RouteNodeType.Rest:
                    sprite = restIcon;
                    nodeName = "휴식";
                    break;
                case RouteNodeType.Shop:
                    sprite = shopIcon;
                    nodeName = "상점";
                    break;
                case RouteNodeType.Boss:
                    sprite = bossIcon;
                    nodeName = "보스";
                    break;
                default:
                    sprite = battleIcon;
                    nodeName = "전투";
                    break;
            }
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
            if (tooltip != null) tooltip.Configure(nodeName, 0f, showWhenDisabled: true);
            SetState(state);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(icon.gameObject);
            }
#endif
        }

        public void SetState(RouteNodeState value)
        {
            iconMotion?.Kill();
            iconMotion = null;
            state = value;
            button.interactable = value == RouteNodeState.Available;
            float opacity = value == RouteNodeState.Locked ? .55f
                : value == RouteNodeState.Completed ? .78f : 1f;
            icon.color = new Color(1f, 1f, 1f, opacity);
            icon.rectTransform.localScale = Vector3.one * (value == RouteNodeState.Current ? 1.12f
                : value == RouteNodeState.Available && hovered ? EnlargedScale : 1f);
            if (Application.isPlaying && isActiveAndEnabled && value == RouteNodeState.Available && !hovered)
                StartPulse();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(button);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(icon.rectTransform);
            }
#endif
        }
    }
}
