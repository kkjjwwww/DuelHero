using UnityEngine;

namespace DuelHero.Run
{
    [RequireComponent(typeof(Light))]
    public sealed class CampfireLightFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, .5f)] private float flickerStrength = .15f;
        [SerializeField, Min(.01f)] private float flickerSpeed = .9f;

        private Light campfireLight;
        private float baseIntensity;
        private double startTime;

        private void Awake() => campfireLight = GetComponent<Light>();

        private void OnEnable()
        {
            baseIntensity = campfireLight.intensity;
            startTime = Time.timeAsDouble;
        }

        private void Update()
        {
            float phase = (float)(Time.timeAsDouble - startTime) * flickerSpeed;
            float slow = Mathf.PerlinNoise(phase + 12.3f, 3.7f);
            float quick = Mathf.PerlinNoise(phase * 3f + 25.7f, 9.4f);
            float flicker = Mathf.Clamp((slow * .75f + quick * .25f - .5f) * 3f, -1f, 1f);
            float flare = Mathf.SmoothStep(0f, .5f, Mathf.InverseLerp(.72f, .88f, quick));
            campfireLight.intensity = baseIntensity * (1f + flickerStrength * (flicker + flare));
        }

        private void OnDisable() => campfireLight.intensity = baseIntensity;
    }
}
