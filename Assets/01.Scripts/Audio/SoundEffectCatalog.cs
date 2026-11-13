using System;
using UnityEngine;

namespace DuelHero.Audio
{
    [CreateAssetMenu(menuName = "Duel Hero/Audio/Sound Effect Catalog")]
    public sealed class SoundEffectCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry Find(string id)
        {
            foreach (var entry in entries)
                if (entry != null && entry.id == id) return entry;
            return null;
        }
    }
}
