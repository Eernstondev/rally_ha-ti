using System;
using UnityEngine;

namespace RallyAyiti.Kart
{
    /// <summary>Fait tourner les roues selon la vitesse et braque les roues avant.</summary>
    [RequireComponent(typeof(KartController))]
    public sealed class KartWheels : MonoBehaviour
    {
        [SerializeField] private Transform[] wheels = Array.Empty<Transform>();
        [SerializeField] private Transform[] steering = Array.Empty<Transform>();
        [SerializeField] private float maxSteerAngle = 24f;

        private KartController _kart;
        private Quaternion[] _base;
        private float[] _radius, _sign, _angle;
        private bool[] _steers;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            int n = wheels.Length;
            _base = new Quaternion[n];
            _radius = new float[n];
            _sign = new float[n];
            _angle = new float[n];
            _steers = new bool[n];

            for (int i = 0; i < n; i++)
            {
                Transform w = wheels[i];
                if (w == null) continue;
                _base[i] = w.localRotation;
                var r = w.GetComponentInChildren<Renderer>();
                _radius[i] = r != null ? Mathf.Max(0.05f, r.bounds.extents.y) : 0.35f;
                Vector3 axle = w.TransformDirection(Vector3.right);
                _sign[i] = Vector3.Dot(axle, transform.right) >= 0f ? 1f : -1f;
                _steers[i] = Array.IndexOf(steering, w) >= 0;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float steer = Mathf.Clamp(_kart.LastInput.Steer, -1f, 1f) * maxSteerAngle;

            for (int i = 0; i < wheels.Length; i++)
            {
                Transform w = wheels[i];
                if (w == null) continue;

                _angle[i] = Mathf.Repeat(_angle[i] + _sign[i] * _kart.CurrentSpeed * dt / (2f * Mathf.PI * _radius[i]) * 360f, 360f);
                Quaternion spin = Quaternion.AngleAxis(_angle[i], Vector3.right);
                Quaternion yaw = Quaternion.identity;
                if (_steers[i] && w.parent != null)
                    yaw = Quaternion.AngleAxis(steer, w.parent.InverseTransformDirection(transform.up));

                w.localRotation = yaw * _base[i] * spin;
            }
        }
    }
}
