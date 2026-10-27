using RallyAyiti.Audio;
using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>Noix de coco lancée à vitesse constante. Rebondit sur les murs, fait tourner le kart touché.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KokoyeProjectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private float hitDuration = 1.2f;
        [SerializeField] private float radius = 0.35f;
        [SerializeField] private Transform visual;

        private Rigidbody _rb;
        private Vector3 _dir = Vector3.forward;
        private float _speed;
        private float _age;
        private KartController _owner;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (Collider c in GetComponents<Collider>())
            {
                if (c.isTrigger) continue;
                c.sharedMaterial = new PhysicsMaterial("KokoyeBounce")
                {
                    bounciness = 0.8f,
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                };
            }
        }

        public void Launch(KartController owner, Vector3 direction, float speed)
        {
            _owner = owner;
            _dir = direction.normalized;
            _speed = speed;
            _rb.linearVelocity = _dir * _speed;
        }

        private void FixedUpdate()
        {
            Vector3 v = _rb.linearVelocity;
            var flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude > 1f) _dir = flat.normalized;
            _rb.linearVelocity = _dir * _speed + Vector3.up * v.y;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (visual != null)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, _dir);
                visual.Rotate(axis, _speed / radius * Mathf.Rad2Deg * Time.deltaTime, Space.World);
            }
            if (_age > lifetime) Destroy(gameObject);
        }

        /// <summary>Appelé par la zone de touche (trigger enfant).</summary>
        internal void OnHitbox(Collider other)
        {
            if (!KartBodyLink.TryGetKart(other, out KartController kart)) return;
            if (kart == _owner && _age < 0.5f) return;
            kart.ApplyHit(hitDuration);
            GameAudio.PlayAt(Sfx.KokoyeHit, transform.position);
            Destroy(gameObject);
        }
    }
}
