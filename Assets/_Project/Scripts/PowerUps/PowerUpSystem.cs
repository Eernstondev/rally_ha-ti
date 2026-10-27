using System;
using System.Collections;
using RallyAyiti.Audio;
using RallyAyiti.Kart;
using RallyAyiti.Race;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>
    /// Bonus tenu par un kart. Le joueur le lance avec la touche Bonus ; l'IA choisit son moment.
    /// Le tirage dépend de la place : les premiers reçoivent surtout des pièges et des défenses,
    /// les derniers des attaques, des boosts, l'orage et le tap-tap fou.
    /// </summary>
    [RequireComponent(typeof(KartController), typeof(LapTracker))]
    public sealed class PowerUpSystem : MonoBehaviour
    {
        public static readonly string[] Names =
            { "", "Kokoye", "Mango tonbe", "Twa kokoye", "Fize", "Po bannann", "Lanbi", "Kafe", "Parasòl", "Loraj", "Pikliz", "Tap-tap fou" };

        /// <summary>Un kart a reçu du pikliz (le HUD barbouille l'écran si c'est le joueur).</summary>
        public static event Action<KartController> Splatted;

        [SerializeField] private KokoyeProjectile kokoyePrefab;
        [SerializeField] private MangoTrap mangoPrefab;
        [SerializeField] private MangoTrap bananaPrefab;
        [SerializeField] private HomingRocket rocketPrefab;
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

        private float Back()
        {
            if (_race == null || _race.Standings.Count < 2) return 0.5f;
            return (_race.PositionOf(_tracker) - 1f) / (_race.Standings.Count - 1f);
        }

        private PowerUpType Draw()
        {
            float b = Back();
            bool last = b > 0.99f;
            // Poids selon la place : b = 0 en tête, 1 en dernière position.
            (PowerUpType type, float weight)[] table =
            {
                (PowerUpType.Kokoye, 3f),
                (PowerUpType.MangoTonbe, Mathf.Lerp(3f, 1f, b)),
                (PowerUpType.PoBannann, Mathf.Lerp(3f, 0.5f, b)),
                (PowerUpType.Parasol, Mathf.Lerp(3f, 0.3f, b)),
                (PowerUpType.Lanbi, 2f),
                (PowerUpType.TwaKokoye, Mathf.Lerp(0f, 3f, b)),
                (PowerUpType.Fize, Mathf.Lerp(0f, 3f, b)),
                (PowerUpType.Pikliz, Mathf.Lerp(0f, 2f, b)),
                (PowerUpType.Kafe, Mathf.Lerp(0.6f, 3f, b)),
                (PowerUpType.Loraj, last ? 1.2f : 0f),
                (PowerUpType.TaptapFou, b > 0.75f ? 1f : 0f),
            };
            float total = 0f;
            foreach (var entry in table) total += entry.weight;
            float roll = UnityEngine.Random.value * total;
            foreach (var entry in table)
            {
                roll -= entry.weight;
                if (roll <= 0f) return entry.type;
            }
            return PowerUpType.Kokoye;
        }

        private void Update()
        {
            if (Current == PowerUpType.None || !_kart.ControlEnabled || _kart.IsStunned) return;
            if (_tracker.IsPlayer)
            {
                if (_kart.LastInput.UseItem) Use();
                return;
            }
            if ((_aiTimer -= Time.deltaTime) > 0f) return;
            // L'IA garde la conque pour le moment où un adversaire est tout près (6 s au plus).
            if (Current == PowerUpType.Lanbi && NearestOther(12f) == null && _aiTimer > -6f) return;
            Use();
        }

        private KartController AheadTarget()
        {
            if (_race == null) return null;
            int pos = _race.PositionOf(_tracker);
            return pos > 1 ? _race.Standings[pos - 2].Kart : null;
        }

        private KartController NearestOther(float radius)
        {
            if (_race == null) return null;
            KartController best = null;
            float bestSq = radius * radius;
            foreach (LapTracker r in _race.Standings)
            {
                if (r.Kart == _kart) continue;
                float sq = (r.Kart.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = r.Kart; }
            }
            return best;
        }

        private void Use()
        {
            Transform t = _kart.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            PowerUpType used = Current;
            Current = PowerUpType.None;
            Changed?.Invoke(Current);

            switch (used)
            {
                case PowerUpType.Kokoye:
                    Throw(fwd);
                    break;
                case PowerUpType.TwaKokoye:
                    foreach (float angle in new[] { -9f, 0f, 9f }) Throw(Quaternion.Euler(0f, angle, 0f) * fwd);
                    break;
                case PowerUpType.Fize when rocketPrefab != null:
                {
                    HomingRocket rocket = Instantiate(rocketPrefab, t.position + fwd * 2.8f + Vector3.up * 1f, Quaternion.LookRotation(fwd));
                    rocket.Launch(_kart, AheadTarget(), fwd);
                    GameAudio.PlayAt(Sfx.Rocket, t.position);
                    break;
                }
                case PowerUpType.MangoTonbe:
                    Drop(mangoPrefab, fwd);
                    break;
                case PowerUpType.PoBannann:
                    Drop(bananaPrefab, fwd);
                    break;
                case PowerUpType.Lanbi:
                    RaceFx.Blast(_kart, t.position, 13f, 0.9f, 16f, new Color(1f, 0.85f, 0.6f));
                    GameAudio.PlayAt(Sfx.Lanbi, t.position);
                    break;
                case PowerUpType.Kafe:
                    _kart.ApplyBoost(3f, 1.35f);
                    break;
                case PowerUpType.Parasol:
                    _kart.ApplyShield(10f, true);
                    RaceFx.Bubble(_kart, new Color(0.96f, 0.76f, 0.17f));
                    GameAudio.PlayAt(Sfx.Shield, t.position);
                    break;
                case PowerUpType.Loraj:
                    Storm();
                    break;
                case PowerUpType.Pikliz:
                {
                    KartController target = AheadTarget();
                    if (target == null) break;
                    target.ApplyConfuse(2.2f);
                    RaceFx.Splash(target.transform.position, new Color(1f, 0.45f, 0.1f));
                    GameAudio.PlayAt(Sfx.MangoSplat, target.transform.position, 1f, 1.3f);
                    Splatted?.Invoke(target);
                    break;
                }
                case PowerUpType.TaptapFou:
                    StartCoroutine(Autopilot(4f));
                    break;
            }
        }

        private void Throw(Vector3 dir)
        {
            if (kokoyePrefab == null) return;
            Transform t = _kart.transform;
            KokoyeProjectile k = Instantiate(kokoyePrefab, t.position + dir * 2.6f + Vector3.up * 0.6f, Quaternion.LookRotation(dir));
            k.Launch(_kart, dir, Mathf.Max(kokoyeSpeed, _kart.CurrentSpeed + 12f));
            GameAudio.PlayAt(Sfx.KokoyeThrow, t.position, 0.9f);
        }

        private void Drop(MangoTrap prefab, Vector3 fwd)
        {
            if (prefab == null) return;
            Vector3 pos = _kart.transform.position - fwd * 2.8f;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point;
            Instantiate(prefab, pos, Quaternion.LookRotation(fwd)).Init(_kart);
            GameAudio.PlayAt(Sfx.KokoyeThrow, pos, 0.5f, 0.75f);
        }

        /// <summary>Loraj : la foudre ralentit tous les karts devant.</summary>
        private void Storm()
        {
            if (_race == null) return;
            int pos = _race.PositionOf(_tracker);
            for (int i = 0; i < pos - 1; i++)
            {
                KartController k = _race.Standings[i].Kart;
                k.ApplySlow(2.5f, 0.55f);
                RaceFx.Bolt(k.transform.position);
            }
            GameAudio.Play(Sfx.Thunder);
        }

        /// <summary>Tap-tap fou : l'IA prend le volant, super vitesse, rien ne l'arrête.</summary>
        private IEnumerator Autopilot(float duration)
        {
            _kart.ApplyBoost(duration, 1.45f);
            _kart.ApplyShield(duration, false);
            _kart.ClearStun();
            RaceFx.Bubble(_kart, new Color(0.89f, 0.21f, 0.48f));
            GameAudio.PlayAt(Sfx.Power, _kart.transform.position);
            if (!_tracker.IsPlayer) yield break;

            var ai = GetComponent<AIDriver>();
            var human = GetComponent<PlayerKartInput>();
            if (ai == null || human == null) yield break;
            _kart.SetInput(ai);
            yield return new WaitForSeconds(duration);
            _kart.SetInput(human);
        }
    }
}
