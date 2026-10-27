using Unity.Cinemachine;
using UnityEngine;

namespace RallyAyiti.Kart
{
    /// <summary>
    /// Élargit le champ de vision avec la vitesse et pendant les boosts ; fait trembler la caméra
    /// selon la vitesse (vibrations de la piste), à l'atterrissage et quand le kart est touché.
    /// </summary>
    public sealed class KartCameraFx : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera cam;
        [SerializeField] private KartController kart;
        [SerializeField] private CinemachineBasicMultiChannelPerlin noise;
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float speedFov = 8f;
        [SerializeField] private float boostFov = 10f;
        [SerializeField] private float smoothing = 6f;
        [Header("Tremblements")]
        [SerializeField] private float roadShake = 0.35f;
        [SerializeField] private float landShake = 1.6f;
        [SerializeField] private float hitShake = 3f;

        private float _shake;

        public KartController Kart
        {
            get => kart;
            set
            {
                Unsubscribe();
                kart = value;
                Subscribe();
            }
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (kart == null || !isActiveAndEnabled) return;
            kart.Landed += OnLanded;
            kart.Hit += OnHit;
        }

        private void Unsubscribe()
        {
            if (kart == null) return;
            kart.Landed -= OnLanded;
            kart.Hit -= OnHit;
        }

        private void OnLanded() => _shake = Mathf.Max(_shake, landShake);
        private void OnHit() => _shake = Mathf.Max(_shake, hitShake);

        private void LateUpdate()
        {
            if (cam == null || kart == null) return;
            float dt = Time.deltaTime;
            float target = baseFov + speedFov * kart.Speed01 + (kart.IsBoosting ? boostFov : 0f);
            LensSettings lens = cam.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, target, 1f - Mathf.Exp(-smoothing * dt));
            cam.Lens = lens;

            if (noise == null) return;
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 6f);
            float road = kart.IsGrounded ? roadShake * kart.Speed01 * (kart.IsBoosting ? 1.6f : 1f) : 0f;
            noise.AmplitudeGain = road + _shake;
            noise.FrequencyGain = 1f + kart.Speed01 * 1.5f + _shake;
        }
    }
}
