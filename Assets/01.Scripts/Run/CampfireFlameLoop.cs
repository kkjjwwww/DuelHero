using System;
using UnityEngine;

namespace DuelHero.Run
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CampfireFlameLoop : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(.01f)] private float secondsPerFrame = 1f;

        private SpriteRenderer flame;
        private double startTime;
        private int currentFrame;

        private void Awake() => flame = GetComponent<SpriteRenderer>();

        private void OnEnable()
        {
            startTime = Time.timeAsDouble;
            currentFrame = 0;
            if (frames.Length > 0) flame.sprite = frames[0];
        }

        private void Update()
        {
            if (frames.Length < 2) return;
            var elapsedFrames = Math.Floor((Time.timeAsDouble - startTime) / secondsPerFrame);
            var nextFrame = (int)(elapsedFrames % frames.Length);
            if (nextFrame == currentFrame) return;
            currentFrame = nextFrame;
            flame.sprite = frames[nextFrame];
        }
    }
}
