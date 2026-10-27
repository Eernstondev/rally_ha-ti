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
        public bool IsShielded => _shieldTimer > 0f;
        public bool IsSlowed => _slowTimer > 0f;
        public bool IsConfused => _confuseTimer > 0f;
        public KartInput LastInput => _input;

        public event Action<int> DriftReleased;
        public event Action Landed;
        public event Action Hit;
        public event Action ShieldBlocked;

        private float _shieldTimer;
        private bool _shieldBreaks;
        private float _slowTimer, _slowFactor = 1f;
        private float _confuseTimer;
        private float _accelScale = 1f, _steerScale = 1f;

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

        // Suspension simulée : tangage à l'accélération, écrasement à l'atterrissage, vibrations du sol.
        private float _pitch, _pitchVel, _bounce, _bounceVel, _prevSpeed;

        /// <summary>Intensité des vibrations du sol (sable, terre battue : plus fort).</summary>
        public float RoadRoughness { get => roadRoughness; set => roadRoughness = value; }

        [Tooltip("Vibrations du sol : 0,02 asphalte, 0,05 sable ou terre.")]
        [SerializeField] private float roadRoughness = 0.02f;

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

        /// <summary>Protégé : les coups suivants sont ignorés. breakOnHit = le bouclier saute au premier coup (Parasòl).</summary>
        public void ApplyShield(float duration, bool breakOnHit)
        {
            _shieldTimer = Mathf.Max(_shieldTimer, duration);
            _shieldBreaks = breakOnHit;
        }

        /// <summary>Vitesse max réduite pendant un temps (orage Loraj).</summary>
        public void ApplySlow(float duration, float factor)
        {
            if (IsShielded) { ConsumeShield(); return; }
            _slowTimer = Mathf.Max(_slowTimer, duration);
            _slowFactor = Mathf.Clamp(factor, 0.1f, 1f);
        }

        /// <summary>Direction perturbée (pikliz dans les yeux).</summary>
        public void ApplyConfuse(float duration)
        {
            if (IsShielded) { ConsumeShield(); return; }
            _confuseTimer = Mathf.Max(_confuseTimer, duration);
        }

        /// <summary>Poussée latérale (onde de lanbi, klaxon géant).</summary>
        public void Push(Vector3 velocityChange)
        {
            if (IsShielded) return;
            sphere.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>Annule un tête-à-queue en cours (réparation, soin).</summary>
        public void ClearStun()
        {
            _stunTimer = 0f;
            _slowTimer = 0f;
            _confuseTimer = 0f;
        }

        /// <summary>Améliorations du garage : niveaux 0 à 4 en vitesse, accélération et maniabilité.</summary>
        public void SetUpgrades(int speed, int acceleration, int handling)
        {
            SpeedScale = 1f + 0.025f * speed;
            _accelScale = 1f + 0.06f * acceleration;
            _steerScale = 1f + 0.05f * handling;
        }

        private void ConsumeShield()
        {
            if (_shieldBreaks) _shieldTimer = 0f;
        }

        /// <summary>Touché par un bonus : le kart saute, tourne sur lui-même et perd sa vitesse.</summary>
        public void ApplyHit(float duration)
        {
            if (IsShielded)
            {
                ConsumeShield();
                ShieldBlocked?.Invoke();
                return;
            }
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
            if (_shieldTimer > 0f) _shieldTimer -= dt;
            if (_slowTimer > 0f) _slowTimer -= dt;
            if (_confuseTimer > 0f) _confuseTimer -= dt;
            if (_boostTimer > 0f)
            {
                _boostTimer -= dt;
                if (_boostTimer <= 0f) _boostMultiplier = 1f;
            }
        }

        private void UpdateSpeed(float dt)
        {
            float top = stats.maxSpeed * SpeedScale * _boostMultiplier * (_slowTimer > 0f ? _slowFactor : 1f);
            float target;
            if (_stunTimer > 0f) target = 0f;
            else if (_input.Brake) target = _speed > 1f ? 0f : -stats.reverseSpeed;
            else target = _input.Throttle * top;

            float rate = target > _speed
                ? (_boostTimer > 0f ? stats.boostAcceleration : stats.acceleration * _accelScale)
                : stats.deceleration;
            if (!_grounded) rate *= 0.2f;

            _speed = Mathf.MoveTowards(_speed, target, rate * dt);
        }

        private void UpdateSteering(float dt)
        {
            float steer = _stunTimer > 0f ? 0f : Mathf.Clamp(_input.Steer, -1f, 1f);
            if (_confuseTimer > 0f) steer = Mathf.Clamp(steer * 0.4f + Mathf.Sin(Time.time * 7f) * 0.8f, -1f, 1f);
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
                yawRate = steer * stats.steering * _steerScale * speedFactor * (_speed < 0f ? -1f : 1f);
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
            _bounceVel -= 3.5f;
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

            // Tangage : le nez se lève quand on accélère, plonge quand on freine. Ressort amorti.
            float accel = (_speed - _prevSpeed) / Mathf.Max(dt, 0.0001f);
            _prevSpeed = _speed;
            float targetPitch = _grounded ? Mathf.Clamp(-accel * 0.35f, -5f, 5f) : 0f;
            _pitchVel += ((targetPitch - _pitch) * 90f - _pitchVel * 12f) * dt;
            _pitch += _pitchVel * dt;
            _bounceVel += (-_bounce * 140f - _bounceVel * 11f) * dt;
            _bounce += _bounceVel * dt;
            float shake = _grounded ? (Mathf.PerlinNoise(Time.time * 18f, GetInstanceID() * 0.1f) - 0.5f) * RoadRoughness * Speed01 * 4f : 0f;

            visual.localRotation = Quaternion.Euler(_pitch, _visualYaw + spin, lean + _trickAngle);

            // Touché : le kart décolle et retombe en tournant, comme dans les karts arcade.
            float hop = 0f;
            if (_stunTimer > 0f)
            {
                float k = Mathf.Clamp01((1f - _stunTimer / _stunDuration) / 0.7f);
                hop = Mathf.Sin(k * Mathf.PI) * 1.3f;
            }
            visual.localPosition = new Vector3(0f, hop + Mathf.Max(-0.25f, _bounce * 0.12f) + shake, 0f);
        }
    }
}
