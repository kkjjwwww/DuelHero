using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DuelHero.Audio
{
    public sealed class SoundEffectPlayer : MonoBehaviour
    {
        private static SoundEffectPlayer instance;
        private SoundEffectCatalog catalog;
        private AudioMixerGroup effectsGroup;
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private const float CardHoverInterval = 0.08f;
        private float lastCardHoverTime = float.NegativeInfinity;
        private const float TitleButtonHoverInterval = 0.08f;
        private float lastTitleButtonHoverTime = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => instance = null;

        public static void Play(string id)
        {
            if (instance == null) CreatePlayer();
            instance.PlayEffect(id);
        }

        private static void CreatePlayer()
        {
            var root = new GameObject("Sound Effect Player");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<SoundEffectPlayer>();
            instance.catalog = Resources.Load<SoundEffectCatalog>("SoundEffectCatalog");
            var mixer = Resources.Load<AudioMixer>("DuelHeroAudio");
            instance.effectsGroup = mixer.FindMatchingGroups("Effects")[0];
        }

        private void PlayEffect(string id)
        {
            var entry = catalog.Find(id);
            if (entry == null || entry.clip == null) return;
            if (id == "battle.card.hover" && Time.unscaledTime - lastCardHoverTime < CardHoverInterval) return;
            if (id == "title.button.hover" && Time.unscaledTime - lastTitleButtonHoverTime < TitleButtonHoverInterval) return;

            AudioSource source = null;
            foreach (var candidate in sources)
                if (!candidate.isPlaying) { source = candidate; break; }
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = effectsGroup;
                sources.Add(source);
            }
            source.clip = entry.clip;
            source.volume = entry.volume;
            source.Play();
            if (id == "battle.card.hover") lastCardHoverTime = Time.unscaledTime;
            if (id == "title.button.hover") lastTitleButtonHoverTime = Time.unscaledTime;
        }
    }
}
