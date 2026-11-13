using DuelHero.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuelHero.Title
{
    [RequireComponent(typeof(Button))]
    public sealed class TitleButtonHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            var button = GetComponent<Button>();
            if (button.isActiveAndEnabled && button.IsInteractable())
                SoundEffectPlayer.Play("title.button.hover");
        }
    }
}
