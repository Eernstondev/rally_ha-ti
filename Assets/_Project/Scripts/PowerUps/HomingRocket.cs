using RallyAyiti.Audio;
using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>Fize : fusée qui file vers sa cible en suivant la piste, explose au contact.</summary>
    public sealed class HomingRocket : MonoBehaviour
    {
        [SerializeField] private float speed = 52f;
        [SerializeField] private float turnRate = 220f;
        [SerializeField] private float hitDuration = 1.3f;
        [SerializeField] private float lifetime = 7f;

        private KartController _owner, _target;
        private Vector3 _dir;
        private float _age;

        public void Launch(KartController owner, KartController target, Vector3 direction)
        {
            _owner = owner;
            _target = target;
            _dir = direction.normalized;
            transform.rotation = Quaternion.LookRotation(_dir);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age > lifetime)
            {
                Explode();
                return;
            }

            Vector3 pos = transform.position;
            if (_target != null && _age > 0.25f)
            {
                Vector3 aim = _target.transform.position + Vector3.up * 0.8f - pos;
                _dir = Vector3.RotateTowards(_dir, aim.normalized, turnRate * Mathf.Deg2Rad * dt, 0f);
                if (aim.sqrMagnitude < 2.2f * 2.2f)
                {
                    _target.ApplyHit(hitDuration);
                    Explode();
                    return;
                }
            }

            // Reste à hauteur de kart au-dessus du sol.
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 8f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = Mathf.Lerp(pos.y, ground.point.y + 1f, 1f - Mathf.Exp(-8f * dt));

            transform.SetPositionAndRotation(pos + _dir * speed * dt, Quaternion.LookRotation(_dir));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!KartBodyLink.TryGetKart(other, out KartController kart) || kart == _owner) return;
            kart.ApplyHit(hitDuration);
            Explode();
        }

        private void Explode()
        {
            GameAudio.PlayAt(Sfx.KokoyeHit, transform.position, 1f, 0.7f);
            RaceFx.Splash(transform.position, new Color(1f, 0.55f, 0.15f));
            Destroy(gameObject);
        }
    }
}
