using System;
using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.Race
{
    /// <summary>
    /// Suit les tours d'un kart et calcule sa progression pour le classement.
    /// La grille de départ se place juste APRÈS la ligne d'arrivée.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public sealed class LapTracker : MonoBehaviour
    {
        [SerializeField] private string displayName = "Pilote";
        [SerializeField] private bool isPlayer;
        [Tooltip("En dessous de cette hauteur, le kart est replacé au dernier checkpoint.")]
        [SerializeField] private float fallHeight = -20f;

        public string DisplayName => displayName;
        public bool IsPlayer => isPlayer;
        /// <summary>Appelé par RaceSetup avant le départ, selon le pilote choisi au menu.</summary>
        public void SetPlayer(bool value) => isPlayer = value;
        public KartController Kart { get; private set; }
        /// <summary>Tours terminés.</summary>
        public int Lap { get; private set; }
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }

        public event Action<LapTracker> LapCompleted;

        private RaceManager _race;
        private int _next = 1;
        private int _passedThisLap;

        private void Awake() => Kart = GetComponent<KartController>();

        internal void Init(RaceManager race)
        {
            _race = race;
            Lap = 0;
            _next = race.CheckpointCount > 1 ? 1 : 0;
            _passedThisLap = 0;
            Finished = false;
        }

        /// <summary>Plus grand = plus avancé. Les karts arrivés passent devant, dans l'ordre d'arrivée.</summary>
        public float Progress
        {
            get
            {
                if (_race == null) return 0f;
                if (Finished) return 1_000_000f - FinishTime;

                int count = _race.CheckpointCount;
                Vector3 from = _race.GetCheckpoint((_next - 1 + count) % count).transform.position;
                Vector3 to = _race.GetCheckpoint(_next).transform.position;
                float segment = Mathf.Max(0.01f, Vector3.Distance(from, to));
                float fraction = 1f - Mathf.Clamp01(Vector3.Distance(transform.position, to) / segment);
                return Lap * count + _passedThisLap + fraction;
            }
        }

        internal void OnCheckpoint(Checkpoint cp)
        {
            if (_race == null || Finished || cp.Index != _next) return;

            if (cp.Index == 0)
            {
                Lap++;
                _passedThisLap = 0;
                LapCompleted?.Invoke(this);
            }
            else
            {
                _passedThisLap++;
            }
            _next = (cp.Index + 1) % _race.CheckpointCount;
        }

        internal void MarkFinished(float time)
        {
            Finished = true;
            FinishTime = time;
        }

        private void Update()
        {
            if (_race == null || Kart.Body.position.y > fallHeight) return;
            int count = _race.CheckpointCount;
            Transform last = _race.GetCheckpoint((_next - 1 + count) % count).transform;
            Kart.Respawn(last.position, last.rotation);
        }
    }
}
