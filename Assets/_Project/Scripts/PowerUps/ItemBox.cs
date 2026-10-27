using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>Caisse de bonus flottante. Donne un bonus au kart qui la traverse, réapparaît après un délai.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ItemBox : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float respawnDelay = 2.5f;
        [SerializeField] private float spinSpeed = 70f;
        [SerializeField] private float bobHeight = 0.2f;

        private Collider _trigger;
        private Vector3 _basePos;
        private float _phase;
        private float _hiddenUntil = -1f;
        private float _grow = 1f;

        private void Awake()
        {
            _trigger = GetComponent<Collider>();
            _trigger.isTrigger = true;
            if (visual != null) _basePos = visual.localPosition;
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (_hiddenUntil > 0f && Time.time >= _hiddenUntil)
            {
                _hiddenUntil = -1f;
                _trigger.enabled = true;
                _grow = 0f;
                if (visual != null) visual.gameObject.SetActive(true);
            }
            if (visual == null || !visual.gameObject.activeSelf) return;

            _grow = Mathf.Min(1f, _grow + Time.deltaTime * 3f);
            visual.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, _grow);
            visual.localPosition = _basePos + Vector3.up * (Mathf.Sin(Time.time * 2f + _phase) * bobHeight);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!KartBodyLink.TryGetKart(other, out KartController kart)) return;
            if (kart.TryGetComponent(out PowerUpSystem powerUps)) powerUps.Grant();
            _trigger.enabled = false;
            if (visual != null) visual.gameObject.SetActive(false);
            _hiddenUntil = Time.time + respawnDelay;
        }
    }
}
