using System.Collections.Generic;
using System.IO;
using System.Linq;
using RallyAyiti.Audio;
using RallyAyiti.Core;
using RallyAyiti.Environment;
using RallyAyiti.Kart;
using RallyAyiti.PowerUps;
using RallyAyiti.Race;
using RallyAyiti.UI;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Construit les circuits jouables : Katye (quartier, test) et Labadie (plage, mer, vagues).
    /// Chaque scène contient karts modélisés, IA, bonus, caméra, post-traitement, HUD et commandes tactiles.
    /// Se lance seul une fois après une mise à jour, ou via le menu « Rally Ayiti ».
    /// </summary>
    [InitializeOnLoad]
    public static partial class PrototypeBuilder
    {
        private const string Root = "Assets/_Project";
        private const string AutoBuildKey = "RallyAyiti.AutoBuilt.v5";
        private const float HalfWidth = 7f;
        private const int Samples = 480;
        private const float WaterY = -0.35f;
        private const float BeachDrop = 3.4f;
        private const float BeachWidth = 45f;

        private sealed class Theme
        {
            public string Name, ScenePath;
            public int Seed;
            public Vector2[] Knots;
            public float Scale = 1f;
            public bool Coast;
            public float CoastX;
            public int Houses, Palms, Stalls, WallRuns, Tires, BeachProps, FloatingBoats, Hills;
            public float RampT;
            public (float t, float lateral)[] Boosts;
            public float[] ItemRows;
            public bool Arch;
            // Entourage : végétation et vie autour du circuit.
            public float Dry;
            public int Trees, Bushes, Flowers, Bananas, Grass, Rocks, Fences, Villages, Parked, Billboards;
            public string Ambience;
            // Réalisme : relief (m), piste en sable tassé plutôt qu'en asphalte.
            public float Relief;
            public bool SandTrack;

            public bool LandOk(Vector3 p) => !Coast || p.x < CoastX - 55f;
        }

        private static readonly Theme Katye = new()
        {
            Name = "Katye", ScenePath = Root + "/Scenes/Prototype.unity", Seed = 2026, Scale = 1.4f,
            Knots = new Vector2[]
            {
                new(0, 0), new(60, 0), new(110, 10), new(140, 45), new(130, 90), new(95, 105), new(70, 85),
                new(45, 95), new(20, 130), new(-30, 135), new(-70, 110), new(-80, 60), new(-60, 20), new(-30, 5),
            },
            Houses = 38, Palms = 55, Stalls = 10, WallRuns = 14, Tires = 26, Hills = 12,
            RampT = 0.55f, Boosts = new[] { (0.2f, 0f), (0.8f, -3f) }, ItemRows = new[] { 0.1f, 0.42f, 0.7f },
            Dry = 0.55f, Trees = 90, Bushes = 150, Flowers = 60, Bananas = 70, Grass = 750, Rocks = 60, Fences = 30,
            Villages = 9, Parked = 16, Billboards = 5, Ambience = "Ambience_City",
            Relief = 6f, SandTrack = false,
        };

        private static readonly Theme Labadie = new()
        {
            Name = "Labadie", ScenePath = Root + "/Scenes/Labadie.unity", Seed = 1804, Scale = 1f,
            Knots = new Vector2[]
            {
                new(0, 0), new(70, -10), new(130, 0), new(155, 40), new(158, 110), new(150, 180), new(115, 215),
                new(60, 205), new(30, 170), new(40, 130), new(10, 100), new(-40, 110), new(-70, 80), new(-60, 30), new(-30, 5),
            },
            Coast = true, CoastX = 188f,
            Houses = 18, Palms = 70, Stalls = 6, WallRuns = 5, Tires = 18, BeachProps = 34, FloatingBoats = 5, Hills = 14,
            RampT = 0.33f, Boosts = new[] { (0.27f, 0f), (0.88f, 2.5f) }, ItemRows = new[] { 0.08f, 0.5f, 0.75f },
            Arch = true,
            Dry = 0.2f, Trees = 130, Bushes = 170, Flowers = 45, Bananas = 110, Grass = 900, Rocks = 70, Fences = 22,
            Villages = 6, Parked = 10, Billboards = 4, Ambience = "Ambience_Ocean",
            Relief = 7f, SandTrack = true,
        };

        private static readonly string[] WallTextures =
            { "Wall_Pink.png", "Wall_Turquoise.png", "Wall_Yellow.png", "Wall_Blue.png", "Wall_White.png" };

        private static readonly Color Sun = Hex(0xf6c22b), Hibiscus = Hex(0xe2367a), Lagoon = Hex(0x1fb5a8),
            Mango = Hex(0xf2762e), Violet = Hex(0x7a5cd6), Green = Hex(0x3f8f4f), Paper = Hex(0xf6ecd9),
            Night = Hex(0x121a33);

        private struct Frame { public Vector3 Pos, Fwd, Right; }

        private readonly struct Pilot
        {
            public readonly string Name, Model, Driver, SideArt;
            public readonly float Length;
            public readonly Color Helmet, Shirt;

            public Pilot(string name, string model, float length, string driver, Color helmet, Color shirt, string sideArt = null)
            {
                Name = name; Model = model; Length = length; Driver = driver; Helmet = helmet; Shirt = shirt;
                SideArt = sideArt;
            }
        }

        private sealed class Mats
        {
            public Material Road, Line, Sand, Wall, Kerb, Ramp, Boost, Water, Foam, Hill;
        }

        private static readonly List<(Vector3 pos, float radius)> Occupied = new();

        static PrototypeBuilder() => EditorApplication.delayCall += AutoBuild;

        private static void AutoBuild()
        {
            if (SessionState.GetBool(AutoBuildKey, false) || EditorPrefs.GetBool(AutoBuildKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoBuild;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(AutoBuildKey, true);
            EditorPrefs.SetBool(AutoBuildKey, true);
            Build(Katye);
            Build(Labadie);
            BuildMenuScene();
        }

        [MenuItem("Rally Ayiti/Construire Labadie")]
        private static void BuildLabadieMenu() => BuildFromMenu(Labadie);

        [MenuItem("Rally Ayiti/Construire Katye (circuit de test)")]
        private static void BuildKatyeMenu() => BuildFromMenu(Katye);

        private static void BuildFromMenu(Theme theme)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Rally Ayiti : arrête le mode Play avant de reconstruire un circuit.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(theme);
        }

        private static void Build(Theme th)
        {
            if (!ArtLibrary.HasModel("TapTap"))
            {
                Debug.LogError("Rally Ayiti : les modèles 3D sont introuvables dans Assets/_Project/Art/Models.");
                return;
            }

            RenderSetup.EnsureUrp();
            FixTextureImports();
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/Prefabs");
            Occupied.Clear();
            _th = th;
            _hf = null;

            int kartLayer = EnsureLayer("Kart");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UnityEngine.Random.InitState(th.Seed);

            ConfigureQuality();
            Mats m = CreateMaterials();
            CreateLighting();

            SplineContainer line = CreateRacingLine(th);
            Frame[] frames = SampleTrack(line);

            CreatePaintedGround(th, frames);
            if (th.Coast) CreateSea(th, frames, m);
            CreateRoad(th, frames, m);
            CreateWalls(frames, m);
            CreateBorders(th, frames);
            CreateStartLine(line, m);
            Transform checkpoints = CreateCheckpoints(line);
            CreateRamp(line, th.RampT, m);
            foreach ((float t, float lateral) in th.Boosts) CreateBoostPad(line, t, lateral, m);
            CreateItemBoxes(line, th.ItemRows);

            var race = new GameObject("RaceManager").AddComponent<RaceManager>();
            SetRef(race, "checkpointsRoot", checkpoints);
            SetInt(race, "totalLaps", 3);

            KokoyeProjectile kokoye = KokoyePrefab(kartLayer);
            MangoTrap mango = MangoPrefab();
            MangoTrap banana = BananaPrefab();
            HomingRocket rocket = RocketPrefab();
            KartController[] karts = CreateGrid(line, kartLayer, kokoye, mango, banana, rocket);
            CreateRaceFx();
            CreateCoins(line, th);
            KartController player = karts[PlayerPilot];

            if (th.Arch) CreateArch(line);
            CreateStartArea(th, frames, line);
            CreateNeighborhood(th, frames);
            if (th.Coast) CreateBeach(th, frames);
            CreateScenery(th, frames);
            CreateHills(th, frames, m);
            (CinemachineCamera chase, KartCameraFx camFx) = CreateCamera(player);
            CreatePostFx();
            CreateHud(race);
            CreateRaceAudio(th, race);

            var setup = new GameObject("RaceSetup").AddComponent<RaceSetup>();
            SetRefArray(setup, "karts", karts.Cast<Object>().ToArray());
            SetRef(setup, "chaseCamera", chase);
            SetRef(setup, "cameraFx", camFx);
            SetInt(setup, "defaultPilot", PlayerPilot);
            SetInt(setup, "playerGridSlot", PlayerPilot);
            SetStringArray(setup, "vehicles", Pilots.Select(p => p.Model).ToArray());

            EditorSceneManager.SaveScene(scene, th.ScenePath);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"Rally Ayiti : circuit « {th.Name} » construit ({th.ScenePath}). Appuie sur Play pour conduire.");
        }

        /// <summary>Réimporte les textures dont les réglages spéciaux (sprite, transparence, bords) n'ont pas été appliqués.</summary>
        private static void FixTextureImports()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtLibrary.ArtRoot + "/Textures" })
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                bool ok = ArtImport.IsSprite(file) ? importer.textureType == TextureImporterType.Sprite
                    : ArtImport.IsCutout(file) || file == "Foam" ? importer.alphaIsTransparency
                    : file == "Water_Gradient" ? importer.wrapModeV == TextureWrapMode.Clamp
                    : true;
                if (!ok) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void UpdateBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in new[] { MenuScenePath, Labadie.ScenePath, Katye.ScenePath })
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- Qualité, lumière, post-traitement ----------

        private static void ConfigureQuality()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            QualitySettings.shadowDistance = 150f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            QualitySettings.antiAliasing = 4;
        }

        private static void CreateLighting()
        {
            var sun = new GameObject("Soley").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.75f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = sun;

            Material sky = SkyMaterial();
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.33f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.76f, 0.86f, 0.94f);
            RenderSettings.fogStartDistance = 180f;
            RenderSettings.fogEndDistance = 620f;
        }

        private static void CreatePostFx()
        {
            var volume = new GameObject("PostTretman").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = RenderSetup.PostProfile();
        }

        private static Mats CreateMaterials()
        {
            var m = new Mats
            {
                Road = ArtLibrary.Get("Asphalt"),
                Sand = ArtLibrary.Get("Sand"),
                Wall = ArtLibrary.Get("Concrete_Blocks"),
                Kerb = ArtLibrary.Get("Kerb"),
                Water = ArtLibrary.Get("Water"),
                Foam = ArtLibrary.Get("Foam"),
                Hill = ArtLibrary.Get("Morne"),
                Line = Mat("LiyBlan", new Color(0.95f, 0.93f, 0.88f), 0.2f),
                Ramp = Mat("Rampe", Sun, 0.4f),
                Boost = Mat("Boost", Lagoon, 0.7f, 1.5f),
            };
            m.Sand.mainTextureScale = Vector2.one;
            return m;
        }

        // ---------- Circuit ----------

        private static SplineContainer CreateRacingLine(Theme th)
        {
            var container = new GameObject("RacingLine").AddComponent<SplineContainer>();
            Spline spline = container.Spline;
            spline.Clear();
            foreach (Vector2 k in th.Knots)
            {
                float x = k.x * th.Scale, z = k.y * th.Scale;
                spline.Add(new BezierKnot(new float3(x, RawHeight(x, z), z)), TangentMode.AutoSmooth);
            }
            spline.Closed = true;
            return container;
        }

        private static Frame FrameAt(SplineContainer line, float t)
        {
            line.Evaluate(Mathf.Repeat(t, 1f), out float3 p, out float3 tan, out _);
            // Avant : suit la pente de la piste (plaques, rampe, ligne d'arrivée inclinées avec elle). Côté : toujours horizontal.
            Vector3 fwd = ((Vector3)tan).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            return new Frame { Pos = new Vector3(p.x, p.y, p.z), Fwd = fwd, Right = right };
        }

        private static Frame[] SampleTrack(SplineContainer line)
        {
            var frames = new Frame[Samples];
            for (int i = 0; i < Samples; i++) frames[i] = FrameAt(line, (float)i / Samples);
            return frames;
        }

        private static Bounds TrackBounds(Frame[] f)
        {
            var b = new Bounds(f[0].Pos, Vector3.zero);
            foreach (Frame fr in f) b.Encapsulate(fr.Pos);
            return b;
        }

        /// <summary>Plage en pente douce, mer animée, écume qui va et vient.</summary>
        private static void CreateSea(Theme th, Frame[] f, Mats m)
        {
            Bounds b = TrackBounds(f);
            float zMin = b.min.z - 320f, zMax = b.max.z + 320f;
            float x0 = th.CoastX;
            Quad("PlajPant", x0, x0 + BeachWidth, zMin, zMax, 0f, -BeachDrop, 6f, m.Sand, true);

            // Mer : grille assez fine pour la houle.
            const float cell = 8f;
            float xs = x0 - 2f, xe = x0 + 520f;
            int nx = Mathf.CeilToInt((xe - xs) / cell) + 1, nz = Mathf.CeilToInt((zMax - zMin) / cell) + 1;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = xs + ix * cell, z = zMin + iz * cell;
                verts[iz * nx + ix] = new Vector3(x, WaterY, z);
                uvs[iz * nx + ix] = new Vector2((z - zMin) / 160f, (x - xs) / 160f);
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int k = 0;
            for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = iz * nx + ix;
                tris[k++] = a; tris[k++] = a + nx; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = a + nx; tris[k++] = a + nx + 1;
            }
            GameObject sea = CreateMeshObject("Lanme", verts, uvs, tris, m.Water, false);
            sea.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            sea.AddComponent<WaterSurface>();

            // Écume : deux bandes décalées sur la ligne où la mer touche le sable.
            float shore = x0 + -WaterY * BeachWidth / BeachDrop;
            float slope = BeachDrop / BeachWidth;
            for (int i = 0; i < 2; i++)
            {
                float xa = shore - 1.6f, xb = shore + 1.6f;
                float ya = -(xa - x0) * slope + 0.05f, yb = -(xb - x0) * slope + 0.05f;
                var fv = new[] { new Vector3(xa, ya, zMin), new Vector3(xb, yb, zMin), new Vector3(xa, ya, zMax), new Vector3(xb, yb, zMax) };
                var fu = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2((zMax - zMin) / 25f, 0f), new Vector2((zMax - zMin) / 25f, 1f) };
                GameObject foam = CreateMeshObject("Ekim" + i, fv, fu, new[] { 0, 2, 1, 1, 2, 3 }, m.Foam, false);
                foam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var anim = foam.AddComponent<ShoreFoam>();
                SetVector3(anim, "travel", new Vector3(-3.2f, 3.2f * slope, 0f));
                SetFloat(anim, "phase", i * 0.5f);
                SetFloat(anim, "period", 6.5f + i * 1.3f);
            }
        }

        /// <summary>
        /// Plan à plat ou incliné en X (y0 en x0, y1 en x1), UV en mètres / uvMeters.
        /// Découpé en cases de 100 m au plus : la physique déconseille les très grands triangles.
        /// </summary>
        private static GameObject Quad(string name, float x0, float x1, float z0, float z1, float y0, float y1, float uvMeters,
            Material mat, bool collider, Rect? uvRect = null)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / 100f)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / 100f));
            var v = new Vector3[(nx + 1) * (nz + 1)];
            for (int iz = 0; iz <= nz; iz++)
            for (int ix = 0; ix <= nx; ix++)
            {
                float tx = ix / (float)nx;
                v[iz * (nx + 1) + ix] = new Vector3(Mathf.Lerp(x0, x1, tx), Mathf.Lerp(y0, y1, tx), Mathf.Lerp(z0, z1, iz / (float)nz));
            }
            var tris = new int[nx * nz * 6];
            int k = 0;
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                int a = iz * (nx + 1) + ix, row = nx + 1;
                tris[k++] = a; tris[k++] = a + row; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = a + row; tris[k++] = a + row + 1;
            }
            var uv = uvRect is { } r
                ? v.Select(p => new Vector2((p.x - r.xMin) / r.width, (p.z - r.yMin) / r.height)).ToArray()
                : v.Select(p => new Vector2(p.x / uvMeters, p.z / uvMeters)).ToArray();
            GameObject go = CreateMeshObject(name, v, uv, tris, mat, collider);
            go.isStatic = true;
            return go;
        }

        private static void CreateRoad(Theme th, Frame[] f, Mats m)
        {
            Material surface = ArtLibrary.Get(th.SandTrack ? "Track_Sand" : "Track_Asphalt");
            CreateFlatStrip("Wout", f, -HalfWidth, HalfWidth, 0.02f, 5f, surface, true);
            if (th.SandTrack) return; // Piste de plage : pas de bordures peintes, le sable rejoint la plage.
            CreateFlatStrip("BodiGoch", f, -HalfWidth, -HalfWidth + 0.8f, 0.035f, 2f, m.Kerb, false);
            CreateFlatStrip("BodiDwat", f, HalfWidth - 0.8f, HalfWidth, 0.035f, 2f, m.Kerb, false);
            CreateFlatStrip("LigneGauche", f, -HalfWidth + 1.0f, -HalfWidth + 1.2f, 0.03f, 5f, m.Line, false);
            CreateFlatStrip("LigneDroite", f, HalfWidth - 1.2f, HalfWidth - 1.0f, 0.03f, 5f, m.Line, false);
        }

        /// <summary>Bande à plat le long du circuit. uvMeters = taille d'une répétition de texture.</summary>
        private static void CreateFlatStrip(string name, Frame[] f, float from, float to, float y, float uvMeters,
            Material mat, bool collider)
        {
            int n = f.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[n * 6];
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist += Vector3.Distance(f[i].Pos, f[i - 1].Pos);
                verts[i * 2] = f[i].Pos + f[i].Right * from + Vector3.up * y;
                verts[i * 2 + 1] = f[i].Pos + f[i].Right * to + Vector3.up * y;
                uvs[i * 2] = new Vector2(from / uvMeters, dist / uvMeters);
                uvs[i * 2 + 1] = new Vector2(to / uvMeters, dist / uvMeters);

                int j = (i + 1) % n, t = i * 6;
                tris[t] = i * 2; tris[t + 1] = j * 2; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2 + 1; tris[t + 4] = j * 2; tris[t + 5] = j * 2 + 1;
            }
            CreateMeshObject(name, verts, uvs, tris, mat, collider);
        }

        private static void CreateWalls(Frame[] f, Mats m)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                int n = f.Length;
                var verts = new Vector3[n * 2];
                var uvs = new Vector2[n * 2];
                var tris = new int[n * 12];
                float dist = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) dist += Vector3.Distance(f[i].Pos, f[i - 1].Pos);
                    Vector3 b = f[i].Pos + f[i].Right * side * (HalfWidth + 0.5f);
                    verts[i * 2] = b;
                    verts[i * 2 + 1] = b + Vector3.up * 1.1f;
                    uvs[i * 2] = new Vector2(dist / 2f, 0f);
                    uvs[i * 2 + 1] = new Vector2(dist / 2f, 0.55f);

                    int j = (i + 1) % n, t = i * 12;
                    tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = j * 2;
                    tris[t + 3] = j * 2; tris[t + 4] = i * 2 + 1; tris[t + 5] = j * 2 + 1;
                    // Faces arrière : mur visible et solide des deux côtés.
                    tris[t + 6] = j * 2; tris[t + 7] = i * 2 + 1; tris[t + 8] = i * 2;
                    tris[t + 9] = j * 2 + 1; tris[t + 10] = i * 2 + 1; tris[t + 11] = j * 2;
                }
                // Mur invisible : il garde les karts sur la piste, le décor naturel (CreateBorders) se voit à sa place.
                GameObject wall = CreateMeshObject(side < 0 ? "MiGoch" : "MiDwat", verts, uvs, tris, m.Wall, true);
                Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());
            }
        }

        private static GameObject CreateMeshObject(string name, Vector3[] verts, Vector2[] uvs, int[] tris, Material mat, bool collider)
        {
            var mesh = new Mesh { name = name };
            if (verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private static void CreateStartLine(SplineContainer line, Mats m)
        {
            Frame f = FrameAt(line, 0f);
            Prim(PrimitiveType.Quad, "LigneArrivee", null, f.Pos + Vector3.up * 0.04f,
                new Vector3(HalfWidth * 2f, 2.5f, 1f), m.Line, Quaternion.LookRotation(f.Fwd) * Quaternion.Euler(90f, 0f, 0f));
        }

        private static Transform CreateCheckpoints(SplineContainer line)
        {
            var root = new GameObject("Checkpoints").transform;
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                Frame f = FrameAt(line, (float)i / count);
                var go = new GameObject(i == 0 ? "0 - Ligne d'arrivee" : $"Checkpoint {i}");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(f.Pos + Vector3.up * 3f, Quaternion.LookRotation(f.Fwd));
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(HalfWidth * 2f + 6f, 8f, 2f);
                go.AddComponent<Checkpoint>();
            }
            return root;
        }

        private static void CreateRamp(SplineContainer line, float t, Mats m)
        {
            Frame f = FrameAt(line, t);
            Prim(PrimitiveType.Cube, "Rampe", null, f.Pos + Vector3.up * 0.35f, new Vector3(HalfWidth * 1.2f, 0.4f, 7f),
                m.Ramp, Quaternion.LookRotation(f.Fwd) * Quaternion.Euler(-10f, 0f, 0f), keepCollider: true);
        }

        private static void CreateBoostPad(SplineContainer line, float t, float lateral, Mats m)
        {
            Frame f = FrameAt(line, t);
            var go = new GameObject("PlaqueBoost");
            go.transform.SetPositionAndRotation(f.Pos + f.Right * lateral + Vector3.up * 0.05f, Quaternion.LookRotation(f.Fwd));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 2f, 6f);
            box.center = new Vector3(0f, 1f, 0f);
            go.AddComponent<BoostPad>();
            Prim(PrimitiveType.Quad, "Visuel", go.transform, Vector3.zero, new Vector3(4f, 6f, 1f), m.Boost, Quaternion.Euler(90f, 0f, 0f));
        }

        private static void CreateItemBoxes(SplineContainer line, float[] rows)
        {
            var root = new GameObject("KesBonis").transform;
            foreach (float t in rows)
            {
                Frame f = FrameAt(line, t);
                foreach (float lateral in new[] { -4.5f, -1.5f, 1.5f, 4.5f })
                {
                    var go = new GameObject("KesBonis");
                    go.transform.SetParent(root, false);
                    go.transform.SetPositionAndRotation(f.Pos + f.Right * lateral, Quaternion.LookRotation(f.Fwd));
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(1.8f, 2.4f, 1.8f);
                    box.center = new Vector3(0f, 1.2f, 0f);
                    var item = go.AddComponent<ItemBox>();
                    Transform visual = ArtLibrary.Spawn("ItemBox", go.transform);
                    if (visual != null)
                    {
                        visual.localPosition = new Vector3(0f, 1.2f, 0f);
                        SetRef(item, "visual", visual);
                    }
                }
            }
        }

        private static void CreateArch(SplineContainer line)
        {
            Frame f = FrameAt(line, 0f);
            var root = new GameObject("PotayLabadi").transform;
            root.SetPositionAndRotation(f.Pos, Quaternion.LookRotation(new Vector3(f.Fwd.x, 0f, f.Fwd.z)));
            Material pillar = ArtLibrary.Get("Wall_Paint", null, "Wall_Turquoise.png");
            Material board = ArtLibrary.Get("Concrete_Slab");
            Material sign = ArtLibrary.Get("Sign_Labadi");
            float span = HalfWidth + 1.6f;
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Poto", root, new Vector3(side * span, 3.8f, 0f), new Vector3(1.1f, 7.6f, 1.1f), pillar, Quaternion.identity, true);
                Prim(PrimitiveType.Cube, "Baz", root, new Vector3(side * span, 0.3f, 0f), new Vector3(1.6f, 0.6f, 1.6f), board, Quaternion.identity);
            }
            float width = span * 2f + 1.2f;
            Prim(PrimitiveType.Cube, "Pano", root, new Vector3(0f, 8.1f, 0f), new Vector3(width, 2.9f, 0.4f), board, Quaternion.identity);
            Prim(PrimitiveType.Quad, "AfichAvan", root, new Vector3(0f, 8.1f, -0.21f), new Vector3(width - 0.3f, 2.7f, 1f), sign, Quaternion.identity);
            Prim(PrimitiveType.Quad, "AfichDeye", root, new Vector3(0f, 8.1f, 0.21f), new Vector3(width - 0.3f, 2.7f, 1f), sign, Quaternion.Euler(0f, 180f, 0f));
            Occupied.Add((f.Pos + f.Right * span, 1.5f));
            Occupied.Add((f.Pos - f.Right * span, 1.5f));
        }

        // ---------- Quartier ----------

        private static void CreateNeighborhood(Theme th, Frame[] f)
        {
            var root = new GameObject("Katye").transform;
            PlaceStalls(th, f, root);
            PlaceHouses(th, f, root);
            PlaceBlockWalls(th, f, root);
            PlacePoles(th, f, root);
            PlacePalms(th, f, root);
            PlaceTireStacks(th, f, root);
        }

        private static bool Free(Vector3 p, float r)
        {
            foreach ((Vector3 pos, float radius) in Occupied)
                if ((pos - p).sqrMagnitude < (radius + r) * (radius + r)) return false;
            return true;
        }

        private static Transform Place(string model, Transform parent, Vector3 pos, Quaternion rot, float radius,
            Dictionary<string, Material> overrides = null)
        {
            Transform t = ArtLibrary.Spawn(model, parent, overrides);
            if (t == null) return null;
            pos = OnGround(pos);
            t.SetPositionAndRotation(pos, rot);
            Occupied.Add((pos, radius));
            return t;
        }

        private static Quaternion FacingRoad(Frame fr, Vector3 p, float jitter = 0f)
        {
            Vector3 dir = fr.Pos - p;
            dir.y = 0f;
            return Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(0f, UnityEngine.Random.Range(-jitter, jitter), 0f);
        }

        private static Dictionary<string, Material> RandomWalls() => new()
        {
            ["Wall_Paint"] = ArtLibrary.Get("Wall_Paint", null, WallTextures[UnityEngine.Random.Range(0, WallTextures.Length)]),
        };

        private static void PlaceHouses(Theme th, Frame[] f, Transform root)
        {
            (string model, float radius)[] kinds = { ("House_A", 5.5f), ("House_B", 4.8f), ("Boutik", 4.2f), ("Shack", 2.8f) };
            int placed = 0;
            for (int attempt = 0; attempt < 900 && placed < th.Houses; attempt++)
            {
                Frame fr = f[UnityEngine.Random.Range(0, f.Length)];
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                (string model, float size) = kinds[UnityEngine.Random.Range(0, kinds.Length)];
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 4f + size + UnityEngine.Random.value * 18f);
                if (!th.LandOk(p) || MinDistanceToTrack(f, p) < HalfWidth + 3f + size || !Free(p, size)) continue;
                Place(model, root, p, FacingRoad(fr, p, 6f), size, RandomWalls());
                placed++;
            }
        }

        private static void PlaceStalls(Theme th, Frame[] f, Transform root)
        {
            // Le marché se tient au bord de la ligne droite de départ.
            int placed = 0;
            for (int attempt = 0; attempt < 200 && placed < th.Stalls; attempt++)
            {
                Frame fr = f[UnityEngine.Random.Range(6, Samples / 7)];
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 3.2f);
                if (!Free(p, 1.8f)) continue;
                Place(UnityEngine.Random.value < 0.5f ? "MarketStall" : "UmbrellaStall", root, p, FacingRoad(fr, p, 10f), 1.8f);
                placed++;
            }
        }

        private static void PlaceBlockWalls(Theme th, Frame[] f, Transform root)
        {
            for (int run = 0; run < th.WallRuns; run++)
            {
                int start = UnityEngine.Random.Range(0, f.Length);
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                for (int k = 0; k < 4; k++)
                {
                    Frame fr = f[(start + Mathf.RoundToInt(k * 2.4f)) % f.Length];
                    Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 6f);
                    if (!th.LandOk(p) || MinDistanceToTrack(f, p) < HalfWidth + 4f || !Free(p, 2.1f)) continue;
                    Place("BlockWall", root, p, Quaternion.LookRotation(-fr.Right * side), 2.1f);
                }
            }
        }

        private static void PlacePoles(Theme th, Frame[] f, Transform root)
        {
            Material wire = ArtLibrary.Get("Iron");
            Transform previous = null;
            for (int i = 0; i < f.Length; i += 26)
            {
                Frame fr = f[i];
                Vector3 p = fr.Pos + fr.Right * (HalfWidth + 2.2f);
                bool ok = (!th.Coast || p.x < th.CoastX - 25f) && MinDistanceToTrack(f, p) >= HalfWidth + 1.5f && Free(p, 0.6f);
                if (!ok)
                {
                    previous = null;
                    continue;
                }
                Transform pole = Place("UtilityPole", root, p, Quaternion.LookRotation(-fr.Right), 0.6f);
                if (pole != null && previous != null) Wires(previous, pole, wire, root);
                previous = pole;
            }
        }

        private static void Wires(Transform a, Transform b, Material mat, Transform parent)
        {
            foreach (float x in new[] { -0.9f, 0.3f, 0.9f })
            {
                Vector3 p0 = a.TransformPoint(new Vector3(x, 8.6f, 0f));
                Vector3 p1 = b.TransformPoint(new Vector3(x, 8.6f, 0f));
                var go = new GameObject("Fil");
                go.transform.SetParent(parent, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.widthMultiplier = 0.035f;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                const int points = 10;
                lr.positionCount = points;
                for (int i = 0; i < points; i++)
                {
                    float t = i / (points - 1f);
                    lr.SetPosition(i, Vector3.Lerp(p0, p1, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.7f));
                }
            }
        }

        private static void PlacePalms(Theme th, Frame[] f, Transform root)
        {
            int placed = 0;
            for (int attempt = 0; attempt < 700 && placed < th.Palms; attempt++)
            {
                Frame fr = f[UnityEngine.Random.Range(0, f.Length)];
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 3f + UnityEngine.Random.value * 9f);
                if ((th.Coast && p.x > th.CoastX - 1f) || MinDistanceToTrack(f, p) < HalfWidth + 2.5f || !Free(p, 1.4f)) continue;
                PlacePalm(root, p);
                placed++;
            }
        }

        private static void PlacePalm(Transform root, Vector3 p)
        {
            Transform palm = Place(UnityEngine.Random.value < 0.5f ? "Palm_A" : "Palm_B", root, p,
                Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), 1.4f);
            if (palm != null) palm.localScale = Vector3.one * UnityEngine.Random.Range(0.85f, 1.2f);
        }

        private static void PlaceTireStacks(Theme th, Frame[] f, Transform root)
        {
            int placed = 0;
            for (int attempt = 0; attempt < 200 && placed < th.Tires; attempt++)
            {
                Frame fr = f[UnityEngine.Random.Range(0, f.Length)];
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 1.3f);
                if (MinDistanceToTrack(f, p) < HalfWidth + 1f || !Free(p, 0.7f)) continue;
                Place("TireStack", root, p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), 0.7f);
                placed++;
            }
        }

        /// <summary>Bande de sable entre la piste et la mer : cocotiers, parasols, canots tirés sur le sable, cases.</summary>
        private static void CreateBeach(Theme th, Frame[] f)
        {
            var root = new GameObject("Plaj").transform;
            Bounds b = TrackBounds(f);
            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < th.BeachProps; attempt++)
            {
                float z = UnityEngine.Random.Range(b.min.z - 40f, b.max.z + 40f);
                float roll = UnityEngine.Random.value;
                if (roll < 0.22f)
                {
                    var p = new Vector3(th.CoastX - 3.2f, 0f, z);
                    if (MinDistanceToTrack(f, p) < HalfWidth + 3f || !Free(p, 3.2f)) continue;
                    Place("Boat", root, p, Quaternion.LookRotation(Vector3.right) * Quaternion.Euler(0f, UnityEngine.Random.Range(-15f, 15f), 0f), 3.2f);
                }
                else
                {
                    var p = new Vector3(UnityEngine.Random.Range(th.CoastX - 26f, th.CoastX - 2f), 0f, z);
                    if (MinDistanceToTrack(f, p) < HalfWidth + 3f || !Free(p, 1.8f)) continue;
                    if (roll < 0.62f) PlacePalm(root, p);
                    else if (roll < 0.9f) Place("UmbrellaStall", root, p, Quaternion.LookRotation(Vector3.right), 1.8f);
                    else Place("Shack", root, p, Quaternion.LookRotation(Vector3.right), 2.8f, RandomWalls());
                }
                placed++;
            }

            for (int i = 0; i < th.FloatingBoats; i++)
            {
                var p = new Vector3(UnityEngine.Random.Range(th.CoastX + 25f, th.CoastX + 110f), WaterY - 0.32f,
                    UnityEngine.Random.Range(b.min.z, b.max.z));
                Transform boat = ArtLibrary.Spawn("Boat", root);
                if (boat == null) continue;
                boat.SetPositionAndRotation(p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                boat.gameObject.AddComponent<Bobbing>();
            }
        }

        /// <summary>Mornes verts autour du circuit (jamais du côté de la mer).</summary>
        private static void CreateHills(Theme th, Frame[] f, Mats m)
        {
            if (th.Hills <= 0) return;
            var root = new GameObject("Mon").transform;
            Bounds b = TrackBounds(f);
            float ring = Mathf.Max(b.extents.x, b.extents.z) + 240f;
            int made = 0;
            for (int attempt = 0; attempt < th.Hills * 4 && made < th.Hills; attempt++)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                float dist = ring + UnityEngine.Random.Range(0f, 140f);
                var c = new Vector3(b.center.x + Mathf.Cos(angle) * dist, 0f, b.center.z + Mathf.Sin(angle) * dist);
                if (th.Coast && c.x > th.CoastX - 60f) continue;
                CreateHill(root, OnGround(c), UnityEngine.Random.Range(90f, 170f), UnityEngine.Random.Range(40f, 95f), m.Hill, made * 1.7f);
                made++;
            }
        }

        private static void CreateHill(Transform parent, Vector3 c, float radius, float height, Material mat, float seed)
        {
            const int seg = 24, rings = 7;
            var verts = new List<Vector3> { c + Vector3.up * height };
            for (int r = 1; r <= rings; r++)
            {
                float k = r / (float)rings;
                for (int s = 0; s < seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    float wobble = 1f + 0.18f * Mathf.Sin(a * 3f + seed) + 0.1f * Mathf.Sin(a * 7f + seed * 2f);
                    float rad = radius * k * wobble;
                    float h = r == rings ? -2f : height * (0.5f + 0.5f * Mathf.Cos(k * Mathf.PI)) * (1f + 0.12f * Mathf.Sin(a * 5f + seed));
                    verts.Add(c + new Vector3(Mathf.Cos(a) * rad, h, Mathf.Sin(a) * rad));
                }
            }
            var tris = new List<int>();
            for (int s = 0; s < seg; s++)
            {
                tris.Add(0); tris.Add(1 + (s + 1) % seg); tris.Add(1 + s);
            }
            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < seg; s++)
            {
                int a = 1 + r * seg + s, b2 = 1 + r * seg + (s + 1) % seg;
                int c2 = a + seg, d = b2 + seg;
                tris.Add(a); tris.Add(b2); tris.Add(c2);
                tris.Add(b2); tris.Add(d); tris.Add(c2);
            }
            Vector2[] uvs = verts.Select(p => new Vector2(p.x / 45f, p.z / 45f)).ToArray();
            GameObject hill = CreateMeshObject("Mon", verts.ToArray(), uvs, tris.ToArray(), mat, false);
            hill.transform.SetParent(parent, true);
            hill.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            hill.isStatic = true;
        }

        private static float MinDistanceToTrack(Frame[] f, Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < f.Length; i += 2)
            {
                float dx = f[i].Pos.x - p.x, dz = f[i].Pos.z - p.z;
                best = Mathf.Min(best, dx * dx + dz * dz);
            }
            return Mathf.Sqrt(best);
        }

        // ---------- Bonus ----------

        private static KokoyeProjectile KokoyePrefab(int kartLayer)
        {
            var go = new GameObject("Kokoye") { layer = kartLayer };
            go.AddComponent<Rigidbody>().mass = 0.5f;
            go.AddComponent<SphereCollider>().radius = 0.35f;
            var projectile = go.AddComponent<KokoyeProjectile>();

            var hit = new GameObject("ZonTouche") { layer = kartLayer };
            hit.transform.SetParent(go.transform, false);
            var trigger = hit.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.95f;
            hit.AddComponent<KokoyeHitbox>();

            Transform visual = ArtLibrary.Spawn("Kokoye", go.transform);
            if (visual != null) SetRef(projectile, "visual", visual);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Kokoye.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<KokoyeProjectile>();
        }

        private static MangoTrap MangoPrefab()
        {
            var go = new GameObject("MangoTonbe");
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.0f;
            trigger.center = new Vector3(0f, 0.4f, 0f);
            go.AddComponent<MangoTrap>();
            foreach (Vector3 offset in new[] { new Vector3(-0.35f, 0f, 0.1f), new Vector3(0.3f, 0f, -0.15f), new Vector3(0.05f, 0f, 0.38f) })
            {
                Transform mango = ArtLibrary.Spawn("Mango", go.transform);
                if (mango == null) continue;
                mango.localPosition = offset;
                mango.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/MangoTonbe.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<MangoTrap>();
        }

        // ---------- Karts ----------

        /// <summary>Pilote joué par défaut (quand on lance une scène sans passer par le menu) : Ti Djo.</summary>
        private const int PlayerPilot = 5;

        /// <summary>Catalogue des pilotes, dans l'ordre du menu.</summary>
        private static readonly Pilot[] Pilots =
        {
            new("Manmzèl Loudena", "MotoTaxi", 2.1f, "Driver_Moto", Hibiscus, Sun),
            new("Bòs Wilfrid", "TapTap", 3.4f, "Driver_Car", Lagoon, Paper, "TapTap_Side_B.png"),
            new("Nadège", "MotoTaxi", 2.1f, "Driver_Moto", Mango, Lagoon),
            new("Judens Konpa", "Buggy", 2.8f, "Driver_Car", Violet, Paper),
            new("Sè Naïelie", "Buggy", 2.8f, "Driver_Car", Green, Hibiscus),
            new("Ti Djo", "TapTap", 3.4f, "Driver_Car", Sun, Hex(0x2a5bd7), "TapTap_Side_A.png"),
        };

        private static KartStats StatsFor(string model) => model switch
        {
            "TapTap" => StatsAsset("TapTap", 27f, 11f, 82f, 70f, 22f),
            "MotoTaxi" => StatsAsset("MotoTaxi", 29f, 18f, 110f, 50f, 15f),
            _ => StatsAsset("Buggy", 28f, 14f, 95f, 60f, 18f),
        };

        /// <summary>Les six karts, dans l'ordre du catalogue. Ti Djo part en fond de grille ; RaceSetup échange selon le choix du menu.</summary>
        private static KartController[] CreateGrid(SplineContainer line, int kartLayer, KokoyeProjectile kokoye, MangoTrap mango,
            MangoTrap banana, HomingRocket rocket)
        {
            float[] lanes = { 0f, -0.5f, 0.5f, -0.3f, 0.3f, 0.15f };
            float[] skills = { 0.99f, 0.96f, 0.94f, 0.97f, 0.93f, 0.95f };

            float length = line.CalculateLength();
            var karts = new KartController[Pilots.Length];
            for (int i = 0; i < Pilots.Length; i++)
            {
                int row = i / 2;
                float side = i % 2 == 0 ? -3.2f : 3.2f;
                // Grille juste APRÈS la ligne d'arrivée.
                Frame f = FrameAt(line, (26f - row * 8f) / length);
                Vector3 pos = f.Pos + f.Right * side + Vector3.up * 0.05f;
                Pilot p = Pilots[i];

                KartController kart = CreateKart(p, i == PlayerPilot, pos, Quaternion.LookRotation(f.Fwd), StatsFor(p.Model),
                    kartLayer, line, lanes[i], skills[i]);
                var powerUps = kart.gameObject.AddComponent<PowerUpSystem>();
                SetRef(powerUps, "kokoyePrefab", kokoye);
                SetRef(powerUps, "mangoPrefab", mango);
                SetRef(powerUps, "bananaPrefab", banana);
                SetRef(powerUps, "rocketPrefab", rocket);

                var power = kart.gameObject.AddComponent<PilotPower>();
                SetInt(power, "power", (int)PilotPowers[i].type);
                SetString(power, "powerName", PilotPowers[i].name);
                SetRef(power, "mangoPrefab", mango);
                SetRef(power, "hornClip", Clip("Horn"));
                karts[i] = kart;
                Occupied.Add((pos, 2f));
            }
            return karts;
        }

        /// <summary>Véhicule peint + pilote habillé à ses couleurs, mis à la bonne longueur, posé au sol.</summary>
        private static Transform SpawnPilotModel(Pilot pilot, Transform parent)
        {
            Dictionary<string, Material> paint = null;
            if (pilot.SideArt != null)
                paint = new Dictionary<string, Material> { ["TT_SideArt"] = ArtLibrary.Get("TT_SideArt", null, pilot.SideArt) };
            Transform vehicle = ArtLibrary.Spawn(pilot.Model, parent, paint);
            if (vehicle == null) return null;

            var outfit = new Dictionary<string, Material>
            {
                ["Drv_Helmet"] = ArtLibrary.Get("Drv_Helmet", pilot.Helmet),
                ["Drv_Shirt"] = ArtLibrary.Get("Drv_Shirt", pilot.Shirt),
            };
            Transform seat = ArtLibrary.FindDeep(vehicle, "Seat");
            Transform driver = ArtLibrary.Spawn(pilot.Driver, vehicle, outfit);
            if (driver != null && seat != null)
            {
                driver.position = seat.position;
                driver.rotation = vehicle.rotation;
            }
            ArtLibrary.FitLength(vehicle, pilot.Length);
            return vehicle;
        }

        private static AudioClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{ArtLibrary.ArtRoot}/Audio/{name}.wav");

        private static KartController CreateKart(Pilot pilot, bool isPlayer, Vector3 pos, Quaternion rot, KartStats stats,
            int kartLayer, SplineContainer line, float lane, float skill)
        {
            var root = new GameObject($"Kart ({pilot.Name})");
            root.transform.SetPositionAndRotation(pos, rot);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Transform vehicle = SpawnPilotModel(pilot, visual);

            float back = -pilot.Length * 0.42f;
            ParticleSystem sparksL = Particles("EtenselG", visual, new Vector3(-0.5f, 0.12f, back), Color.white, 0.12f, 3f, 60f, 0.25f);
            ParticleSystem sparksR = Particles("EtenselD", visual, new Vector3(0.5f, 0.12f, back), Color.white, 0.12f, 3f, 60f, 0.25f);
            ParticleSystem flame = Particles("Flanm", visual, new Vector3(0f, 0.45f, back - 0.1f), new Color(1f, 0.6f, 0.2f), 0.35f, 4f, 80f, 0.2f);

            var sphere = new GameObject("Sphere") { layer = kartLayer };
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            sphere.AddComponent<SphereCollider>().radius = 0.5f;
            var rb = sphere.AddComponent<Rigidbody>();
            rb.mass = 1f;

            var kart = root.AddComponent<KartController>();
            SetRef(kart, "stats", stats);
            SetRef(kart, "sphere", rb);
            SetRef(kart, "visual", visual);
            SetInt(kart, "groundMask", kartLayer > 0 ? ~(1 << kartLayer) : ~0);

            // Chaque kart sait conduire seul ou être piloté : RaceSetup active l'un ou l'autre.
            var human = root.AddComponent<PlayerKartInput>();
            human.enabled = isPlayer;
            var ai = root.AddComponent<AIDriver>();
            SetRef(ai, "racingLine", line);
            SetFloat(ai, "laneOffset", lane);
            SetFloat(ai, "skill", skill);
            ai.enabled = !isPlayer;

            var tracker = root.AddComponent<LapTracker>();
            SetString(tracker, "displayName", pilot.Name);
            SetBool(tracker, "isPlayer", isPlayer);

            var sound = root.AddComponent<KartAudio>();
            SetRef(sound, "engine", Clip("Engine_Loop"));
            SetRef(sound, "skid", Clip("Skid_Loop"));
            SetRef(sound, "horn", Clip("Horn"));
            SetRef(sound, "hornMelody", Clip("Horn_Melody"));
            SetFloat(sound, "enginePitch", pilot.Model switch { "MotoTaxi" => 1.35f, "TapTap" => 0.85f, _ => 1.05f });

            var fx = root.AddComponent<KartEffects>();
            SetRefArray(fx, "driftSparks", new Object[] { sparksL, sparksR });
            SetRef(fx, "boostFlame", flame);
            bool sand = _th != null && _th.SandTrack;
            SetRef(fx, "dust", DustTrail(root.transform, new Vector3(0f, 0.25f, back),
                sand ? new Color(0.86f, 0.77f, 0.6f, 0.55f) : new Color(0.62f, 0.55f, 0.47f, 0.4f)));
            SetRefArray(fx, "skidMarks", new Object[]
            {
                SkidTrail(root.transform, new Vector3(-0.45f, 0.05f, back * 0.85f)),
                SkidTrail(root.transform, new Vector3(0.45f, 0.05f, back * 0.85f)),
            });
            SetFloat(kart, "roadRoughness", sand ? 0.05f : 0.02f);

            if (vehicle != null)
            {
                List<Transform> wheels = ArtLibrary.FindAll(vehicle, "Wheel_");
                var spin = root.AddComponent<KartWheels>();
                SetRefArray(spin, "wheels", wheels.Cast<Object>().ToArray());
                SetRefArray(spin, "steering", wheels.Where(w => w.name.Contains("_F")).Cast<Object>().ToArray());
            }
            return kart;
        }

        private static KartStats StatsAsset(string name, float maxSpeed, float acceleration, float steering, float grip, float driftGrip)
        {
            string path = $"{Root}/Data/{name}.asset";
            var stats = AssetDatabase.LoadAssetAtPath<KartStats>(path);
            if (stats != null) return stats;

            stats = ScriptableObject.CreateInstance<KartStats>();
            stats.maxSpeed = maxSpeed;
            stats.acceleration = acceleration;
            stats.steering = steering;
            stats.grip = grip;
            stats.driftGrip = driftGrip;
            AssetDatabase.CreateAsset(stats, path);
            return stats;
        }

        private static ParticleSystem Particles(string name, Transform parent, Vector3 localPos, Color color,
            float size, float speed, float rate, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = 0.5f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.05f;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial();
            return ps;
        }

        // ---------- Caméra et HUD ----------

        private static (CinemachineCamera, KartCameraFx) CreateCamera(KartController player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 700f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.None;
            Transform target = player.transform;
            camGo.transform.SetPositionAndRotation(target.position - target.forward * 8f + Vector3.up * 3.5f, target.rotation);

            var vcGo = new GameObject("KameraPoursuite");
            var vc = vcGo.AddComponent<CinemachineCamera>();
            vc.Follow = target;
            vc.LookAt = target;
            LensSettings lens = vc.Lens;
            lens.FieldOfView = 62f;
            lens.FarClipPlane = 700f;
            vc.Lens = lens;

            var follow = vcGo.AddComponent<CinemachineFollow>();
            // Caméra basse et proche, le kart bien visible : l'esprit des karts arcade mobiles.
            follow.FollowOffset = new Vector3(0f, 2.4f, -6.2f);
            TrackerSettings tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
            tracker.PositionDamping = new Vector3(0.2f, 0.3f, 0.35f);
            tracker.RotationDamping = new Vector3(0f, 0.5f, 0f);
            follow.TrackerSettings = tracker;

            var composer = vcGo.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = new Vector3(0f, 1.0f, 2.6f);

            var shake = vcGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
            shake.NoiseProfile = ShakeProfile();
            shake.AmplitudeGain = 0f;

            var fx = vcGo.AddComponent<KartCameraFx>();
            SetRef(fx, "cam", vc);
            SetRef(fx, "kart", player);
            SetRef(fx, "noise", shake);
            SetFloat(fx, "baseFov", 62f);
            return (vc, fx);
        }

        private static void CreateHud(RaceManager race)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvas = canvasGo.transform;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font display = DisplayFont();
            Text position = Label(canvas, "Plas", display, 96, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(400f, 120f), Sun);
            Text lap = Label(canvas, "Tou", display, 44, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(44f, -150f), new Vector2(400f, 60f), Paper);
            Text time = Label(canvas, "Kwonomet", display, 44, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(400f, 60f), Paper);
            Text speed = Label(canvas, "Vites", display, 40, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-40f, -95f), new Vector2(400f, 60f), Paper);
            Text center = Label(canvas, "MesajSantral", display, 140, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1600f, 240f), Sun);

            var hud = canvasGo.AddComponent<RaceHUD>();
            SetRef(hud, "race", race);
            SetRef(hud, "positionText", position);
            SetRef(hud, "lapText", lap);
            SetRef(hud, "timeText", time);
            SetRef(hud, "speedText", speed);
            SetRef(hud, "centerText", center);

            CreateItemSlot(canvas, race);

            TouchButton(canvas, "Goch", "<", "<Gamepad>/dpad/left", font, new Vector2(0f, 0f), new Vector2(60f, 60f), 220f);
            TouchButton(canvas, "Dwat", ">", "<Gamepad>/dpad/right", font, new Vector2(0f, 0f), new Vector2(310f, 60f), 220f);
            TouchButton(canvas, "Drift", "DRIFT", "<Gamepad>/buttonSouth", font, new Vector2(1f, 0f), new Vector2(-60f, 60f), 240f);
            TouchButton(canvas, "Bonis", "BONIS", "<Gamepad>/buttonEast", font, new Vector2(1f, 0f), new Vector2(-330f, 60f), 180f);
            TouchButton(canvas, "Fren", "FREN", "<Gamepad>/leftTrigger", font, new Vector2(1f, 0f), new Vector2(-60f, 330f), 150f);
            TouchButton(canvas, "Klakson", "KLAKSON", "<Gamepad>/buttonNorth", font, new Vector2(1f, 0f), new Vector2(-330f, 330f), 130f);

            CreateArcadeHud(canvas, hud, font, display);
            CreateResultsPanel(canvas, hud, font, display);
        }

        private static void CreateResultsPanel(Transform canvas, RaceHUD hud, Font font, Font display)
        {
            RectTransform panel = Panel(canvas, "Rezilta", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 700f),
                new Color(Night.r, Night.g, Night.b, 0.9f), Sun);
            Label(panel, "Tit", display, 64, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 90f), Sun).text = "REZILTA";

            Text list = Label(panel, "Lis", font, 40, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(760f, 400f), Paper);
            list.lineSpacing = 1.15f;
            list.supportRichText = true;

            Button retry = UiButton(panel, "Rejwe", "REJWE", display, new Vector2(0.5f, 0f), new Vector2(-190f, 50f), new Vector2(320f, 100f), Lagoon, Paper);
            Button menu = UiButton(panel, "Meni", "MENI", display, new Vector2(0.5f, 0f), new Vector2(190f, 50f), new Vector2(320f, 100f), Hibiscus, Paper);

            SetRef(hud, "resultsPanel", panel.gameObject);
            SetRef(hud, "resultsText", list);
            SetRef(hud, "retryButton", retry);
            SetRef(hud, "menuButton", menu);
        }

        private static void CreateItemSlot(Transform canvas, RaceManager race)
        {
            var slotGo = new GameObject("KaseBonis", typeof(RectTransform));
            slotGo.transform.SetParent(canvas, false);
            var slot = (RectTransform)slotGo.transform;
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 1f);
            slot.anchoredPosition = new Vector2(0f, -24f);
            slot.sizeDelta = new Vector2(150f, 150f);
            var bg = slotGo.AddComponent<Image>();
            bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(Night.r, Night.g, Night.b, 0.92f);
            var outline = slotGo.AddComponent<Outline>();
            outline.effectColor = Sun;
            outline.effectDistance = new Vector2(4f, -4f);

            var iconGo = new GameObject("Ikon", typeof(RectTransform));
            iconGo.transform.SetParent(slot, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(118f, 118f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var item = slotGo.AddComponent<ItemHUD>();
            SetRef(item, "race", race);
            SetRef(item, "slot", slot);
            SetRef(item, "icon", icon);
            string[] iconFiles = { null, "Icon_Kokoye", "Icon_Mango", "Icon_TwaKokoye", "Icon_Fize", "Icon_PoBannann", "Icon_Lanbi",
                "Icon_Kafe", "Icon_Parasol", "Icon_Loraj", "Icon_Pikliz", "Icon_TaptapFou" };
            SetRefArray(item, "icons", iconFiles.Select(f => (Object)(f == null ? null : ArtLibrary.LoadSprite(f + ".png"))).ToArray());
            Text name = Label(slot, "NonBonis", DisplayFont(), 24, TextAnchor.UpperCenter, new Vector2(0.5f, 0f), new Vector2(0f, -40f),
                new Vector2(300f, 34f), Paper);
            SetRef(item, "label", name);
        }

        private static Text Label(Transform parent, string name, Font font, int size, TextAnchor align,
            Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = align;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = "";

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        private static void TouchButton(Transform parent, string name, string caption, string controlPath, Font font,
            Vector2 anchor, Vector2 anchoredPos, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(size, size);

            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = new Color(Night.r, Night.g, Night.b, 0.55f);

            // Champ sérialisé : évite de créer une manette virtuelle pendant la construction dans l'éditeur.
            var button = go.AddComponent<OnScreenButton>();
            SetString(button, "m_ControlPath", controlPath);

            Text label = Label(go.transform, "Teks", font, caption.Length > 1 ? 40 : 90, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), Paper);
            label.text = caption;
        }

        // ---------- Matériaux simples ----------

        private static Material Mat(string name, Color color, float smoothness, float emission = 0f)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(ArtLibrary.LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = ArtLibrary.LitShader();
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ParticleMaterial()
        {
            string path = $"{Root}/Materials/Particule.mat";
            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                : Shader.Find("Legacy Shaders/Particles/Additive");
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
                mat.SetFloat("_Blend", 2f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Utilitaires ----------

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
            Material mat, Quaternion rot, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
            }
            else
            {
                go.transform.SetPositionAndRotation(pos, rot);
            }
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Color Hex(int rgb) =>
            new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static int EnsureLayer(string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            int free = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                string current = layers.GetArrayElementAtIndex(i).stringValue;
                if (current == name) return i;
                if (free < 0 && string.IsNullOrEmpty(current)) free = i;
            }
            if (free < 0)
            {
                Debug.LogWarning("Rally Ayiti : aucun layer libre pour « Kart ».");
                return 0;
            }
            layers.GetArrayElementAtIndex(free).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return free;
        }

        private static SerializedProperty Prop(Object target, string field, out SerializedObject so)
        {
            so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogError($"Rally Ayiti : champ « {field} » introuvable sur {target.GetType().Name}.");
            return p;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefArray(Object target, string field, Object[] values)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string field, int value)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Object target, string field, Vector3 value)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
