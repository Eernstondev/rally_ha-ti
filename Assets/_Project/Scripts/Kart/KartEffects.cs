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

            bool boosting = _kart.IsBoosting;
            if (boosting != _flameOn && boostFlame != null)
            {
                _flameOn = boosting;
                if (boosting) boostFlame.Play();
                else boostFlame.Stop();
            }
        }
    }
}
