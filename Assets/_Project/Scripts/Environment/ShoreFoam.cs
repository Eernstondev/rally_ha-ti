using UnityEngine;

namespace RallyAyiti.Environment
{
    /// <summary>Écume qui monte sur le sable puis se retire, en s'estompant.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class ShoreFoam : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Tooltip("Déplacement maximal vers la plage (m), dans l'espace du parent.")]
        [SerializeField] private Vector3 travel = new(-3f, 0.2f, 0f);
        [SerializeField] private float period = 7f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private Vector3 _base;
        private Material _material;
        private Color _color = Color.white;

        private void Awake()
        {
            _base = transform.localPosition;
            _material = GetComponent<Renderer>().material;
            if (_material.HasProperty(BaseColor)) _color = _material.GetColor(BaseColor);
        }

        private void Update()
        {
            float cycle = Mathf.Repeat(Time.time / period + phase, 1f);
            // Montée rapide, retrait lent, disparition en fin de cycle.
            float s = cycle < 0.35f
                ? Mathf.SmoothStep(0f, 1f, cycle / 0.35f)
                : 1f - Mathf.SmoothStep(0f, 1f, (cycle - 0.35f) / 0.65f);
            transform.localPosition = _base + travel * s;
            float fadeOut = cycle < 0.85f ? 1f : (1f - cycle) / 0.15f;
            _color.a = Mathf.Lerp(0.35f, 0.9f, s) * fadeOut;
            _material.SetColor(BaseColor, _color);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
