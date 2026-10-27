using System;
using RallyAyiti.Audio;
using RallyAyiti.Kart;
using RallyAyiti.Race;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>
    /// Bonus tenu par un kart. Le joueur le lance avec la touche Bonus ; l'IA le lance après un court délai.
    /// Le tirage dépend de la place : les derniers reçoivent plus souvent des Kokoye.
    /// </summary>
    [RequireComponent(typeof(KartController), typeof(LapTracker))]
    public sealed class PowerUpSystem : MonoBehaviour
    {
        [SerializeField] private KokoyeProjectile kokoyePrefab;
        [SerializeField] private MangoTrap mangoPrefab;
        [SerializeField] private float kokoyeSpeed = 38f;
        [Tooltip("Délai (s) avant qu'un pilote IA utilise son bonus.")]
        [SerializeField] private Vector2 aiDelay = new(0.8f, 3.5f);

        public PowerUpType Current { get; private set; }
        public event Action<PowerUpType> Changed;

        private KartController _kart;
        private LapTracker _tracker;
        private RaceManager _race;
        private float _aiTimer;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            _tracker = GetComponent<LapTracker>();
        }

        private void Start() => _race = FindFirstObjectByType<RaceManager>();

        /// <summary>Donne un bonus si le kart n'en tient pas déjà un.</summary>
        public bool Grant()
        {
            if (Current != PowerUpType.None) return false;
            Current = Draw();
            _aiTimer = UnityEngine.Random.Range(aiDelay.x, aiDelay.y);
            if (_tracker.IsPlayer) GameAudio.Play(Sfx.ItemPickup, 0.8f);
            Changed?.Invoke(Current);
            return true;
        }

        private PowerUpType Draw()
        {
            float back = 0.5f;
            if (_race != null && _race.Standings.Count > 1)
                back = (_race.PositionOf(_tracker) - 1f) / (_race.Standings.Count - 1f);
            return UnityEngine.Random.value < 0.3f + 0.5f * back ? PowerUpType.Kokoye : PowerUpType.MangoTonbe;
        }

        private void Update()
        {
            if (Current == PowerUpType.None || !_kart.ControlEnabled || _kart.IsStunned) return;
            bool use = _tracker.IsPlayer ? _kart.LastInput.UseItem : (_aiTimer -= Time.deltaTime) <= 0f;
            if (use) Use();
        }

        private void Use()
        {
            Transform t = _kart.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;

            switch (Current)
            {
                case PowerUpType.Kokoye when kokoyePrefab != null:
                {
                    KokoyeProjectile k = Instantiate(kokoyePrefab, t.position + fwd * 2.6f + Vector3.up * 0.6f, Quaternion.LookRotation(fwd));
                    k.Launch(_kart, fwd, Mathf.Max(kokoyeSpeed, _kart.CurrentSpeed + 12f));
                    GameAudio.PlayAt(Sfx.KokoyeThrow, t.position, 0.9f);
                    break;
                }
                case PowerUpType.MangoTonbe when mangoPrefab != null:
                {
                    Vector3 pos = t.position - fwd * 2.8f;
                    if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                        pos = hit.point;
                    Instantiate(mangoPrefab, pos, Quaternion.LookRotation(fwd)).Init(_kart);
                    GameAudio.PlayAt(Sfx.KokoyeThrow, pos, 0.5f, 0.75f);
                    break;
                }
            }

            Current = PowerUpType.None;
            Changed?.Invoke(Current);
        }
    }
}
