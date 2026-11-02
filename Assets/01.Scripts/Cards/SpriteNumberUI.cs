using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class SpriteNumberUI : MonoBehaviour
    {
        [SerializeField] private Sprite[] digits = new Sprite[10];
        [SerializeField] private float digitSize = 24f;
        [SerializeField] private float digitAdvance = 16f;
        private readonly List<Image> images = new();
        private int displayedValue = -1;

        public void SetValue(int value)
        {
            value = Mathf.Max(0, value);
            if (displayedValue == value) return;
            if (images.Count == 0) images.AddRange(GetComponentsInChildren<Image>(true));
            string text = value.ToString();
            float height = digitSize * digits[0].rect.height / digits[0].texture.height;
            while (images.Count < text.Length)
            {
                var go = new GameObject("Digit", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                images.Add(image);
            }
            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                image.gameObject.SetActive(i < text.Length);
                if (i >= text.Length) continue;
                image.sprite = digits[text[i] - '0'];
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(height * image.sprite.rect.width / image.sprite.rect.height, height);
                rect.anchoredPosition = new Vector2((i - (text.Length - 1) * .5f) * digitAdvance, 0f);
            }
            displayedValue = value;
        }
    }
}
