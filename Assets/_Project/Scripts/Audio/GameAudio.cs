using UnityEngine;

namespace RallyAyiti.Audio
{
    public enum Sfx { ItemPickup, KokoyeThrow, KokoyeHit, MangoSplat, Boost, CountdownBeep, CountdownGo, Finish, UiClick }

    /// <summary>Bruitages ponctuels : un son 2D pour l'interface, une petite réserve de sources 3D pour la course.</summary>
    public sealed class GameAudio : MonoBehaviour
    {
        [Tooltip("Un clip par valeur de Sfx, dans le même ordre.")]
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
        [SerializeField, Min(1)] private int voices = 10;

        private static GameAudio _instance;
        private AudioSource _flat;
        private AudioSource[] _pool;
        private int _next;

        private void Awake()
        {
            _instance = this;
            _flat = gameObject.AddComponent<AudioSource>();
            _flat.playOnAwake = false;
            _flat.spatialBlend = 0f;

            _pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("Son3D");
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 8f;
                s.maxDistance = 160f;
                s.dopplerLevel = 0f;
                _pool[i] = s;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private AudioClip Clip(Sfx sfx)
        {
            int i = (int)sfx;
            return clips != null && i < clips.Length ? clips[i] : null;
        }

        /// <summary>Son sans position (interface, décompte, son du joueur).</summary>
        public static void Play(Sfx sfx, float gain = 1f, float pitch = 1f)
        {
            if (_instance == null) return;
            AudioClip clip = _instance.Clip(sfx);
            if (clip == null) return;
            _instance._flat.pitch = pitch;
            _instance._flat.PlayOneShot(clip, _instance.volume * gain);
        }

        /// <summary>Son placé dans la scène (bonus des autres pilotes, impacts).</summary>
        public static void PlayAt(Sfx sfx, Vector3 position, float gain = 1f, float pitch = 1f)
        {
            if (_instance == null) return;
            AudioClip clip = _instance.Clip(sfx);
            if (clip == null) return;
            AudioSource s = _instance._pool[_instance._next];
            _instance._next = (_instance._next + 1) % _instance._pool.Length;
            s.transform.position = position;
            s.pitch = pitch;
            s.clip = clip;
            s.volume = _instance.volume * gain;
            s.Play();
        }
    }
}
