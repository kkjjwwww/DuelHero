using System;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Map
{
    public enum RouteNodeState { Locked, Available, Completed, Current }

    [RequireComponent(typeof(RectTransform), typeof(Button))]
    public sealed class RouteNodeView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image border;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Sprite battleIcon;
        [SerializeField] private Sprite restIcon;
        [SerializeField] private Sprite shopIcon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Color battleColor = new Color(.76f, .57f, .35f);
        [SerializeField] private Color restColor = new Color(.42f, .75f, .67f);
        [SerializeField] private Color shopColor = new Color(.9f, .77f, .47f);
        [SerializeField] private Color bossColor = new Color(.71f, .57f, .88f);
        [SerializeField] private Color backgroundColor = new Color(.075f, .105f, .135f);
        [SerializeField] private Color currentColor = new Color(1f, .88f, .55f);

        private string nodeId;
        private Color accent;
        private RouteNodeState state;
        public event Action<string> Clicked;

        private void OnEnable() => button.onClick.AddListener(HandleClick);
        private void OnDisable() => button.onClick.RemoveListener(HandleClick);

        public void Bind(string id, RouteNodeType type)
        {
            nodeId = id;
            Render(type);
        }

        private void HandleClick()
        {
            if (nodeId != null && button.IsInteractable()) Clicked?.Invoke(nodeId);
        }

        public void Render(RouteNodeType type)
        {
            Sprite sprite;
            switch (type)
            {
                case RouteNodeType.Rest:
                    accent = restColor;
                    sprite = restIcon;
                    label.text = "휴식";
                    break;
                case RouteNodeType.Shop:
                    accent = shopColor;
                    sprite = shopIcon;
                    label.text = "상점";
                    break;
                case RouteNodeType.Boss:
                    accent = bossColor;
                    sprite = bossIcon;
                    label.text = "최종 보스";
                    break;
                default:
                    accent = battleColor;
                    sprite = battleIcon;
                    label.text = "일반 전투";
                    break;
            }
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
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
            state = value;
            button.interactable = value == RouteNodeState.Available;
            switch (value)
            {
                case RouteNodeState.Available:
                    border.color = Color.Lerp(accent, Color.white, .35f);
                    background.color = backgroundColor;
                    label.color = accent;
                    break;
                case RouteNodeState.Completed:
                    border.color = Color.Lerp(backgroundColor, accent, .55f);
                    background.color = Color.Lerp(backgroundColor, accent, .08f);
                    label.color = Color.Lerp(backgroundColor, accent, .6f);
                    break;
                case RouteNodeState.Current:
                    border.color = currentColor;
                    background.color = Color.Lerp(backgroundColor, currentColor, .25f);
                    label.color = currentColor;
                    break;
                default:
                    border.color = Color.Lerp(backgroundColor, accent, .2f);
                    background.color = Color.Lerp(Color.black, backgroundColor, .65f);
                    label.color = Color.Lerp(backgroundColor, accent, .4f);
                    break;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(button);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(border);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(background);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
#endif
        }
    }
}
