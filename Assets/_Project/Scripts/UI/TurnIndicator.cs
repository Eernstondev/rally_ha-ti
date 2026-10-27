using RallyAyiti.Race;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace RallyAyiti.UI
{
    /// <summary>
    /// Flèches de virage sur le bord de l'écran : annoncent le prochain virage serré (côté et force),
    /// comme les chevrons de Beach Buggy. Lit la courbure du tracé quelques dizaines de mètres devant le joueur.
    /// </summary>
    public sealed class TurnIndicator : MonoBehaviour
    {
        [SerializeField] private RaceManager race;
        [SerializeField] private SplineContainer line;
        [SerializeField] private CanvasGroup left;
        [SerializeField] private CanvasGroup right;
        [SerializeField] private float lookAhead = 40f;
        [SerializeField] private float minAngle = 32f;
        [SerializeField] private float strongAngle = 75f;

        private float _length, _timer, _angle;

        private void Start()
        {
            if (line != null) _length = line.CalculateLength();
            Set(left, 0f);
            Set(right, 0f);
        }

        private void Update()
        {
            LapTracker player = race != null ? race.Player : null;
            if (player == null || line == null || _length <= 0f) return;

            // La recherche du point le plus proche est le calcul coûteux : 8 fois par seconde suffit.
            if ((_timer -= Time.deltaTime) <= 0f)
            {
                _timer = 0.125f;
                _angle = AngleAhead(player.transform.position);
            }

            float strength = Mathf.InverseLerp(minAngle, strongAngle, Mathf.Abs(_angle));
            float alpha = Mathf.Abs(_angle) >= minAngle ? (0.55f + 0.45f * strength) * (0.75f + 0.25f * Mathf.Sin(Time.time * 10f)) : 0f;
            if (race.Phase != RacePhase.Running) alpha = 0f;
            Set(right, _angle > 0f ? alpha : 0f);
            Set(left, _angle < 0f ? alpha : 0f);
        }

        /// <summary>Angle signé (degrés, positif = à droite) entre la direction du tracé ici et un peu plus loin.</summary>
        private float AngleAhead(Vector3 worldPos)
        {
            float3 local = line.transform.InverseTransformPoint(worldPos);
            SplineUtility.GetNearestPoint(line.Spline, local, out _, out float t, 3, 2);
            float near = Mathf.Repeat(t + 8f / _length, 1f);
            float ahead = Mathf.Repeat(t + lookAhead / _length, 1f);
            Vector3 a = line.transform.TransformDirection((Vector3)line.Spline.EvaluateTangent(near));
            Vector3 b = line.transform.TransformDirection((Vector3)line.Spline.EvaluateTangent(ahead));
            a.y = b.y = 0f;
            return Vector3.SignedAngle(a, b, Vector3.up);
        }

        private static void Set(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }
    }
}
