using UnityEngine;

namespace DuelHero.Map
{
    public sealed class RouteMapController : MonoBehaviour
    {
        [SerializeField] private uint previewSeed = 42;
        [SerializeField] private RouteMapView view;

        public RouteMap CurrentMap { get; private set; }

        private void Start() => GeneratePreview();

        [ContextMenu("Generate fixed seed preview")]
        public void GeneratePreview()
        {
            CurrentMap = RouteMapGenerator.Generate(previewSeed);
            view.Render(CurrentMap);
        }
    }
}
