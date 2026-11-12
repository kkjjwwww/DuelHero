using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class OptionsPopupBackdrop : MonoBehaviour
    {
        private GameObject backdrop;

        private void OnEnable()
        {
            if (backdrop == null)
            {
                backdrop = new GameObject("OptionsPopupBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                var rect = backdrop.GetComponent<RectTransform>();
                rect.SetParent(transform.parent, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var image = backdrop.GetComponent<Image>();
                image.color = Color.clear;
                image.canvasRenderer.cullTransparentMesh = false;
                backdrop.GetComponent<Button>().onClick.AddListener(Close);
            }

            transform.SetAsLastSibling();
            backdrop.transform.SetSiblingIndex(transform.GetSiblingIndex() - 1);
            backdrop.SetActive(true);
        }

        private void Close() => gameObject.SetActive(false);
        private void OnDisable() { if (backdrop != null) backdrop.SetActive(false); }
        private void OnDestroy() { if (backdrop != null) Destroy(backdrop); }
    }
}
