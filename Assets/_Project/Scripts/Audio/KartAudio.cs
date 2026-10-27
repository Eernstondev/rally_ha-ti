using RallyAyiti.Kart;
using RallyAyiti.Race;
using UnityEngine;

namespace RallyAyiti.Audio
{
    /// <summary>
    /// Moteur (régime et rapports simulés d'après la vitesse), crissement en drift, boost et klaxon.
    /// Le kart du joueur s'entend « dans la tête », les autres sont placés dans l'espace.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public sealed class KartAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip engine;
        [SerializeField] private AudioClip skid;
        [SerializeField] private AudioClip horn;
        [SerializeField] private AudioClip hornMelody;
        [Tooltip("Tonalité du moteur : moto plus aiguë, tap-tap plus grave.")]
        [SerializeField] private float enginePitch = 1f;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.55f;
        [Tooltip("Délai (s) entre deux coups de klaxon d'un pilote IA proche du joueur.")]
        [SerializeField] private Vector2 aiHornDelay = new(7f, 18f);

        private static readonly float[] GearTops = { 0.22f, 0.45f, 0.7f, 1.01f };

        private KartController _kart;
        private LapTracker _tracker;
        private RaceManager _race;
        private AudioSource _engine, _skid, _horn;
        private bool _wasBoosting;
        private float _aiHornTimer;
        private float _rpm;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            _tracker = GetComponent<LapTracker>();
        }

        private void Start()
        {
            _race = FindFirstObjectByType<RaceManager>();
            bool player = _tracker != null && _tracker.IsPlayer;
            float blend = player ? 0.15f : 1f;

            _engine = Source(engine, true, blend);
            _skid = Source(skid, true, blend);
            _horn = Source(null, false, blend);
            _skid.volume = 0f;
            if (_engine.clip != null)
            {
                _engine.time = Random.Range(0f, _engine.clip.length * 0.9f);
                _engine.Play();
            }
            if (_skid.clip != null) _skid.Play();
            if (!player) enginePitch *= Random.Range(0.94f, 1.06f);
            _aiHornTimer = Random.Range(aiHornDelay.x, aiHornDelay.y);
        }

        private AudioSource Source(AudioClip clip, bool loop, float blend)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = blend;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 6f;
            s.maxDistance = 120f;
            s.dopplerLevel = blend > 0.5f ? 0.4f : 0f;
            return s;
        }

        private void Update()
        {
            if (_engine == null) return;
            float dt = Time.deltaTime;
            float speed = _kart.Speed01;

            // Rapport engagé puis régime dans ce rapport : le son monte, retombe au passage de vitesse, remonte.
            int gear = 0;
            while (gear < GearTops.Length - 1 && speed > GearTops[gear]) gear++;
            float low = gear == 0 ? 0f : GearTops[gear - 1];
            float rpm = Mathf.InverseLerp(low, GearTops[gear], speed);
            float throttle = _kart.LastInput.Throttle;
            _rpm = Mathf.Lerp(_rpm, rpm, 1f - Mathf.Exp(-10f * dt));

            float pitch = enginePitch * (0.62f + 0.5f * _rpm + 0.09f * gear + (_kart.IsBoosting ? 0.12f : 0f));
            _engine.pitch = Mathf.Lerp(_engine.pitch, pitch, 1f - Mathf.Exp(-12f * dt));
            _engine.volume = engineVolume * (0.45f + 0.4f * Mathf.Max(throttle, speed) + (_kart.IsBoosting ? 0.15f : 0f));

            float skidTarget = _kart.IsDrifting && _kart.IsGrounded ? 0.5f + 0.15f * _kart.DriftTier : 0f;
            _skid.volume = Mathf.MoveTowards(_skid.volume, skidTarget, dt * 3f);
            _skid.pitch = 0.85f + 0.3f * speed;

            if (_kart.IsBoosting && !_wasBoosting)
            {
                if (_tracker != null && _tracker.IsPlayer) GameAudio.Play(Sfx.Boost, 0.8f);
                else GameAudio.PlayAt(Sfx.Boost, transform.position, 0.6f);
            }
            _wasBoosting = _kart.IsBoosting;

            if (_tracker != null && _tracker.IsPlayer)
            {
                if (_kart.LastInput.Horn) Honk(horn);
            }
            else if ((_aiHornTimer -= dt) <= 0f)
            {
                _aiHornTimer = Random.Range(aiHornDelay.x, aiHornDelay.y);
                LapTracker player = _race != null ? _race.Player : null;
                if (player != null && (player.transform.position - transform.position).sqrMagnitude < 30f * 30f)
                    Honk(Random.value < 0.4f ? hornMelody : horn);
            }
        }

        private void Honk(AudioClip clip)
        {
            if (clip == null || _horn.isPlaying) return;
            _horn.clip = clip;
            _horn.pitch = enginePitch > 1.1f ? 1.2f : 1f;
            _horn.volume = 0.9f;
            _horn.Play();
        }
    }
}
