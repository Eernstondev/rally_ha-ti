using RallyAyiti.Race;
using UnityEngine;

namespace RallyAyiti.Audio
{
    /// <summary>Ambiance du circuit, bips du décompte, musique konpa au départ, fanfare à l'arrivée.</summary>
    public sealed class RaceAudio : MonoBehaviour
    {
        [SerializeField] private RaceManager race;
        [SerializeField] private AudioClip music;
        [SerializeField] private AudioClip ambience;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.45f;

        private AudioSource _music, _ambience;
        private float _musicTarget;

        private void Awake()
        {
            _music = Loop(music);
            _ambience = Loop(ambience);
        }

        private AudioSource Loop(AudioClip clip)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        private void OnEnable()
        {
            if (race == null) return;
            race.CountdownTick += OnCountdown;
            race.RacerFinished += OnRacerFinished;
        }

        private void OnDisable()
        {
            if (race == null) return;
            race.CountdownTick -= OnCountdown;
            race.RacerFinished -= OnRacerFinished;
        }

        private void Start()
        {
            if (_ambience.clip != null) _ambience.Play();
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-2f * Time.deltaTime);
            _ambience.volume = Mathf.Lerp(_ambience.volume, ambienceVolume, k);
            _music.volume = Mathf.Lerp(_music.volume, _musicTarget, k);
        }

        private void OnCountdown(int n)
        {
            if (n > 0)
            {
                GameAudio.Play(Sfx.CountdownBeep);
                return;
            }
            GameAudio.Play(Sfx.CountdownGo);
            if (_music.clip == null) return;
            _music.Play();
            _musicTarget = musicVolume;
        }

        private void OnRacerFinished(LapTracker racer)
        {
            if (!racer.IsPlayer) return;
            GameAudio.Play(Sfx.Finish);
            _musicTarget = musicVolume * 0.45f;
        }
    }
}
