using System;
using UnityEngine;

namespace RallyAyiti.Kart
{
    /// <summary>
    /// Conduite arcade : une sphère Rigidbody porte la vitesse et les collisions,
    /// cet objet la suit et s'aligne sur le sol. Le joueur et l'IA passent par IKartInput.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartController : MonoBehaviour
    {
        [SerializeField] private KartStats stats;
        [Tooltip("Sphère enfant avec Rigidbody + SphereCollider. Elle est détachée au démarrage.")]
        [SerializeField] private Rigidbody sphere;
        [Tooltip("Racine du modèle 3D (inclinaison, drift, rotations de figure).")]
        [SerializeField] private Transform visual;
        [Tooltip("Sol et décor. Exclure le layer des karts.")]
        [SerializeField] private LayerMask groundMask = ~0;

        public KartStats Stats => stats;
        public Rigidbody Body => sphere;
        public bool ControlEnabled { get; set; }
        /// <summary>Multiplicateur de vitesse max (niveau de l'IA, élastique).</summary>
        public float SpeedScale { get; set; } = 1f;
        public float CurrentSpeed => _speed;
        public float Speed01 => Mathf.Clamp01(Mathf.Abs(_speed) / stats.maxSpeed);
        public bool IsGrounded => _grounded;
        public bool IsDrifting => _drifting;
        public int DriftDirection => _driftDir;
        /// <summary>0 = pas de palier, 1 bleu, 2 orange, 3 violet.</summary>
        public int DriftTier => _driftTier;
        public bool IsBoosting => _boostTimer > 0f;
        public bool IsStunned => _stunTimer > 0f;
        public KartInput LastInput => _input;

        public event Action<int> DriftReleased;
        public event Action Landed;
        public event Action Hit;

        private IKartInput _source;
        private KartInput _input;
        private bool _prevDrift;

        private float _speed;
        private float _yawVelocity;
        private float _sphereRadius;

        private bool _grounded;
        private Vector3 _groundNormal = Vector3.up;

        private bool _drifting;
        private int _driftDir;
        private float _driftTime;
        private int _driftTier;

        private float _boostTimer;
        private float _boostMultiplier = 1f;

        private float _stunTimer;
        private float _stunDuration = 1f;

        private bool _trickQueued;
        private float _trickAngle;
        private float _visualYaw;

        private void Awake()
        {
            foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
            {
                if (mb is not IKartInput src || !mb.enabled) continue;
                _source = src;
                break;
            }

            var col = sphere.GetComponent<SphereCollider>();
            _sphereRadius = col.radius * sphere.transform.lossyScale.x;
            // Sans frottement : c'est KartController qui décide de la vitesse, pas le contact avec le sol.
            if (col.sharedMaterial == null)
            {
                col.sharedMaterial = new PhysicsMaterial("KartBall")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            }

            sphere.transform.SetParent(null, true);
            sphere.interpolation = RigidbodyInterpolation.Interpolate;
            sphere.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            sphere.constraints = RigidbodyConstraints.FreezeRotation;
            sphere.linearDamping = 0f;
            sphere.angularDamping = 0f;

            sphere.gameObject.AddComponent<KartBodyLink>().Controller = this;
        }

        /// <summary>Change la source des commandes (joueur ou IA).</summary>
        public void SetInput(IKartInput source) => _source = source;

        private void OnDestroy()
        {
            if (sphere != null) Destroy(sphere.gameObject);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _input = ControlEnabled && _source != null ? _source.ReadInput() : default;

            UpdateTimers(dt);
            UpdateSpeed(dt);
            UpdateSteering(dt);
            FollowSphere(dt);
            UpdateVisual(dt);

            _prevDrift = _input.Drift;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool wasGrounded = _grounded;

            _grounded = Physics.Raycast(sphere.position, Vector3.down, out RaycastHit hit,
                _sphereRadius + stats.groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
            _groundNormal = _grounded ? hit.normal : Vector3.up;
            if (_grounded && !wasGrounded) OnLand();

            sphere.AddForce(Vector3.down * stats.extraGravity, ForceMode.Acceleration);
            if (!_grounded) return;

            Vector3 velocity = sphere.linearVelocity;
            Vector3 normalPart = Vector3.Project(velocity, _groundNormal);
            Vector3 planar = velocity - normalPart;
            float actualSpeed = planar.magnitude;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;
            float grip = _drifting ? stats.driftGrip : stats.grip;
            planar = Vector3.MoveTowards(planar, forward * _speed, grip * dt);
            sphere.linearVelocity = planar + normalPart;

            // Bloqué contre un mur : on réduit la consigne pour ne pas pousser indéfiniment.
            if (Mathf.Abs(_speed) > 4f && actualSpeed < Mathf.Abs(_speed) * 0.4f)
                _speed = Mathf.MoveTowards(_speed, actualSpeed * Mathf.Sign(_speed), stats.deceleration * 2f * dt);
        }

        // ---------- API publique ----------

        /// <summary>Boost de vitesse. multiplier &lt;= 0 utilise la valeur des stats.</summary>
        public void ApplyBoost(float duration, float multiplier = 0f)
        {
            _boostTimer = Mathf.Max(_boostTimer, duration);
            _boostMultiplier = multiplier > 0f ? multiplier : stats.boostSpeedMultiplier;
            _speed = Mathf.Max(_speed, stats.maxSpeed * SpeedScale * _boostMultiplier * 0.9f);
        }

        /// <summary>Touché par un bonus : tête-à-queue et perte de vitesse.</summary>
        public void ApplyHit(float duration)
        {
            if (_stunTimer > 0f) return;
            _stunDuration = Mathf.Max(0.1f, duration);
            _stunTimer = _stunDuration;
            _speed *= 0.3f;
            _boostTimer = 0f;
            _boostMultiplier = 1f;
            EndDrift(false);
            Hit?.Invoke();
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            sphere.position = position + Vector3.up * _sphereRadius;
            sphere.linearVelocity = Vector3.zero;
            sphere.transform.position = sphere.position;
            transform.SetPositionAndRotation(position, rotation);
            _speed = 0f;
            _yawVelocity = 0f;
            EndDrift(false);
        }

        // ---------- Interne ----------

        private void UpdateTimers(float dt)
        {
            if (_stunTimer > 0f) _stunTimer -= dt;
            if (_boostTimer > 0f)
            {
                _boostTimer -= dt;
                if (_boostTimer <= 0f) _boostMultiplier = 1f;
            }
        }

        private void UpdateSpeed(float dt)
        {
            float top = stats.maxSpeed * SpeedScale * _boostMultiplier;
            float target;
            if (_stunTimer > 0f) target = 0f;
            else if (_input.Brake) target = _speed > 1f ? 0f : -stats.reverseSpeed;
            else target = _input.Throttle * top;

            float rate = target > _speed
                ? (_boostTimer > 0f ? stats.boostAcceleration : stats.acceleration)
                : stats.deceleration;
            if (!_grounded) rate *= 0.2f;

            _speed = Mathf.MoveTowards(_speed, target, rate * dt);
        }

        private void UpdateSteering(float dt)
        {
            float steer = _stunTimer > 0f ? 0f : Mathf.Clamp(_input.Steer, -1f, 1f);
            bool driftPressed = _input.Drift && !_prevDrift;

            // Figure en l'air : appui sur Drift pendant un saut.
            if (!_grounded && driftPressed && !_trickQueued) _trickQueued = true;

            if (_input.Drift && !_drifting && _grounded && Mathf.Abs(steer) > 0.3f && _speed > stats.minDriftSpeed)
            {
                _drifting = true;
                _driftDir = steer > 0f ? 1 : -1;
                _driftTime = 0f;
                _driftTier = 0;
            }

            if (_drifting)
            {
                bool released = !_input.Drift;
                if (released || _speed < stats.minDriftSpeed * 0.7f || _stunTimer > 0f)
                    EndDrift(released && _stunTimer <= 0f);
            }

            float yawRate;
            if (_drifting)
            {
                _driftTime += dt;
                Vector3 t = stats.driftTierTimes;
                _driftTier = _driftTime >= t.z ? 3 : _driftTime >= t.y ? 2 : _driftTime >= t.x ? 1 : 0;

                // Braquer vers l'intérieur resserre le drift, contre-braquer l'élargit.
                float control = Mathf.Lerp(stats.driftWide, stats.driftTight, (steer * _driftDir + 1f) * 0.5f);
                yawRate = _driftDir * control * stats.driftSteering;
            }
            else
            {
                float speedFactor = Mathf.Clamp01(Mathf.Abs(_speed) / (stats.maxSpeed * 0.25f));
                yawRate = steer * stats.steering * speedFactor * (_speed < 0f ? -1f : 1f);
            }

            if (!_grounded) yawRate *= stats.airSteerFactor;

            _yawVelocity = Mathf.Lerp(_yawVelocity, yawRate, 1f - Mathf.Exp(-stats.steerResponse * dt));
            transform.Rotate(0f, _yawVelocity * dt, 0f, Space.Self);
        }

        private void EndDrift(bool grantBoost)
        {
            if (!_drifting) return;
            int tier = _driftTier;
            _drifting = false;
            _driftTier = 0;
            _driftTime = 0f;

            if (grantBoost && tier > 0)
            {
                Vector3 d = stats.driftBoostDurations;
                ApplyBoost(tier == 3 ? d.z : tier == 2 ? d.y : d.x);
            }
            DriftReleased?.Invoke(grantBoost ? tier : 0);
        }

        private void FollowSphere(float dt)
        {
            transform.position = sphere.transform.position - _groundNormal * _sphereRadius;

            Vector3 up = _grounded ? _groundNormal : Vector3.up;
            Quaternion target = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-stats.alignSpeed * dt));
        }

        private void OnLand()
        {
            if (_trickQueued) ApplyBoost(stats.trickBoostDuration);
            _trickQueued = false;
            _trickAngle = 0f;
            Landed?.Invoke();
        }

        private void UpdateVisual(float dt)
        {
            if (visual == null) return;

            float targetYaw = _drifting ? _driftDir * stats.driftVisualAngle : 0f;
            _visualYaw = Mathf.Lerp(_visualYaw, targetYaw, 1f - Mathf.Exp(-10f * dt));

            float spin = _stunTimer > 0f ? (1f - _stunTimer / _stunDuration) * 720f : 0f;
            if (_trickQueued && !_grounded) _trickAngle = Mathf.MoveTowards(_trickAngle, 360f, 900f * dt);

            float lean = -_input.Steer * stats.leanAngle * Speed01;
            visual.localRotation = Quaternion.Euler(0f, _visualYaw + spin, lean + _trickAngle);
        }
    }
}
