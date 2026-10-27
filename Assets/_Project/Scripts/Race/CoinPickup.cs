using RallyAyiti.Audio;
using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.Race
{
    /// <summary>Pièce de 5 gourdes posée sur la piste : elle tourne, disparaît quand on la prend, revient plus tard.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CoinPickup : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float respawnDelay = 9f;

        private Collider _trigger;
        private float _hiddenUntil = -1f;
        private float _phase;

        private void Awake()
        {
            _trigger = GetComponent<Collider>();
            _trigger.isTrigger = true;
            _phase = Random.value * 6f;
        }

        private void Update()
        {
            if (_hiddenUntil > 0f && Time.time >= _hiddenUntil)
            {
                _hiddenUntil = -1f;
                _trigger.enabled = true;
                if (visual != null) visual.gameObject.SetActive(true);
            }
            if (visual == null || !visual.gameObject.activeSelf) return;
            visual.localRotation = Quaternion.Euler(0f, Time.time * 160f + _phase * 60f, 0f);
            visual.localPosition = new Vector3(0f, 1f + Mathf.Sin(Time.time * 3f + _phase) * 0.12f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!KartBodyLink.TryGetKart(other, out KartController kart)) return;
            if (kart.TryGetComponent(out LapTracker tracker) && tracker.IsPlayer && RaceManager.Current != null)
            {
                RaceManager.Current.AddCoin();
                GameAudio.Play(Sfx.Coin, 0.7f, 1f + Random.Range(-0.05f, 0.08f));
            }
            _trigger.enabled = false;
            if (visual != null) visual.gameObject.SetActive(false);
            _hiddenUntil = Time.time + respawnDelay;
        }
    }
}
