using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Map
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class RouteNodeView : MonoBehaviour
    {
        [SerializeField] private Image border;
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

        public void Render(RouteNodeType type)
        {
            Color accent;
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
            border.color = accent;
            label.color = accent;
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(border);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(icon.gameObject);
            }
#endif
        }
    }
}
