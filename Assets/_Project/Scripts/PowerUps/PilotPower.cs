using System;
using System.Collections;
using RallyAyiti.Audio;
using RallyAyiti.Kart;
using RallyAyiti.Race;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    public enum PilotPowerType
    {
        /// <summary>Ti Djo : klaxon géant qui repousse les karts proches.</summary>
        KlaksonJeyan,
        /// <summary>Manmzèl Loudena : pluie de mangues derrière elle.</summary>
        LapliMango,
        /// <summary>Bòs Wilfrid : répare ses dégâts et repart en boost.</summary>
        Repare,
        /// <summary>Nadège : bouclier électrique de 4 s.</summary>
        BouclyeElektrik,
        /// <summary>Judens Konpa : onde sonore qui étourdit autour de lui.</summary>
        OnnKonpa,
        /// <summary>Sè Naïelie : soigne et protège pendant 3 s.</summary>
        Swen,
    }

    /// <summary>
    /// Pouvoir spécial du pilote : il se charge pendant la course (plus vite quand on est derrière),
    /// le joueur le déclenche avec la touche Pouvoir, l'IA quand la situation s'y prête.
    /// </summary>
    [RequireComponent(typeof(KartController), typeof(LapTracker))]
    public sealed class PilotPower : MonoBehaviour
    {
        [SerializeField] private PilotPowerType power;
        [SerializeField] private string powerName = "Pouvwa";
        [SerializeField] private float chargeTime = 24f;
        [SerializeField] private MangoTrap mangoPrefab;
        [SerializeField] private AudioClip hornClip;

        public PilotPowerType Power => power;
        public string PowerName => powerName;
        public float Charge01 { get; private set; }
        public bool Ready => Charge01 >= 1f;
        public event Action Used;

        private KartController _kart;
        private LapTracker _tracker;
        private float _aiWait;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            _tracker = GetComponent<LapTracker>();
        }

        private void Update()
        {
            if (!_kart.ControlEnabled) return;
            RaceManager race = RaceManager.Current;
            float back = 0.5f;
            if (race != null && race.Standings.Count > 1)
                back = (race.PositionOf(_tracker) - 1f) / (race.Standings.Count - 1f);
            if (!Ready) Charge01 = Mathf.Min(1f, Charge01 + Time.deltaTime / chargeTime * (0.7f + 0.8f * back));
            if (!Ready) return;

            if (_tracker.IsPlayer)
            {
                if (_kart.LastInput.Power) Use();
                return;
            }
            _aiWait += Time.deltaTime;
            if (AiWantsToUse() || _aiWait > 8f) Use();
        }

        private bool AiWantsToUse()
        {
            switch (power)
            {
                case PilotPowerType.KlaksonJeyan:
                case PilotPowerType.OnnKonpa:
                    return CountNear(12f) > 0;
                case PilotPowerType.Repare:
                case PilotPowerType.Swen:
                    return _kart.IsStunned || _kart.IsSlowed;
                case PilotPowerType.LapliMango:
                    return CountBehind(25f) > 0;
                default:
                    return _aiWait > 2f;
            }
        }

        private int CountNear(float radius)
        {
            RaceManager race = RaceManager.Current;
            if (race == null) return 0;
            int n = 0;
            foreach (LapTracker r in race.Standings)
                if (r.Kart != _kart && (r.Kart.transform.position - transform.position).sqrMagnitude < radius * radius) n++;
            return n;
        }

        private int CountBehind(float radius)
        {
            RaceManager race = RaceManager.Current;
            if (race == null) return 0;
            int n = 0;
            foreach (LapTracker r in race.Standings)
            {
                Vector3 d = r.Kart.transform.position - transform.position;
                if (r.Kart != _kart && d.sqrMagnitude < radius * radius && Vector3.Dot(d, transform.forward) < 0f) n++;
            }
            return n;
        }

        private void Use()
        {
            Charge01 = 0f;
            _aiWait = 0f;
            Vector3 pos = transform.position;
            switch (power)
            {
                case PilotPowerType.KlaksonJeyan:
                    RaceFx.Blast(_kart, pos, 14f, 0.7f, 18f, new Color(0.96f, 0.76f, 0.17f));
                    if (hornClip != null) AudioSource.PlayClipAtPoint(hornClip, pos, 1f);
                    break;
                case PilotPowerType.LapliMango:
                    StartCoroutine(MangoRain());
                    break;
                case PilotPowerType.Repare:
                    _kart.ClearStun();
                    _kart.ApplyBoost(2.5f, 1.3f);
                    RaceFx.Splash(pos, new Color(0.25f, 0.85f, 0.35f));
                    break;
                case PilotPowerType.BouclyeElektrik:
                    _kart.ApplyShield(4f, false);
                    RaceFx.Bubble(_kart, new Color(0.2f, 0.75f, 1f));
                    break;
                case PilotPowerType.OnnKonpa:
                    RaceFx.Blast(_kart, pos, 16f, 1f, 0f, new Color(0.55f, 0.36f, 0.9f));
                    break;
                case PilotPowerType.Swen:
                    _kart.ClearStun();
                    _kart.ApplyShield(3f, false);
                    _kart.ApplyBoost(1.2f, 1.2f);
                    RaceFx.Bubble(_kart, new Color(0.95f, 0.45f, 0.7f));
                    break;
            }
            if (_tracker.IsPlayer) GameAudio.Play(Sfx.Power);
            else GameAudio.PlayAt(Sfx.Power, pos, 0.7f);
            Used?.Invoke();
        }

        private IEnumerator MangoRain()
        {
            if (mangoPrefab == null) yield break;
            for (int i = 0; i < 5; i++)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd) * UnityEngine.Random.Range(-3f, 3f);
                Vector3 p = transform.position - fwd * 3f + side;
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                    p = hit.point;
                Instantiate(mangoPrefab, p, Quaternion.LookRotation(fwd)).Init(_kart);
                yield return new WaitForSeconds(0.22f);
            }
        }
    }
}
