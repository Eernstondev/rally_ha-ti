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
        [Header("Inclinaison (roulis) dans les virages")]
        [SerializeField] private float dutchSteer = 9f;
        [SerializeField] private float dutchDrift = 6f;
        [Header("Arrivée : la caméra tourne autour du kart")]
        [SerializeField] private float orbitSpeed = 28f;
        [SerializeField] private float orbitDistance = 6.5f;
        [SerializeField] private float orbitHeight = 2.2f;

        private bool _orbiting;
        private float _orbitAngle;
        [SerializeField] private float dutchSmoothing = 4f;
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

        /// <summary>Une fois la ligne franchie, la caméra quitte la poursuite et tourne lentement autour du kart.</summary>
        private bool UpdateOrbit(float dt)
        {
            if (!_orbiting)
            {
                var tracker = kart.GetComponent<RallyAyiti.Race.LapTracker>();
                if (tracker == null || !tracker.Finished) return false;
                _orbiting = true;
                _orbitAngle = 180f;
                foreach (CinemachineComponentBase c in cam.GetComponents<CinemachineComponentBase>()) c.enabled = false;
                if (noise != null) noise.AmplitudeGain = 0f;
                LensSettings flat = cam.Lens;
                flat.Dutch = 0f;
                cam.Lens = flat;
            }
            _orbitAngle += orbitSpeed * dt;
            Vector3 target = kart.transform.position + Vector3.up * 0.8f;
            Vector3 offset = Quaternion.Euler(0f, kart.transform.eulerAngles.y + _orbitAngle, 0f) * new Vector3(0f, orbitHeight, -orbitDistance);
            cam.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(target - (target + offset)));
            return true;
        }

        private void OnLanded() => _shake = Mathf.Max(_shake, landShake);
        private void OnHit() => _shake = Mathf.Max(_shake, hitShake);

        private void LateUpdate()
        {
            if (cam == null || kart == null) return;
            float dt = Time.deltaTime;
            if (UpdateOrbit(dt)) return;
            float target = baseFov + speedFov * kart.Speed01 + (kart.IsBoosting ? boostFov : 0f);
            LensSettings lens = cam.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, target, 1f - Mathf.Exp(-smoothing * dt));
            // L'horizon penche du côté du virage, davantage en dérapage.
            float steer = Mathf.Clamp(kart.LastInput.Steer, -1f, 1f);
            float dutch = steer * dutchSteer * kart.Speed01 + (kart.IsDrifting ? kart.DriftDirection * dutchDrift : 0f);
            lens.Dutch = Mathf.Lerp(lens.Dutch, dutch, 1f - Mathf.Exp(-dutchSmoothing * dt));
            cam.Lens = lens;

            if (noise == null) return;
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 6f);
            float road = kart.IsGrounded ? roadShake * kart.Speed01 * (kart.IsBoosting ? 1.6f : 1f) : 0f;
            noise.AmplitudeGain = road + _shake;
            noise.FrequencyGain = 1f + kart.Speed01 * 1.5f + _shake;
        }
    }
}
