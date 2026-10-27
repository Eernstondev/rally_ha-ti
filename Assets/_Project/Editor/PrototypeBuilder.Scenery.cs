using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Sol peint (terre, herbe, sable selon la distance à la piste), départ animé (gradins, foule, fanions,
    /// panneaux) et entourage qui remplit tout l'espace : herbes, buissons, bougainvilliers, bananiers,
    /// manguiers, rochers, clôtures, hameaux et véhicules garés.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const string GeneratedDir = ArtLibrary.ArtRoot + "/Generated";

        // ---------- Sol ----------

        private static void CreatePaintedGround(Theme th, Frame[] f)
        {
            Bounds b = TrackBounds(f);
            const float margin = 320f;
            float x0 = b.min.x - margin, x1 = th.Coast ? th.CoastX : b.max.x + margin;
            float z0 = b.min.z - margin, z1 = b.max.z + margin;
            var area = new Rect(x0, z0, x1 - x0, z1 - z0);

            Texture2D macro = PaintGround(th, f, area);
            Material mat = GroundMaterial(th, macro, area);
            BuildTerrain(f, area, mat);
        }

        /// <summary>Texture vue de haut de tout le terrain : terre au bord de la piste, herbe variée, sable vers la mer.</summary>
        private static Texture2D PaintGround(Theme th, Frame[] f, Rect area)
        {
            const int res = 2048, field = 256;

            // Distance à la piste sur une grille grossière, interpolée ensuite.
            var dist = new float[field * field];
            for (int j = 0; j < field; j++)
            for (int i = 0; i < field; i++)
            {
                var p = new Vector3(area.xMin + (i + 0.5f) / field * area.width, 0f, area.yMin + (j + 0.5f) / field * area.height);
                dist[j * field + i] = MinDistanceToTrack(f, p);
            }

            float Dist(float u, float v)
            {
                float fx = Mathf.Clamp(u * field - 0.5f, 0f, field - 1.001f), fz = Mathf.Clamp(v * field - 0.5f, 0f, field - 1.001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                float a = Mathf.Lerp(dist[iz * field + ix], dist[iz * field + ix + 1], tx);
                float c = Mathf.Lerp(dist[(iz + 1) * field + ix], dist[(iz + 1) * field + ix + 1], tx);
                return Mathf.Lerp(a, c, tz);
            }

            // Bas-côtés : sable piétiné sur une piste de plage, terre battue ailleurs.
            Color dirt = th.SandTrack ? new Color(0.8f, 0.7f, 0.52f) : new Color(0.56f, 0.44f, 0.31f);
            Color dirtDark = th.SandTrack ? new Color(0.68f, 0.58f, 0.43f) : new Color(0.44f, 0.34f, 0.24f);
            var grassA = new Color(0.35f, 0.5f, 0.2f);
            var grassB = new Color(0.47f, 0.57f, 0.25f);
            var grassDark = new Color(0.24f, 0.37f, 0.15f);
            var dry = new Color(0.63f, 0.57f, 0.37f);
            var sand = new Color(0.787f, 0.699f, 0.514f);

            var pixels = new Color32[res * res];
            for (int y = 0; y < res; y++)
            {
                float v = (y + 0.5f) / res;
                float z = area.yMin + v * area.height;
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res;
                    float wx = area.xMin + u * area.width;
                    float d = Dist(u, v);
                    float n1 = Mathf.PerlinNoise(wx * 0.011f + 3.1f, z * 0.011f + 7.7f);
                    float n2 = Mathf.PerlinNoise(wx * 0.043f + 31f, z * 0.043f + 17f);
                    float n3 = Mathf.PerlinNoise(wx * 0.19f + 5f, z * 0.19f + 9f);

                    Color grass = Color.Lerp(grassA, grassB, n1);
                    grass = Color.Lerp(grass, grassDark, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, n2)) * 0.6f);
                    float dryness = Mathf.InverseLerp(1f - th.Dry - 0.15f, 1f - th.Dry + 0.3f, n1 * 0.6f + n2 * 0.4f);
                    grass = Color.Lerp(grass, dry, Mathf.SmoothStep(0f, 1f, dryness) * 0.85f);

                    float edge = HalfWidth + 7f + n2 * 9f;
                    float toGrass = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HalfWidth + 1.2f, edge, d));
                    Color c = Color.Lerp(Color.Lerp(dirt, dirtDark, n3), grass, toGrass);

                    if (th.Coast)
                    {
                        float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(th.CoastX - 44f, th.CoastX - 20f, wx + (n2 - 0.5f) * 16f));
                        c = Color.Lerp(c, sand * (0.96f + 0.08f * n3), s);
                    }
                    c *= 0.92f + 0.16f * n3;
                    c.a = 1f;
                    pixels[y * res + x] = c;
                }
            }

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            EnsureFolder(GeneratedDir);
            string path = $"{GeneratedDir}/Sol_{th.Name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = res;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Texture d'ensemble + grain de détail répété (cailloux, brins) pour la netteté de près.</summary>
        private static Material GroundMaterial(Theme th, Texture2D macro, Rect area)
        {
            string path = $"{Root}/Materials/Sol_{th.Name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(ArtLibrary.LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = ArtLibrary.LitShader();
            mat.mainTexture = macro;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.04f);

            var detail = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtLibrary.ArtRoot}/Textures/Ground_Detail.png");
            var detailNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtLibrary.ArtRoot}/Textures/Sand_N.png");
            if (detail != null && mat.HasProperty("_DetailAlbedoMap"))
            {
                mat.SetTexture("_DetailAlbedoMap", detail);
                mat.SetTextureScale("_DetailAlbedoMap", new Vector2(area.width / 3.5f, area.height / 3.5f));
                mat.SetFloat("_DetailAlbedoMapScale", 1f);
                if (detailNormal != null)
                {
                    mat.SetTexture("_DetailNormalMap", detailNormal);
                    mat.SetFloat("_DetailNormalMapScale", 0.6f);
                }
                mat.EnableKeyword("_DETAIL_MULX2");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Départ : gradins, foule, fanions, panneaux ----------

        private static Frame FrameAtDistance(SplineContainer line, float length, float meters) =>
            FrameAt(line, Mathf.Repeat(meters / length, 1f));

        private static void CreateStartArea(Theme th, Frame[] f, SplineContainer line)
        {
            var root = new GameObject("Depa").transform;
            float length = line.CalculateLength();

            // Gradins de chaque côté de la ligne droite, remplis de supporters.
            foreach (float side in new[] { -1f, 1f })
            foreach (float along in new[] { -24f, -12f })
            {
                Frame fr = FrameAtDistance(line, length, along + (side > 0 ? 4f : 0f));
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 6.6f);
                if (!th.LandOk(p) || !Free(p, 4.6f)) continue;
                Transform stand = Place("Bleachers", root, p, Quaternion.LookRotation(-fr.Right * side), 4.6f);
                if (stand == null) continue;
                for (int row = 0; row < 4; row++)
                for (float x = -3.5f; x <= 3.5f; x += UnityEngine.Random.Range(0.75f, 1.4f))
                {
                    if (UnityEngine.Random.value < 0.2f) continue;
                    var local = new Vector3(x, 0.49f + row * 0.5f, -row * 0.8f - 0.05f);
                    SpawnPerson(root, stand.TransformPoint(local), stand.rotation, UnityEngine.Random.value < 0.55f);
                }
            }

            // Foule debout derrière le muret, autour de la ligne.
            for (int i = 0; i < 36; i++)
            {
                float along = UnityEngine.Random.Range(-40f, 18f);
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Frame fr = FrameAtDistance(line, length, along);
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + UnityEngine.Random.Range(2f, 3.6f));
                if (!Free(p, 0.45f)) continue;
                if (SpawnPerson(root, OnGround(p), Quaternion.LookRotation(-fr.Right * side), UnityEngine.Random.value < 0.6f) != null)
                    Occupied.Add((p, 0.45f));
            }

            // Jour de course : barrières, drapeaux, tentes, tribunes (Kenney).
            CreateRaceDay(th, f, line, root);

            // Guirlandes de fanions au-dessus de la ligne droite.
            Material pennants = ArtLibrary.Get("Pennants");
            Material pole = ArtLibrary.Get("Paint_White");
            Vector3? previousL = null, previousR = null;
            foreach (float along in new[] { -34f, -20f, -7f, 9f })
            {
                Frame fr = FrameAtDistance(line, length, along);
                Vector3 l = OnGround(fr.Pos - fr.Right * (HalfWidth + 1.4f)), r = OnGround(fr.Pos + fr.Right * (HalfWidth + 1.4f));
                foreach (Vector3 p in new[] { l, r })
                {
                    Prim(PrimitiveType.Cylinder, "PotoFanyon", root, p + Vector3.up * 3.6f, new Vector3(0.14f, 3.6f, 0.14f), pole, Quaternion.identity);
                    Occupied.Add((p, 0.5f));
                }
                Vector3 topL = l + Vector3.up * 7f, topR = r + Vector3.up * 7f;
                PennantString(root, topL, topR, 1.1f, pennants);
                if (previousL is { } pl) PennantString(root, pl, topL, 0.7f, pennants);
                if (previousR is { } pr) PennantString(root, pr, topR, 0.7f, pennants);
                previousL = topL;
                previousR = topR;
            }

            // Grands panneaux publicitaires le long du circuit.
            string[] boards = { "Billboard_Kafe.png", "Billboard_Pen.png", "Billboard_Rally.png" };
            int placed = 0;
            for (int attempt = 0; attempt < 200 && placed < th.Billboards; attempt++)
            {
                int i = UnityEngine.Random.Range(Samples / 12, Samples - Samples / 12);
                Frame fr = f[i];
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 9f);
                if (!th.LandOk(p) || MinDistanceToTrack(f, p) < HalfWidth + 7f || !Free(p, 4f)) continue;
                var art = new Dictionary<string, Material> { ["Billboard_Art"] = ArtLibrary.Get("Billboard_Art", null, boards[placed % boards.Length]) };
                Quaternion rot = Quaternion.LookRotation(-fr.Right * side) * Quaternion.Euler(0f, side * -25f, 0f);
                Place("Billboard", root, p, rot, 4f, art);
                placed++;
            }
        }

        /// <summary>Fil de fanions qui pend entre deux points (chaînette), visible des deux côtés.</summary>
        private static void PennantString(Transform parent, Vector3 a, Vector3 b, float sag, Material mat)
        {
            const int segments = 18;
            const float flagHeight = 0.75f, tile = 2.4f;
            var verts = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            float travelled = 0f;
            Vector3 prev = a;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * sag);
                travelled += Vector3.Distance(prev, p);
                prev = p;
                verts[i * 2] = p;
                verts[i * 2 + 1] = p + Vector3.down * flagHeight;
                uvs[i * 2] = new Vector2(travelled / tile, 1f);
                uvs[i * 2 + 1] = new Vector2(travelled / tile, 0f);
            }
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int k = i * 6, v = i * 2;
                tris[k] = v; tris[k + 1] = v + 2; tris[k + 2] = v + 1;
                tris[k + 3] = v + 1; tris[k + 4] = v + 2; tris[k + 5] = v + 3;
            }
            GameObject go = CreateMeshObject("Fanyon", verts, uvs, tris, mat, false);
            go.transform.SetParent(parent, true);
            // Pas d'ombre : vues du soleil, les guirlandes zébraient toute la piste.
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- Entourage ----------

        private static void CreateScenery(Theme th, Frame[] f)
        {
            var root = new GameObject("Antouraj").transform;
            Bounds b = TrackBounds(f);
            b.Expand(new Vector3(400f, 0f, 400f));

            PlaceVillages(th, f, b, root);
            PlaceParkedVehicles(th, f, root);
            PlaceFences(th, f, root);
            PlaceGroves(th, f, b, root);
            Scatter(th, f, b, root, "Bougainvillea", th.Flowers, 5f, 30f, 1.8f, 0.8f, 1.25f);
            Scatter(th, f, b, root, "Bush", th.Bushes * 7 / 10, 3.5f, 55f, 1.3f, 0.75f, 1.5f);
            Scatter(th, f, b, root, "Bush", th.Bushes * 3 / 10, 55f, 200f, 1.3f, 0.9f, 1.7f);
            Scatter(th, f, b, root, "Rock", th.Rocks, 4f, 160f, 1.2f, 0.45f, 1.6f, sink: 0.18f);
            Scatter(th, f, b, root, "GrassTuft", th.Grass * 55 / 100, 1.7f, 9f, 0.35f, 0.7f, 1.5f, register: false, shadows: false);
            Scatter(th, f, b, root, "GrassTuft", th.Grass * 45 / 100, 9f, 70f, 0.35f, 0.8f, 1.7f, register: false, shadows: false);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = true;
        }

        /// <summary>
        /// Cherche un point libre dont la distance au bord de la piste est dans [dMin, dMax].
        /// Près de la piste : à partir d'un point du tracé. Loin : tirage dans toute la zone.
        /// </summary>
        private static bool TrySpot(Theme th, Frame[] f, Bounds area, float dMin, float dMax, float radius, out Vector3 p)
        {
            if (dMax <= 60f)
            {
                Frame fr = f[UnityEngine.Random.Range(0, f.Length)];
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                p = fr.Pos + fr.Right * side * UnityEngine.Random.Range(HalfWidth + dMin, HalfWidth + dMax);
            }
            else
            {
                p = new Vector3(UnityEngine.Random.Range(area.min.x, area.max.x), 0f, UnityEngine.Random.Range(area.min.z, area.max.z));
            }
            if (th.Coast && p.x > th.CoastX - 30f) return false;
            float d = MinDistanceToTrack(f, p) - HalfWidth;
            return d >= dMin && d <= dMax && Free(p, radius);
        }

        private static void Scatter(Theme th, Frame[] f, Bounds area, Transform root, string model, int count, float dMin, float dMax,
            float radius, float minScale, float maxScale, float sink = 0f, bool register = true, bool shadows = true)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 12 && placed < count; attempt++)
            {
                if (!TrySpot(th, f, area, dMin, dMax, radius, out Vector3 p)) continue;
                p = OnGround(p);
                Transform t = ArtLibrary.Spawn(model, root);
                if (t == null) return;
                float s = UnityEngine.Random.Range(minScale, maxScale);
                t.SetPositionAndRotation(p + Vector3.down * (sink * s), Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                t.localScale = Vector3.one * s;
                if (register) Occupied.Add((p, radius * s));
                if (!shadows)
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                placed++;
            }
        }

        /// <summary>Manguiers en bosquets, bananiers en touffes, quelques arbres isolés près de la route.</summary>
        private static void PlaceGroves(Theme th, Frame[] f, Bounds area, Transform root)
        {
            int target = th.Trees * 3 / 4, trees = 0;
            for (int attempt = 0; attempt < 400 && trees < target; attempt++)
            {
                if (!TrySpot(th, f, area, 25f, 230f, 4f, out Vector3 center)) continue;
                int n = UnityEngine.Random.Range(3, 8);
                for (int k = 0; k < n && trees < target; k++)
                {
                    Vector2 o = UnityEngine.Random.insideUnitCircle * 22f;
                    var p = center + new Vector3(o.x, 0f, o.y);
                    if ((th.Coast && p.x > th.CoastX - 30f) || MinDistanceToTrack(f, p) < HalfWidth + 12f || !Free(p, 3.6f)) continue;
                    if (PlaceScaled("Tree_Mango", root, p, 3.6f, 0.85f, 1.3f)) trees++;
                }
            }
            Scatter(th, f, area, root, "Tree_Mango", th.Trees / 4, 12f, 45f, 3.6f, 0.8f, 1.15f);

            int bananas = 0;
            for (int attempt = 0; attempt < 400 && bananas < th.Bananas; attempt++)
            {
                if (!TrySpot(th, f, area, 7f, 120f, 1.6f, out Vector3 center)) continue;
                int n = UnityEngine.Random.Range(3, 7);
                for (int k = 0; k < n && bananas < th.Bananas; k++)
                {
                    Vector2 o = UnityEngine.Random.insideUnitCircle * 5f;
                    var p = center + new Vector3(o.x, 0f, o.y);
                    if (MinDistanceToTrack(f, p) < HalfWidth + 5f || !Free(p, 1.4f)) continue;
                    if (PlaceScaled("BananaPlant", root, p, 1.4f, 0.8f, 1.25f)) bananas++;
                }
            }
        }

        private static bool PlaceScaled(string model, Transform root, Vector3 p, float radius, float minScale, float maxScale)
        {
            Transform t = Place(model, root, p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), radius);
            if (t == null) return false;
            t.localScale = Vector3.one * UnityEngine.Random.Range(minScale, maxScale);
            return true;
        }

        /// <summary>Petits hameaux à l'écart : maisons tournées vers une cour, arbre, fleurs, moto garée.</summary>
        private static void PlaceVillages(Theme th, Frame[] f, Bounds area, Transform root)
        {
            (string model, float radius)[] kinds = { ("House_A", 5.5f), ("House_B", 4.8f), ("Shack", 2.8f), ("Boutik", 4.2f) };
            int made = 0;
            for (int attempt = 0; attempt < 300 && made < th.Villages; attempt++)
            {
                if (!TrySpot(th, f, area, 40f, 210f, 16f, out Vector3 center) || !th.LandOk(center)) continue;
                int houses = UnityEngine.Random.Range(3, 6);
                for (int k = 0; k < houses; k++)
                {
                    float a = (k + UnityEngine.Random.value * 0.5f) / houses * Mathf.PI * 2f;
                    Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * UnityEngine.Random.Range(11f, 17f);
                    (string model, float r) = kinds[UnityEngine.Random.Range(0, kinds.Length)];
                    if (!th.LandOk(p) || MinDistanceToTrack(f, p) < HalfWidth + 20f || !Free(p, r)) continue;
                    Vector3 dir = center - p;
                    Place(model, root, p, Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(0f, UnityEngine.Random.Range(-12f, 12f), 0f), r, RandomWalls());
                }
                if (Free(center, 3.5f)) PlaceScaled("Tree_Mango", root, center, 3.5f, 0.9f, 1.2f);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = center + new Vector3(UnityEngine.Random.Range(-9f, 9f), 0f, UnityEngine.Random.Range(-9f, 9f));
                    if (Free(p, 1.8f)) PlaceScaled("Bougainvillea", root, p, 1.8f, 0.85f, 1.2f);
                }
                Vector3 moto = center + new Vector3(UnityEngine.Random.Range(-6f, 6f), 0f, UnityEngine.Random.Range(-6f, 6f));
                if (Free(moto, 1.5f))
                {
                    ParkVehicle(root, "MotoTaxi", 2.1f, moto, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                    Occupied.Add((moto, 1.5f));
                }
                made++;
            }
        }

        /// <summary>Tap-taps et motos garés au bord de la route, sans chauffeur.</summary>
        /// <summary>Voitures du quotidien (Kenney, Quaternius) garées au bord du circuit.</summary>
        private static readonly string[] ParkedCars =
        {
            "Car_Normal1", "Car_Normal2", "Car_SuvB", "Car_TaxiB", "Car_Van", "Car_Delivery", "Car_Suv", "Car_Hatch",
            "Car_Truck", "Car_Sedan", "Car_Taxi",
        };

        /// <summary>
        /// Ambiance de jour de course autour de la ligne de départ : barrières rouges et blanches devant la foule,
        /// drapeaux à damier, tours à banderole, tentes, tribunes couvertes et grands projecteurs.
        /// </summary>
        private static void CreateRaceDay(Theme th, Frame[] f, SplineContainer line, Transform root)
        {
            if (!ArtLibrary.HasModel("Kit_Tent")) return;
            float length = line.CalculateLength();
            float edge = th.Street ? HalfWidth + SidewalkWidth - 0.4f : HalfWidth + 1.3f;

            // Barrières alternées le long de la ligne droite, de part et d'autre.
            foreach (float side in new[] { -1f, 1f })
            {
                int k = 0;
                for (float along = -46f; along < 28f; along += 1.45f, k++)
                {
                    if (Mathf.Abs(along) < 2.5f) continue; // passage des mâts du portique
                    Frame fr = FrameAtDistance(line, length, along);
                    Vector3 p = fr.Pos + fr.Right * side * edge;
                    if (!th.LandOk(p) || !Free(p, 0.35f)) continue;
                    Transform b = ArtLibrary.Spawn(k % 2 == 0 ? "Kit_BarrierRed" : "Kit_BarrierWhite", root);
                    if (b == null) continue;
                    b.SetPositionAndRotation(OnGround(p), Quaternion.LookRotation(fr.Right * side));
                }
            }

            // Drapeaux et tours à banderole.
            (float along, string model, float side)[] poles =
            {
                (1.5f, "Kit_FlagCheckers", -1f), (1.5f, "Kit_FlagCheckers", 1f), (-14f, "Kit_BannerRed", -1f), (-14f, "Kit_BannerGreen", 1f),
                (16f, "Kit_BannerGreen", -1f), (16f, "Kit_BannerRed", 1f), (-30f, "Kit_FlagRed", 1f), (-30f, "Kit_FlagGreen", -1f),
            };
            foreach ((float along, string model, float side) in poles)
            {
                Frame fr = FrameAtDistance(line, length, along);
                Vector3 p = fr.Pos + fr.Right * side * (edge + 1.1f);
                if (th.LandOk(p) && Free(p, 0.8f)) Place(model, root, p, Quaternion.LookRotation(-fr.Right * side), 0.8f);
            }

            // Tentes et tribunes couvertes derrière la foule ; projecteurs aux extrémités.
            (float along, string model, float side, float dist, float radius)[] big =
            {
                (-40f, "Kit_TentLong", 1f, 11f, 5f), (-22f, "Kit_Tent", -1f, 10.5f, 3.5f), (-4f, "Kit_TentClosed", 1f, 10.5f, 3.5f),
                (20f, "Kit_Tent", -1f, 10.5f, 3.5f), (-58f, "Kit_Grandstand", -1f, 14f, 6f), (36f, "Kit_GrandstandRound", 1f, 15f, 7f),
                (-50f, "Kit_LightPost", 1f, 6f, 1f), (30f, "Kit_LightPost", -1f, 6f, 1f),
            };
            foreach ((float along, string model, float side, float dist, float radius) in big)
            {
                Frame fr = FrameAtDistance(line, length, along);
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + dist);
                if (!th.LandOk(p) || MinDistanceToTrack(f, p) < HalfWidth + dist - 2f || !Free(p, radius)) continue;
                Transform t = Place(model, root, p, Quaternion.LookRotation(-fr.Right * side), radius);
                if (t != null && model.StartsWith("Kit_Tent")) SpawnPerson(root, OnGround(p + fr.Fwd * 1.2f), Quaternion.LookRotation(-fr.Right * side), false);
            }
        }

        private static void PlaceParkedVehicles(Theme th, Frame[] f, Transform root)
        {
            int placed = 0;
            for (int attempt = 0; attempt < th.Parked * 20 && placed < th.Parked; attempt++)
            {
                int i = UnityEngine.Random.Range(Samples / 14, Samples - Samples / 14);
                Frame fr = f[i];
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                float roll = UnityEngine.Random.value;
                bool taptap = roll < 0.35f;
                bool car = roll >= 0.55f && ArtLibrary.HasModel(ParkedCars[0]);
                Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + (taptap ? 6.5f : car ? 6f : 5.2f));
                float r = taptap ? 2.6f : car ? 2.5f : 1.5f;
                if (!th.LandOk(p) || MinDistanceToTrack(f, p) < HalfWidth + 4.5f || !Free(p, r)) continue;
                Vector3 flat = new Vector3(fr.Fwd.x, 0f, fr.Fwd.z).normalized;
                Quaternion rot = Quaternion.LookRotation(flat * (UnityEngine.Random.value < 0.5f ? 1f : -1f)) *
                                 Quaternion.Euler(0f, UnityEngine.Random.Range(-8f, 8f), 0f);
                if (car) Place(ParkedCars[UnityEngine.Random.Range(0, ParkedCars.Length)], root, p, rot, r);
                else if (taptap) ParkVehicle(root, "TapTap", 3.4f, p, rot, UnityEngine.Random.value < 0.5f ? "TapTap_Side_A.png" : "TapTap_Side_B.png");
                else ParkVehicle(root, "MotoTaxi", 2.1f, p, rot);
                Occupied.Add((p, r));
                placed++;
            }
        }

        private static void ParkVehicle(Transform root, string model, float length, Vector3 pos, Quaternion rot, string sideArt = null)
        {
            var holder = new GameObject("Pake " + model).transform;
            holder.SetParent(root, false);
            holder.SetPositionAndRotation(OnGround(pos), rot);
            Dictionary<string, Material> paint = null;
            if (sideArt != null) paint = new Dictionary<string, Material> { ["TT_SideArt"] = ArtLibrary.Get("TT_SideArt", null, sideArt) };
            Transform vehicle = ArtLibrary.Spawn(model, holder, paint);
            if (vehicle != null) ArtLibrary.FitLength(vehicle, length);
        }

        /// <summary>Clôtures en bois le long de la route, par tronçons.</summary>
        private static void PlaceFences(Theme th, Frame[] f, Transform root)
        {
            float step = Vector3.Distance(f[0].Pos, f[1].Pos) + 0.001f;
            for (int run = 0; run < th.Fences; run++)
            {
                int start = UnityEngine.Random.Range(0, f.Length);
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                float offset = HalfWidth + UnityEngine.Random.Range(7f, 14f);
                int segments = UnityEngine.Random.Range(3, 6);
                for (int k = 0; k < segments; k++)
                {
                    Frame fr = f[(start + Mathf.RoundToInt(k * 4f / step)) % f.Length];
                    Vector3 p = fr.Pos + fr.Right * side * offset;
                    if (!th.LandOk(p) || MinDistanceToTrack(f, p) < offset - 1f || !Free(p, 1.9f)) break;
                    Place("Fence", root, p, Quaternion.LookRotation(-fr.Right * side), 1.9f);
                }
            }
        }
    }
}
