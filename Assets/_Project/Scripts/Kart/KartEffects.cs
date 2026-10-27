using UnityEngine;

namespace RallyAyiti.Kart
{
    /// <summary>Étincelles de drift colorées par palier, flamme de boost, traces de pneus.</summary>
    [RequireComponent(typeof(KartController))]
    public sealed class KartEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] driftSparks;
        [SerializeField] private ParticleSystem boostFlame;
        [SerializeField] private TrailRenderer[] skidMarks;
        [Tooltip("Poussière ou sable soulevé par les roues arrière.")]
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private float dustPerSecond = 45f;
        [SerializeField] private float driftDustPerSecond = 70f;
        [Tooltip("Fumée blanche des roues arrière pendant les dérapages.")]
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private float smokePerSecond = 38f;

        [Header("Couleurs des paliers")]
        [SerializeField] private Color noTier = new(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color blue = new(0.25f, 0.6f, 1f);
        [SerializeField] private Color orange = new(1f, 0.55f, 0.1f);
        [SerializeField] private Color purple = new(0.7f, 0.3f, 1f);

        private KartController _kart;
        private bool _sparksOn, _flameOn;
        private int _tier = -1;

        private void Awake() => _kart = GetComponent<KartController>();

        private void Update()
        {
            bool drifting = _kart.IsDrifting && _kart.IsGrounded;

            if (drifting != _sparksOn)
            {
                _sparksOn = drifting;
                foreach (ParticleSystem ps in driftSparks)
                {
                    if (drifting) ps.Play();
                    else ps.Stop();
                }
                foreach (TrailRenderer trail in skidMarks) trail.emitting = drifting;
                if (!drifting) _tier = -1;
            }

            if (drifting && _kart.DriftTier != _tier)
            {
                _tier = _kart.DriftTier;
                Color c = _tier switch { 1 => blue, 2 => orange, 3 => purple, _ => noTier };
                foreach (ParticleSystem ps in driftSparks)
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = c;
                }
            }

            if (smoke != null)
            {
                // Grosse fumée blanche de dérapage, plus dense à chaque palier.
                ParticleSystem.EmissionModule emission = smoke.emission;
                float rate = drifting ? smokePerSecond * (0.6f + 0.25f * Mathf.Max(0, _kart.DriftTier)) * Mathf.Max(0.4f, _kart.Speed01) : 0f;
                emission.rateOverTime = rate;
                if (rate > 0.5f && !smoke.isPlaying) smoke.Play();
            }

            if (dust != null)
            {
                ParticleSystem.EmissionModule emission = dust.emission;
                float rate = _kart.IsGrounded ? _kart.Speed01 * dustPerSecond + (drifting ? driftDustPerSecond : 0f) : 0f;
                emission.rateOverTime = rate;
                if (rate > 0.5f && !dust.isPlaying) dust.Play();
            }

            bool boosting = _kart.IsBoosting;
            if (boosting != _flameOn && boostFlame != null)
            {
                _flameOn = boosting;
                if (boosting) RallyAyiti.PowerUps.RaceFx.Sparkle(transform.position + Vector3.up * 1f - transform.forward * 0.8f, new Color(1f, 0.8f, 0.35f), 16);
                if (boosting) boostFlame.Play();
                else boostFlame.Stop();
            }
        }
    }
}
