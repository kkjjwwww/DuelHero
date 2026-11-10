using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    [RequireComponent(typeof(Button))]
    public sealed class ButtonHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject outline;

        private Button button;
        private bool hovered;

        private void Awake()
        {
            button = GetComponent<Button>();
            outline.SetActive(false);
            var canvas = button.targetGraphic.canvas;
            if (canvas != null)
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            outline.SetActive(button.isActiveAndEnabled && button.IsInteractable());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            outline.SetActive(false);
        }

        private void LateUpdate()
        {
            if (hovered)
                outline.SetActive(button.isActiveAndEnabled && button.IsInteractable());
        }

        private void OnDisable()
        {
            hovered = false;
            outline.SetActive(false);
        }
    }
}
