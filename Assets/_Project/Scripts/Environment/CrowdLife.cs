using RallyAyiti.Race;
using UnityEngine;

namespace RallyAyiti.Environment
{
    /// <summary>
    /// Foule vivante, façon Beach Buggy : les supporters sautillent, les passants se balancent, et tout le monde
    /// saute plus haut quand un kart passe tout près. Un seul composant anime toute la foule (pas d'Animator).
    /// </summary>
    public sealed class CrowdLife : MonoBehaviour
    {
        [SerializeField] private Transform[] people;
        [Tooltip("Vrai pour les supporters qui acclament (bras levés), faux pour les passants.")]
        [SerializeField] private bool[] cheering;
        [SerializeField] private float hopHeight = 0.18f;
        [SerializeField] private float excitedHop = 0.5f;
        [SerializeField] private float excitementRadius = 16f;

        private Vector3[] _basePos;
        private Quaternion[] _baseRot;
        private float[] _phase, _speed, _excite;
        private int _next;

        private void Start()
        {
            int n = people.Length;
            _basePos = new Vector3[n];
            _baseRot = new Quaternion[n];
            _phase = new float[n];
            _speed = new float[n];
            _excite = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (people[i] == null) continue;
                people[i].GetLocalPositionAndRotation(out _basePos[i], out _baseRot[i]);
                _phase[i] = Random.value * 10f;
                _speed[i] = Random.Range(6.5f, 9.5f);
            }
        }

        private void Update()
        {
            UpdateExcitement();
            float t = Time.time, dt = Time.deltaTime;
            for (int i = 0; i < people.Length; i++)
            {
                Transform p = people[i];
                if (p == null) continue;
                _excite[i] = Mathf.MoveTowards(_excite[i], 0f, dt * 0.6f);
                bool cheer = i < cheering.Length && cheering[i];
                float e = _excite[i];
                float hop = (cheer ? hopHeight : 0f) + e * excitedHop;
                float bounce = Mathf.Abs(Mathf.Sin(t * _speed[i] * (1f + e * 0.6f) + _phase[i])) * hop;
                float sway = Mathf.Sin(t * 1.3f + _phase[i]) * (cheer ? 4f : 6f) + Mathf.Sin(t * 9f + _phase[i]) * e * 6f;
                p.SetLocalPositionAndRotation(_basePos[i] + Vector3.up * bounce, _baseRot[i] * Quaternion.Euler(0f, 0f, sway));
            }
        }

        /// <summary>Par petits lots (un douzième de la foule par image) : quels spectateurs voient passer un kart.</summary>
        private void UpdateExcitement()
        {
            RaceManager race = RaceManager.Current;
            if (race == null || people.Length == 0) return;
            int batch = Mathf.Max(1, people.Length / 12);
            float r2 = excitementRadius * excitementRadius;
            for (int k = 0; k < batch; k++)
            {
                _next = (_next + 1) % people.Length;
                Transform p = people[_next];
                if (p == null) continue;
                Vector3 pos = p.position;
                foreach (LapTracker racer in race.Standings)
                {
                    if ((racer.transform.position - pos).sqrMagnitude > r2) continue;
                    _excite[_next] = 1f;
                    break;
                }
            }
        }
    }
}
