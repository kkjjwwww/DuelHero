using UnityEngine;

namespace DuelHero.Battle
{
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraController : MonoBehaviour
    {
        private Camera targetCamera;
        private Vector3 basePosition, battlePosition;
        private Quaternion baseRotation;
        private float baseSize, baseFov;
        private float baseZoom = 1f, battleZoom = 1f, battlePitch;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            basePosition = transform.position;
            baseRotation = transform.rotation;
            baseSize = targetCamera.orthographicSize;
            baseFov = targetCamera.fieldOfView;
        }

        // Future input changes the base view without overwriting battle offsets.
        public void SetBaseView(Vector3 position, Quaternion rotation, float zoom)
        {
            basePosition = position;
            baseRotation = rotation;
            baseZoom = zoom;
            ApplyView();
        }

        public void SetBattleView(Vector3 positionOffset, float pitchOffset, float zoom)
        {
            battlePosition = positionOffset;
            battlePitch = pitchOffset;
            battleZoom = zoom;
            ApplyView();
        }

        public void ResetBattleView() => SetBattleView(Vector3.zero, 0f, 1f);

        private void ApplyView()
        {
            if (targetCamera == null) return;
            // Position offsets use world axes.
            transform.SetPositionAndRotation(basePosition + battlePosition,
                baseRotation * Quaternion.Euler(battlePitch, 0f, 0f));
            float zoom = baseZoom * battleZoom;
            if (targetCamera.orthographic) targetCamera.orthographicSize = baseSize / zoom;
            else targetCamera.fieldOfView = baseFov / zoom;
        }

        private void OnDisable() => ResetBattleView();
    }
}
