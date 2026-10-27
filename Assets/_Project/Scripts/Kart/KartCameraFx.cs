using Unity.Cinemachine;
using UnityEngine;

namespace RallyAyiti.Kart
{
    /// <summary>Élargit le champ de vision avec la vitesse et pendant les boosts.</summary>
    public sealed class KartCameraFx : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera cam;
        [SerializeField] private KartController kart;
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float speedFov = 8f;
        [SerializeField] private float boostFov = 10f;
        [SerializeField] private float smoothing = 6f;

        public KartController Kart { get => kart; set => kart = value; }

        private void LateUpdate()
        {
            if (cam == null || kart == null) return;
            float target = baseFov + speedFov * kart.Speed01 + (kart.IsBoosting ? boostFov : 0f);
            LensSettings lens = cam.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            cam.Lens = lens;
        }
    }
}
