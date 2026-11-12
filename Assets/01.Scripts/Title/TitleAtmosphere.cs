using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuelHero.Title
{
    internal sealed class TitleAtmosphere : MonoBehaviour
    {
        private const float Tau = 2f * Mathf.PI;
        private const float ZoomDuration = 1.2f;
        private const float MenuDuration = 0.45f;
        private static readonly Vector2 MenuOffset = Vector2.up * 24f;
        private RectTransform artwork;
        private Vector3 artworkScale;
        private Vector3 backgroundScale;
        private readonly System.Collections.Generic.List<(RectTransform Rect, Vector2 Position, CanvasGroup Group)> introItems = new();
        private RectTransform[] layers;
        private Vector2[] origins;
        private RectTransform lanternPivot;
        private Vector2 lanternOrigin;
        private Image[] fireflies;
        private FireflyCycle[] fireflyCycles;
        private float elapsed;
        private readonly System.Collections.Generic.List<Material> windMaterials = new();
        private Vector3 heroScale;

        private struct FireflyCycle
        {
            public float Start;
            public float Duration;
            public float Horizontal;
            public float Height;
            public float Drift;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
        }

        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/00.Scenes/TitleScene.unity") return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Title Canvas")
                    root.AddComponent<TitleAtmosphere>();
        }

        private void Awake()
        {
            artwork = (RectTransform)transform.Find("Title Artwork");
            string[] names = { "Moon", "Midground", "LeftWall", "RightWall",
                "Left Flagpole", "Right Flagpole", "Foreground", "Hero", "Cloud Far", "Cloud Middle" };
            layers = new RectTransform[names.Length + 4];
            origins = new Vector2[layers.Length];
            layers[0] = (RectTransform)transform.Find("Background");
            for (int i = 0; i < names.Length; i++)
                layers[i + 1] = (RectTransform)artwork.Find(names[i]);
            layers[11] = AddCloud(layers[9], "Cloud Small Left", new Vector2(-690f, 460f), 0.32f);
            layers[12] = AddCloud(layers[10], "Cloud Small Right", new Vector2(745f, 455f), 0.38f);
            layers[13] = AddCloud(layers[9], "Cloud Medium Low", new Vector2(90f, 305f), 0.58f);
            for (int i = 0; i < layers.Length; i++) origins[i] = layers[i].anchoredPosition;
            heroScale = layers[8].localScale;
            var wind = Resources.Load<Shader>("Title/TitleWind");
            if (wind != null)
            {
                ApplyWind(layers[5].Find("Left Flag"), wind, 0f, 0f);
                ApplyWind(layers[6].Find("Right Flag"), wind, 0f, 1.3f);
                ApplyWind(layers[8], wind, 1f, 0.8f);
                ApplyWind(layers[3], wind, 2f, 0.4f);
                ApplyWind(layers[4], wind, 2f, 1.1f);
                ApplyWind(layers[7], wind, 2f, 2f);
            }

            foreach (int i in new[] { 0, 2, 3, 4, 7 })
                layers[i].sizeDelta += new Vector2(48f, 0f);

            var lantern = (RectTransform)artwork.Find("Lantern");
            lanternPivot = CreateRect("Lantern Swing");
            lanternOrigin = lantern.anchoredPosition + new Vector2(0f, 72f);
            lanternPivot.anchoredPosition = lanternOrigin;
            lanternPivot.SetSiblingIndex(lantern.GetSiblingIndex());
            lantern.SetParent(lanternPivot, true);

            var fireflySprite = Resources.Load<Sprite>("Title/Firefly");
            fireflies = new Image[8];
            fireflyCycles = new FireflyCycle[fireflies.Length];
            for (int i = 0; i < fireflies.Length; i++)
            {
                var dot = CreateRect("Firefly");
                var image = dot.gameObject.AddComponent<Image>();
                image.sprite = fireflySprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                fireflies[i] = image;
                ScheduleFirefly(i, Random.Range(0f, 4f));
            }

            artworkScale = artwork.localScale;
            backgroundScale = layers[0].localScale;
            AddIntroItem((RectTransform)transform.Find("Logo Placeholder"));
            foreach (Transform child in transform)
                if (child.TryGetComponent<Button>(out _))
                    AddIntroItem((RectTransform)child);
            UpdateIntro();
        }

        private void AddIntroItem(RectTransform rect)
        {
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            introItems.Add((rect, rect.anchoredPosition, group));
        }

        private void UpdateIntro()
        {
            float zoomProgress = Mathf.Clamp01(elapsed / ZoomDuration);
            float remaining = 1f - zoomProgress;
            float zoom = 1f + 0.12f * remaining * remaining * remaining;
            artwork.localScale = artworkScale * zoom;
            layers[0].localScale = backgroundScale * zoom;

            float progress = Mathf.Clamp01((elapsed - ZoomDuration) / MenuDuration);
            remaining = 1f - progress;
            float slide = remaining * remaining * remaining;
            foreach (var item in introItems)
            {
                item.Rect.anchoredPosition = item.Position + MenuOffset * slide;
                item.Group.alpha = progress;
                if (progress >= 1f)
                {
                    item.Group.interactable = true;
                    item.Group.blocksRaycasts = true;
                }
            }

            if (progress >= 1f) introItems.Clear();
        }

        private RectTransform AddCloud(RectTransform source, string cloudName, Vector2 position, float size)
        {
            var cloud = Instantiate(source, artwork, false);
            cloud.name = cloudName;
            cloud.anchoredPosition = position;
            cloud.sizeDelta = source.sizeDelta * size;
            cloud.SetSiblingIndex(source.GetSiblingIndex() + 1);
            return cloud;
        }

        private RectTransform CreateRect(string objectName)
        {
            var rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
            rect.SetParent(artwork, false);
            return rect;
        }

        private void ApplyWind(Transform target, Shader shader, float mode, float phase)
        {
            var image = target.GetComponent<Image>();
            var material = new Material(shader);
            material.SetFloat("_Mode", mode);
            material.SetFloat("_Phase", phase);
            image.material = material;
            windMaterials.Add(material);
        }

        private void OnDestroy()
        {
            foreach (var material in windMaterials) Destroy(material);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (introItems.Count > 0) UpdateIntro();
            float sway = 3f * Mathf.Sin(elapsed * Tau / 16f);
            Move(0, sway);
            Move(1, sway);
            Move(2, sway * 3f);
            for (int i = 3; i <= 6; i++) Move(i, sway * 5f);
            Move(7, sway * 7f);
            Move(8, sway * 7f);
            var hero = layers[8];
            float breath = (1f - Mathf.Cos(elapsed * Tau / 4f)) * 0.004f;
            hero.localScale = Vector3.Scale(heroScale, new Vector3(1f, 1f + breath, 1f));
            float foot = hero.rect.yMin + hero.rect.height * 0.035f;
            hero.anchoredPosition -= Vector2.up * (foot * heroScale.y * breath);
            foreach (var material in windMaterials) material.SetFloat("_Clock", elapsed);
            Scroll(9, 8f);
            Scroll(10, 14f);
            Scroll(11, 5f);
            Scroll(12, 7f);
            Scroll(13, 11f);

            lanternPivot.anchoredPosition = lanternOrigin + Vector2.right * (sway * 5f);
            lanternPivot.localRotation = Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(elapsed * Tau / 5f));

            for (int i = 0; i < fireflies.Length; i++)
            {
                var cycle = fireflyCycles[i];
                if (elapsed >= cycle.Start + cycle.Duration)
                {
                    ScheduleFirefly(i, Random.Range(0.4f, 4.5f));
                    cycle = fireflyCycles[i];
                }
                float phase = Mathf.Clamp01((elapsed - cycle.Start) / cycle.Duration);
                var dot = fireflies[i];
                dot.rectTransform.anchoredPosition = new Vector2(
                    cycle.Horizontal * artwork.rect.width + sway * 7f + Mathf.Sin(phase * Tau) * cycle.Drift,
                    artwork.rect.yMin + cycle.Height + phase * 46f);
                dot.color = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Sin(phase * Mathf.PI), 3f) * 0.8f);
            }
        }

        private void ScheduleFirefly(int index, float delay)
        {
            fireflyCycles[index] = new FireflyCycle
            {
                Start = elapsed + delay,
                Duration = Random.Range(2.5f, 6f),
                Horizontal = Random.Range(-0.46f, 0.46f),
                Height = Random.Range(25f, 105f),
                Drift = Random.Range(-23f, 23f)
            };
            // Only the center of the transparent sprite contains the light.
            fireflies[index].rectTransform.sizeDelta = Vector2.one * Random.Range(150f, 220f);
        }

        private void Move(int index, float offset)
        {
            layers[index].anchoredPosition = origins[index] + Vector2.right * offset;
        }

        private void Scroll(int index, float speed)
        {
            float edge = (artwork.rect.width + layers[index].rect.width) * 0.5f + 8f;
            float x = Mathf.Repeat(origins[index].x + elapsed * speed + edge, edge * 2f) - edge;
            Move(index, x - origins[index].x);
        }
    }
}
