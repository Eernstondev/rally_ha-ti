using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using RallyAyiti.Race;

namespace RallyAyiti.Kart
{
    /// <summary>
    /// Pilote IA : vise un point devant lui sur la ligne de course (spline fermée),
    /// décalé dans sa voie, et drifte dans les virages serrés.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public sealed class AIDriver : MonoBehaviour, IKartInput
    {
        [SerializeField] private SplineContainer racingLine;
        [Tooltip("-1 = côté gauche de la ligne, 0 = centre, 1 = côté droit.")]
        [SerializeField, Range(-1f, 1f)] private float laneOffset;
        [SerializeField] private float laneHalfWidth = 3f;
        [Tooltip("Distance de visée (m), allongée avec la vitesse.")]
        [SerializeField] private float lookAhead = 9f;
        [SerializeField] private float maxSteerAngle = 35f;
        [SerializeField] private float driftEnterAngle = 28f;
        [SerializeField] private float driftExitAngle = 10f;
        [Tooltip("Vitesse max relative : 0,9 = facile, 1 = égal au joueur.")]
        [SerializeField, Range(0.8f, 1.1f)] private float skill = 0.97f;

        private KartController _kart;
        private float _length;
        private bool _drifting;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            _length = Mathf.Max(1f, racingLine.CalculateLength());
        }

        [Header("Rattrapage (rubber band)")]
        [Tooltip("Bonus de vitesse max quand le kart est loin derrière le joueur.")]
        [SerializeField] private float catchUpBoost = 0.1f;
        [Tooltip("Ralentissement max quand il est loin devant (laisse une chance au joueur).")]
        [SerializeField] private float leadPenalty = 0.04f;
        [Tooltip("Écart (en checkpoints) pour lequel l'effet est maximal.")]
        [SerializeField] private float gapForFullEffect = 4f;

        private LapTracker _self;
        private float _baseScale;

        private void Start()
        {
            _self = GetComponent<LapTracker>();
            // Le joueur en pilote automatique (Taptap fou) garde ses améliorations de garage.
            _baseScale = IsPlayer ? _kart.SpeedScale : skill;
            _kart.SpeedScale = _baseScale;
        }

        private bool IsPlayer => _self != null && _self.IsPlayer;

        /// <summary>Niveau de l'IA (vitesse max relative), à régler avant le départ : le boss d'un duel va plus vite.</summary>
        public void SetSkill(float value) => skill = value;

        private void Update()
        {
            if (IsPlayer) return;
            RaceManager race = RaceManager.Current;
            LapTracker player = race != null ? race.Player : null;
            if (player == null || _self == null || player.Finished || _self.Finished)
            {
                _kart.SpeedScale = _baseScale;
                return;
            }
            float gap = (player.Progress - _self.Progress) / gapForFullEffect;
            float factor = gap >= 0f ? 1f + catchUpBoost * Mathf.Clamp01(gap) : 1f - leadPenalty * Mathf.Clamp01(-gap);
            _kart.SpeedScale = _baseScale * factor;
        }

        public KartInput ReadInput()
        {
            float3 local = racingLine.transform.InverseTransformPoint(transform.position);
            SplineUtility.GetNearestPoint(racingLine.Spline, local, out _, out float t);

            float distance = lookAhead + Mathf.Max(0f, _kart.CurrentSpeed) * 0.35f;
            float tAhead = Mathf.Repeat(t + distance / _length, 1f);
            racingLine.Evaluate(tAhead, out float3 pos, out float3 tangent, out float3 up);

            Vector3 right = Vector3.Cross(((Vector3)up).normalized, ((Vector3)tangent).normalized);
            Vector3 target = (Vector3)pos + right * (laneOffset * laneHalfWidth);

            float angle = Vector3.SignedAngle(transform.forward, target - transform.position, transform.up);
            float absAngle = Mathf.Abs(angle);

            if (!_drifting && absAngle > driftEnterAngle && _kart.CurrentSpeed > _kart.Stats.minDriftSpeed + 2f)
                _drifting = true;
            else if (_drifting && absAngle < driftExitAngle)
                _drifting = false;

            return new KartInput
            {
                Steer = Mathf.Clamp(angle / maxSteerAngle, -1f, 1f),
                Throttle = absAngle > 60f ? 0.6f : 1f,
                Drift = _drifting,
            };
        }
    }
}
