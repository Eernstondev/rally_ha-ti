using UnityEngine;

namespace RallyAyiti.Kart
{
    /// <summary>Réglages d'un véhicule. Un asset par véhicule (Tap-tap, Moto taxi, Buggy...).</summary>
    [CreateAssetMenu(menuName = "Rally Ayiti/Kart Stats", fileName = "KartStats")]
    public sealed class KartStats : ScriptableObject
    {
        [Header("Vitesse (m/s, m/s²)")]
        public float maxSpeed = 28f;
        public float reverseSpeed = 8f;
        public float acceleration = 14f;
        public float boostAcceleration = 40f;
        public float deceleration = 20f;

        [Header("Direction (°/s)")]
        public float steering = 95f;
        [Tooltip("Vitesse à laquelle le volant atteint la consigne.")]
        public float steerResponse = 10f;
        [Range(0f, 1f)] public float airSteerFactor = 0.35f;

        [Header("Adhérence (m/s² de correction latérale)")]
        public float grip = 60f;
        public float driftGrip = 18f;

        [Header("Drift")]
        public float minDriftSpeed = 10f;
        public float driftSteering = 85f;
        [Tooltip("Multiplicateur quand on braque vers l'intérieur du drift.")]
        public float driftTight = 1.5f;
        [Tooltip("Multiplicateur quand on contre-braque.")]
        public float driftWide = 0.45f;
        [Tooltip("Durée de drift (s) pour atteindre les paliers bleu, orange, violet.")]
        public Vector3 driftTierTimes = new(0.8f, 1.6f, 2.5f);
        [Tooltip("Durée du boost (s) obtenu pour chaque palier.")]
        public Vector3 driftBoostDurations = new(0.6f, 1.1f, 1.6f);
        public float driftVisualAngle = 22f;

        [Header("Boost")]
        public float boostSpeedMultiplier = 1.35f;
        public float trickBoostDuration = 0.5f;

        [Header("Physique")]
        public float extraGravity = 20f;
        public float groundCheckDistance = 0.4f;
        public float alignSpeed = 10f;

        [Header("Visuel")]
        public float leanAngle = 8f;
    }
}
