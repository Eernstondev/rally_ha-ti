using RallyAyiti.Audio;
using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>Mangues posées sur la route : le premier kart qui passe dessus dérape.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MangoTrap : MonoBehaviour
    {
        [SerializeField] private float lifetime = 30f;
        [SerializeField] private float hitDuration = 0.9f;

        private KartController _owner;
        private float _age;

        public void Init(KartController owner) => _owner = owner;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            transform.localScale = Vector3.one * 0.01f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Min(1f, _age / 0.2f);
            if (_age > lifetime) Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!KartBodyLink.TryGetKart(other, out KartController kart)) return;
            if (kart == _owner && _age < 0.8f) return;
            kart.ApplyHit(hitDuration);
            GameAudio.PlayAt(Sfx.MangoSplat, transform.position);
            Destroy(gameObject);
        }
    }
}
