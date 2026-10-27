using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Réalisme façon kart arcade : terrain en relief (collines, piste qui monte et descend), bordures naturelles
    /// (clôtures, rochers, piles de pneus) au lieu de murs gris, poussière et traces de pneus, caméra qui vibre.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const float TerrainCell = 4f;

        /// <summary>Circuit en cours de construction (null pour le menu : sol plat).</summary>
        private static Theme _th;
        private static float[] _hf;
        /// <summary>Tracé du circuit en mode rue : sert à poser les objets sur le trottoir surélevé.</summary>
        private static Frame[] _streetFrames;
        private static int _hfNx, _hfNz;
        private static float _hfX0, _hfZ0;

        // ---------- Relief ----------

        /// <summary>Collines douces du paysage, aplaties vers la mer.</summary>
        private static float RawHeight(float x, float z)
        {
            if (_th == null || _th.Relief <= 0f) return 0f;
            float s = _th.Seed * 0.013f;
            float n = Mathf.PerlinNoise(x * 0.0055f + s, z * 0.0055f + 7.3f) * 0.72f
                      + Mathf.PerlinNoise(x * 0.018f + 3.1f, z * 0.018f + s) * 0.28f;
            float h = (n - 0.45f) * 2f * _th.Relief;
            if (_th.Coast) h *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_th.CoastX - 25f, _th.CoastX - 110f, x));
            return h;
        }

        /// <summary>Hauteur du sol construit (suit la piste près d'elle), ou du relief brut ailleurs.</summary>
        private static float GroundY(float x, float z)
        {
            if (_hf == null) return RawHeight(x, z);
            float fx = (x - _hfX0) / TerrainCell, fz = (z - _hfZ0) / TerrainCell;
            if (fx < 0f || fz < 0f || fx >= _hfNx - 1 || fz >= _hfNz - 1) return RawHeight(x, z);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            float a = Mathf.Lerp(_hf[iz * _hfNx + ix], _hf[iz * _hfNx + ix + 1], tx);
            float b = Mathf.Lerp(_hf[(iz + 1) * _hfNx + ix], _hf[(iz + 1) * _hfNx + ix + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        private static Vector3 OnGround(Vector3 p)
        {
            p.y = GroundY(p.x, p.z);
            if (_streetFrames != null)
            {
                // Sur le trottoir : le sol est 15 cm sous la piste, le trottoir 16 cm au-dessus.
                float d = MinDistanceToTrack(_streetFrames, p);
                if (d > HalfWidth - 0.2f && d < HalfWidth + SidewalkWidth + 0.3f) p.y += 0.15f + SidewalkHeight;
            }
            return p;
        }

        /// <summary>
        /// Sol en relief : grille de 4 m, collider compris. Près de la piste, le sol rejoint la hauteur de la piste
        /// (un peu en dessous pour ne jamais la recouvrir), puis remonte doucement vers les collines.
        /// </summary>
        private static GameObject BuildTerrain(Frame[] f, Rect area, Material mat)
        {
            _hfX0 = area.xMin;
            _hfZ0 = area.yMin;
            _hfNx = Mathf.CeilToInt(area.width / TerrainCell) + 1;
            _hfNz = Mathf.CeilToInt(area.height / TerrainCell) + 1;
            var heights = new float[_hfNx * _hfNz];
            var verts = new Vector3[heights.Length];
            var uvs = new Vector2[heights.Length];

            for (int iz = 0; iz < _hfNz; iz++)
            for (int ix = 0; ix < _hfNx; ix++)
            {
                float x = Mathf.Min(area.xMax, _hfX0 + ix * TerrainCell), z = Mathf.Min(area.yMax, _hfZ0 + iz * TerrainCell);
                float best = float.MaxValue;
                int nearest = 0;
                for (int i = 0; i < f.Length; i++)
                {
                    float dx = f[i].Pos.x - x, dz = f[i].Pos.z - z, sq = dx * dx + dz * dz;
                    if (sq >= best) continue;
                    best = sq;
                    nearest = i;
                }
                // Hauteur exacte de la piste : projection sur les deux segments voisins du point le plus proche.
                float trackY = f[nearest].Pos.y, d = Mathf.Sqrt(best);
                foreach (int j in new[] { (nearest - 1 + f.Length) % f.Length, nearest })
                {
                    Vector3 a = f[j].Pos, b = f[(j + 1) % f.Length].Pos;
                    var ab = new Vector2(b.x - a.x, b.z - a.z);
                    float s = Mathf.Clamp01(Vector2.Dot(new Vector2(x - a.x, z - a.z), ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                    var q = new Vector2(a.x + ab.x * s - x, a.z + ab.y * s - z);
                    if (q.magnitude > d + 0.001f) continue;
                    d = q.magnitude;
                    trackY = Mathf.Lerp(a.y, b.y, s);
                }
                // En ville, le sol reste plat sous les trottoirs et les maisons avant de rejoindre les mornes.
                bool street = _th != null && _th.Street;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HalfWidth + (street ? 16f : 1.5f), HalfWidth + (street ? 40f : 26f), d));
                float h = Mathf.Lerp(trackY - 0.15f, RawHeight(x, z), w);
                int k = iz * _hfNx + ix;
                heights[k] = h;
                verts[k] = new Vector3(x, h, z);
                uvs[k] = new Vector2((x - area.xMin) / area.width, (z - area.yMin) / area.height);
            }

            var tris = new int[(_hfNx - 1) * (_hfNz - 1) * 6];
            int t = 0;
            for (int iz = 0; iz < _hfNz - 1; iz++)
            for (int ix = 0; ix < _hfNx - 1; ix++)
            {
                int a = iz * _hfNx + ix;
                tris[t++] = a; tris[t++] = a + _hfNx; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = a + _hfNx; tris[t++] = a + _hfNx + 1;
            }
            GameObject go = CreateMeshObject("Sol", verts, uvs, tris, mat, true);
            go.isStatic = true;
            _hf = heights;
            return go;
        }

        // ---------- Bordures naturelles ----------

        /// <summary>
        /// Le long des deux bords, par tronçons : clôtures en bois, rochers, piles de pneus, murets
        /// (en ville), avec des ouvertures. Les murs invisibles retiennent les karts.
        /// </summary>
        private static void CreateBorders(Theme th, Frame[] f)
        {
            if (th.Street) return; // En ville, les trottoirs et les façades bordent la piste.
            var root = new GameObject("Bodi").transform;
            string[] kinds = th.SandTrack
                ? new[] { "Fence", "Fence", "Rock", "Rock", "TireStack", "None" }
                : new[] { "TireStack", "TireStack", "BlockWall", "Fence", "Rock", "None" };

            foreach (float side in new[] { -1f, 1f })
            {
                float travelled = 0f, next = 0f, runEnd = 0f;
                string kind = "None";
                for (int i = 1; i < f.Length; i++)
                {
                    travelled += Vector3.Distance(f[i].Pos, f[i - 1].Pos);
                    if (travelled >= runEnd)
                    {
                        kind = kinds[Random.Range(0, kinds.Length)];
                        runEnd = travelled + Random.Range(25f, 70f);
                    }
                    if (travelled < next) continue;

                    Frame fr = f[i];
                    float spacing;
                    switch (kind)
                    {
                        case "Fence":
                        {
                            spacing = 4f;
                            Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 1.1f);
                            if (Free(p, 1.9f)) Place("Fence", root, p, Quaternion.LookRotation(-fr.Right * side), 1.9f);
                            break;
                        }
                        case "Rock":
                        {
                            spacing = Random.Range(2f, 3.4f);
                            Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + Random.Range(1.2f, 2.2f));
                            if (!Free(p, 0.9f)) break;
                            Transform rock = Place("Rock", root, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), 0.9f);
                            if (rock != null) rock.localScale = Vector3.one * Random.Range(0.55f, 1f);
                            break;
                        }
                        case "TireStack":
                        {
                            spacing = 1.5f;
                            Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 1.1f);
                            if (Free(p, 0.65f)) Place("TireStack", root, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), 0.65f);
                            break;
                        }
                        case "BlockWall":
                        {
                            spacing = 4.2f;
                            Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 1.3f);
                            if (th.LandOk(p) && Free(p, 2.1f))
                                Place("BlockWall", root, p, Quaternion.LookRotation(-fr.Right * side), 2.1f);
                            break;
                        }
                        default:
                            spacing = 6f;
                            break;
                    }
                    next = travelled + spacing;
                }
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        // ---------- Effets du kart ----------

        /// <summary>Nuage de poussière (ou de sable) derrière les roues ; KartEffects en règle le débit selon la vitesse.</summary>
        private static ParticleSystem DustTrail(Transform parent, Vector3 localPos, Color color)
        {
            var go = new GameObject("Pousye");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-10f, 180f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.gravityModifier = -0.05f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.5f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AlphaParticleMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Gros nuages blancs de dérapage : bouffées larges, lentes, qui gonflent et restent un peu derrière le kart.</summary>
        private static ParticleSystem DriftSmoke(Transform parent, Vector3 localPos)
        {
            ParticleSystem ps = DustTrail(parent, localPos, new Color(1f, 1f, 1f, 0.7f));
            ps.gameObject.name = "LafimeDerapaj";
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            main.gravityModifier = -0.12f;
            main.maxParticles = 220;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.angle = 40f;
            shape.radius = 0.8f;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 3.2f));
            return ps;
        }

        /// <summary>Trace de pneu posée à plat sur le sol pendant les drifts.</summary>
        private static TrailRenderer SkidTrail(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("TrasPne");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var trail = go.AddComponent<TrailRenderer>();
            trail.alignment = LineAlignment.TransformZ;
            trail.time = 4f;
            trail.minVertexDistance = 0.3f;
            trail.widthMultiplier = 0.24f;
            trail.emitting = false;
            trail.sharedMaterial = ArtLibrary.Get("Skid");
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.textureMode = LineTextureMode.Tile;
            return trail;
        }

        private static Material AlphaParticleMaterial()
        {
            string path = $"{Root}/Materials/Pousye.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.mainTexture = tex;
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Profil de bruit pour les vibrations de la caméra (léger roulis, petites secousses).</summary>
        private static NoiseSettings ShakeProfile()
        {
            string path = $"{Root}/Settings/KameraTranble.asset";
            var ns = AssetDatabase.LoadAssetAtPath<NoiseSettings>(path);
            if (ns != null) return ns;
            ns = ScriptableObject.CreateInstance<NoiseSettings>();
            ns.PositionNoise = new[]
            {
                new NoiseSettings.TransformNoiseParams
                {
                    X = new NoiseSettings.NoiseParams { Frequency = 2.3f, Amplitude = 0.03f },
                    Y = new NoiseSettings.NoiseParams { Frequency = 3.1f, Amplitude = 0.05f },
                    Z = new NoiseSettings.NoiseParams { Frequency = 1.7f, Amplitude = 0.02f },
                },
            };
            ns.OrientationNoise = new[]
            {
                new NoiseSettings.TransformNoiseParams
                {
                    X = new NoiseSettings.NoiseParams { Frequency = 3.3f, Amplitude = 0.3f },
                    Y = new NoiseSettings.NoiseParams { Frequency = 2.4f, Amplitude = 0.25f },
                    Z = new NoiseSettings.NoiseParams { Frequency = 1.9f, Amplitude = 0.35f },
                },
            };
            EnsureFolder(Root + "/Settings");
            AssetDatabase.CreateAsset(ns, path);
            return ns;
        }
    }
}
