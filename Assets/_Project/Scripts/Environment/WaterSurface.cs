using UnityEngine;

namespace RallyAyiti.Environment
{
    /// <summary>
    /// Houle qui roule vers le rivage (côté X minimum du mesh) et reflets qui défilent.
    /// Les vagues s'aplatissent près du bord pour que le rivage reste en place.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaterSurface : MonoBehaviour
    {
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

        [SerializeField] private float amplitude = 0.35f;
        [SerializeField] private float wavelength = 16f;
        [SerializeField] private float speed = 0.35f;
        [SerializeField] private float fadeDistance = 30f;
        [SerializeField] private Vector2 normalScroll = new(0.006f, 0.012f);

        private Mesh _mesh;
        private Vector3[] _base;
        private Vector3[] _verts;
        private float _minX;
        private Material _material;

        private void Awake()
        {
            _mesh = GetComponent<MeshFilter>().mesh;
            _mesh.MarkDynamic();
            _base = _mesh.vertices;
            _verts = (Vector3[])_base.Clone();
            _minX = float.MaxValue;
            foreach (Vector3 v in _base) _minX = Mathf.Min(_minX, v.x);

            Bounds b = _mesh.bounds;
            b.Expand(new Vector3(0f, amplitude * 4f, 0f));
            _mesh.bounds = b;
            _material = GetComponent<MeshRenderer>().material;
        }

        private void Update()
        {
            float t = Time.time;
            const float tau = Mathf.PI * 2f;
            for (int i = 0; i < _base.Length; i++)
            {
                Vector3 b = _base[i];
                float fade = Mathf.Clamp01((b.x - _minX) / fadeDistance);
                float swell = Mathf.Sin((b.x / wavelength + t * speed) * tau);
                float cross = 0.35f * Mathf.Sin(b.z * 0.11f + t * 1.3f) + 0.25f * Mathf.Sin((b.x + b.z) * 0.07f - t * 0.9f);
                _verts[i].y = b.y + amplitude * fade * (swell + cross);
            }
            _mesh.vertices = _verts;
            _mesh.RecalculateNormals();
            _material.SetTextureOffset(BumpMap, normalScroll * t);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
