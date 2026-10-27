using UnityEngine;

namespace RallyAyiti.Environment
{
    /// <summary>Bateau au mouillage : monte, descend et roule doucement avec la houle.</summary>
    public sealed class Bobbing : MonoBehaviour
    {
        [SerializeField] private float height = 0.18f;
        [SerializeField] private float roll = 4f;
        [SerializeField] private float speed = 1.1f;

        private Vector3 _pos;
        private Quaternion _rot;
        private float _phase;

        private void Awake()
        {
            _pos = transform.localPosition;
            _rot = transform.localRotation;
            _phase = Random.value * 10f;
        }

        private void Update()
        {
            float t = Time.time * speed + _phase;
            transform.localPosition = _pos + Vector3.up * (Mathf.Sin(t) * height);
            transform.localRotation = _rot * Quaternion.Euler(Mathf.Sin(t * 0.7f) * roll * 0.5f, 0f, Mathf.Sin(t * 0.9f + 1f) * roll);
        }
    }
}
