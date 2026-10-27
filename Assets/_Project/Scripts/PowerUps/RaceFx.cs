using System.Collections;
using RallyAyiti.Kart;
using RallyAyiti.Race;
using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>
    /// Effets partagés des bonus et des pouvoirs : ondes de choc, éclairs, bulles de protection, éclaboussures.
    /// Une instance par scène de course, créée par le builder avec ses matériaux.
    /// </summary>
    public sealed class RaceFx : MonoBehaviour
    {
        [SerializeField] private Material waveMaterial;
        [SerializeField] private Material shieldMaterial;
        [SerializeField] private Material boltMaterial;

        public static RaceFx Instance { get; private set; }

        private Mesh _disc, _sphere;

        private void Awake()
        {
            Instance = this;
            _disc = PrimitiveMesh(PrimitiveType.Cylinder);
            _sphere = PrimitiveMesh(PrimitiveType.Sphere);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return mesh;
        }

        /// <summary>
        /// Onde autour d'un point : les karts touchés tournent et sont repoussés (push en m/s).
        /// Renvoie le nombre de karts atteints.
        /// </summary>
        public static int Blast(KartController owner, Vector3 origin, float radius, float hitDuration, float push, Color color)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.Wave(origin, radius, color));
            RaceManager race = RaceManager.Current;
            if (race == null) return 0;
            int count = 0;
            foreach (LapTracker r in race.Standings)
            {
                KartController k = r.Kart;
                if (k == owner) continue;
                Vector3 d = k.transform.position - origin;
                d.y = 0f;
                if (d.sqrMagnitude > radius * radius) continue;
                if (hitDuration > 0f) k.ApplyHit(hitDuration);
                if (push > 0f) k.Push(d.normalized * push + Vector3.up * push * 0.3f);
                count++;
            }
            return count;
        }

        /// <summary>Éclair qui tombe sur un kart.</summary>
        public static void Bolt(Vector3 target)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.BoltRoutine(target));
        }

        /// <summary>Bulle colorée autour d'un kart tant qu'il est protégé.</summary>
        public static void Bubble(KartController kart, Color color)
        {
            if (Instance == null || kart == null) return;
            Instance.StartCoroutine(Instance.BubbleRoutine(kart, color));
        }

        /// <summary>Petite gerbe colorée (pikliz, étincelles de réparation).</summary>
        public static void Splash(Vector3 position, Color color)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.Wave(position + Vector3.up * 0.8f, 3f, color, 0.3f));
        }

        private GameObject Shape(string name, Mesh mesh, Material mat, Color color)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.material = mat;
            r.material.color = color;
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", color);
            return go;
        }

        private IEnumerator Wave(Vector3 origin, float radius, Color color, float duration = 0.45f)
        {
            if (waveMaterial == null) yield break;
            GameObject ring = Shape("Onn", _disc, waveMaterial, color);
            ring.transform.position = origin + Vector3.up * 0.4f;
            Material m = ring.GetComponent<MeshRenderer>().material;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float size = Mathf.Lerp(1f, radius * 2f, 1f - (1f - k) * (1f - k));
                ring.transform.localScale = new Vector3(size, 0.04f + 0.6f * (1f - k), size);
                Color c = color;
                c.a = 0.6f * (1f - k);
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                yield return null;
            }
            Destroy(m);
            Destroy(ring);
        }

        private IEnumerator BoltRoutine(Vector3 target)
        {
            if (boltMaterial == null) yield break;
            var go = new GameObject("Loraj");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = boltMaterial;
            lr.widthMultiplier = 0.35f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            const int points = 9;
            lr.positionCount = points;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                for (int i = 0; i < points; i++)
                {
                    float k = i / (points - 1f);
                    Vector3 p = Vector3.Lerp(target + Vector3.up * 30f, target + Vector3.up * 0.5f, k);
                    if (i > 0 && i < points - 1) p += new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                    lr.SetPosition(i, p);
                }
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator BubbleRoutine(KartController kart, Color color)
        {
            if (shieldMaterial == null) yield break;
            color.a = 0.35f;
            GameObject bubble = Shape("Boukliye", _sphere, shieldMaterial, color);
            bubble.transform.SetParent(kart.transform, false);
            bubble.transform.localPosition = new Vector3(0f, 1f, 0f);
            Material m = bubble.GetComponent<MeshRenderer>().material;
            yield return null;
            while (kart != null && kart.IsShielded)
            {
                float pulse = 3.6f + Mathf.Sin(Time.time * 8f) * 0.15f;
                bubble.transform.localScale = new Vector3(pulse, pulse * 0.75f, pulse * 1.15f);
                yield return null;
            }
            Destroy(m);
            Destroy(bubble);
        }
    }
}
