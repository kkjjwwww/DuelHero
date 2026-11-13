using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class BgmLoopFade : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fadeInSeconds = 1f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 1f;

    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
        source.volume = 0f;
    }

    private void Update()
    {
        if (!source.isPlaying || source.clip == null) return;

        float position = source.timeSamples / (float)source.clip.frequency;
        float remaining = (source.clip.samples - source.timeSamples) / (float)source.clip.frequency;
        float fadeIn = fadeInSeconds > 0f ? Mathf.Clamp01(position / fadeInSeconds) : 1f;
        float fadeOut = fadeOutSeconds > 0f ? Mathf.Clamp01(remaining / fadeOutSeconds) : 1f;
        source.volume = baseVolume * Mathf.Min(fadeIn, fadeOut);
    }
}
