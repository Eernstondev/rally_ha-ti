using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Circuit « rue haïtienne » (Katye) : chaussée pavée, trottoirs à bordure peinte, rangées continues de maisons
    /// gingerbread à galerie, lampadaires, fûts, caisses, passants et portique damier sur la ligne de départ.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const float SidewalkWidth = 3.2f;
        private const float SidewalkHeight = 0.16f;

        /// <summary>Façades de rue : modèle, largeur (m), avancée devant le centre (galerie et marches), profondeur arrière.</summary>
        private static readonly (string model, float width, float front, float back)[] StreetHouses =
        {
            ("Kay_Galeri", 7.6f, 6.2f, 3.1f),
            ("Kay_Etaj", 8.6f, 5.4f, 3.6f),
            ("Kay_Galeri", 7.6f, 6.2f, 3.1f),
            ("House_B", 6.4f, 4.5f, 3.1f),
            ("Boutik", 5.4f, 2.7f, 2.6f),
            ("Kay_Etaj", 8.6f, 5.4f, 3.6f),
        };

        private static readonly Color[] ShutterColors =
        {
            new(0.12f, 0.45f, 0.36f), new(0.16f, 0.36f, 0.62f), new(0.75f, 0.22f, 0.2f), new(0.9f, 0.62f, 0.15f),
            new(0.35f, 0.2f, 0.45f),
        };

        private static readonly Color[] DrumColors =
        {
            new(0.15f, 0.32f, 0.7f), new(0.75f, 0.18f, 0.15f), new(0.2f, 0.5f, 0.3f), new(0.85f, 0.6f, 0.12f),
        };

        /// <summary>Chaussée pavée et trottoirs surélevés (avec leurs bordures verticales) des deux côtés.</summary>
        private static void CreateStreet(Frame[] f)
        {
            CreateFlatStrip("Pave", f, -HalfWidth, HalfWidth, 0.02f, 2.8f, ArtLibrary.Get("Track_Pave"), true);
            Material walk = ArtLibrary.Get("Trotwa");
            Material curb = ArtLibrary.Get("Concrete_Slab");
            foreach (float side in new[] { -1f, 1f })
            {
                float a = side * HalfWidth, b = side * (HalfWidth + SidewalkWidth);
                CreateFlatStrip(side < 0 ? "TrotwaGoch" : "TrotwaDwat", f, Mathf.Min(a, b), Mathf.Max(a, b), SidewalkHeight, 6f, walk, true, true);
                CreateSkirt("Bodi", f, a, -0.3f, SidewalkHeight, curb);
                CreateSkirt("BodiDeye", f, b, -0.6f, SidewalkHeight, curb);
            }
        }

        /// <summary>Bande verticale (bordure de trottoir) le long du tracé, visible des deux côtés.</summary>
        private static void CreateSkirt(string name, Frame[] f, float lateral, float yLow, float yHigh, Material mat)
        {
            int n = f.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[n * 12];
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist += Vector3.Distance(f[i].Pos, f[i - 1].Pos);
                Vector3 p = f[i].Pos + f[i].Right * lateral;
                verts[i * 2] = p + Vector3.up * yLow;
                verts[i * 2 + 1] = p + Vector3.up * yHigh;
                uvs[i * 2] = new Vector2(dist / 2f, 0f);
                uvs[i * 2 + 1] = new Vector2(dist / 2f, 0.2f);
                int j = (i + 1) % n, t = i * 12;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = j * 2;
                tris[t + 3] = j * 2; tris[t + 4] = i * 2 + 1; tris[t + 5] = j * 2 + 1;
                tris[t + 6] = j * 2; tris[t + 7] = i * 2 + 1; tris[t + 8] = i * 2;
                tris[t + 9] = j * 2 + 1; tris[t + 10] = i * 2 + 1; tris[t + 11] = j * 2;
            }
            CreateMeshObject(name, verts, uvs, tris, mat, false).isStatic = true;
        }

        /// <summary>Rangées continues de maisons le long des trottoirs, avec mobilier de rue et passants.</summary>
        private static void PlaceStreetRows(Theme th, Frame[] f, Transform root)
        {
            var rows = new GameObject("Lari").transform;
            rows.SetParent(root, false);
            Material slab = ArtLibrary.Get("Concrete_Slab");
            foreach (float side in new[] { -1f, 1f })
            {
                float travelled = 0f, nextHouse = Random.Range(0f, 6f), nextLamp = 8f, nextProp = 5f;
                for (int i = 1; i < f.Length; i++)
                {
                    travelled += Vector3.Distance(f[i].Pos, f[i - 1].Pos);
                    Frame fr = f[i];
                    Quaternion facing = Quaternion.LookRotation(-fr.Right * side);

                    if (travelled >= nextLamp)
                    {
                        nextLamp = travelled + 24f;
                        Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + 0.7f);
                        p.y = fr.Pos.y + SidewalkHeight;
                        if (Free(p, 0.5f) && StreetSpot(f, p, HalfWidth + 0.6f))
                        {
                            Transform lamp = ArtLibrary.Spawn("Lanp", rows);
                            if (lamp != null)
                            {
                                lamp.SetPositionAndRotation(p, facing);
                                Occupied.Add((p, 0.5f));
                            }
                        }
                    }

                    if (travelled >= nextProp)
                    {
                        nextProp = travelled + Random.Range(7f, 16f);
                        Vector3 p = fr.Pos + fr.Right * side * (HalfWidth + SidewalkWidth - 0.7f) + fr.Fwd * Random.Range(-1f, 1f);
                        p.y = fr.Pos.y + SidewalkHeight;
                        if (Free(p, 0.9f) && StreetSpot(f, p, HalfWidth + SidewalkWidth - 0.9f))
                            StreetProp(rows, p, facing * Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f));
                    }

                    if (travelled < nextHouse) continue;
                    var kind = StreetHouses[Random.Range(0, StreetHouses.Length)];
                    float dist = HalfWidth + SidewalkWidth + 0.2f + kind.front;
                    Vector3 c = fr.Pos + fr.Right * side * dist;
                    float radius = Mathf.Max(kind.width, kind.front + kind.back) * 0.5f;
                    // Intérieur des virages serrés : la maison toucherait la piste, on laisse une ruelle.
                    if (!th.LandOk(c) || MinDistanceToTrack(f, c) < dist - 0.6f || !Free(c, radius * 0.9f))
                    {
                        nextHouse = travelled + 2f;
                        continue;
                    }
                    var overrides = RandomWalls();
                    overrides["Shutter_Paint"] = ArtLibrary.Get("Shutter_Paint", ShutterColors[Random.Range(0, ShutterColors.Length)]);
                    Transform house = ArtLibrary.Spawn(kind.model, rows, overrides);
                    if (house == null) continue;
                    // Au niveau du trottoir (pas du relief) : la rue reste à plat devant les façades.
                    house.SetPositionAndRotation(new Vector3(c.x, fr.Pos.y + SidewalkHeight, c.z), facing);
                    Occupied.Add((c, radius));
                    // Socle : masque le vide si le terrain descend derrière le trottoir.
                    Prim(PrimitiveType.Cube, "Soubasman", house, new Vector3(0f, -0.9f, 0f),
                        new Vector3(kind.width - 0.2f, 1.8f, (kind.front + kind.back) * 0.8f), slab, Quaternion.identity);
                    float gap = Random.value < 0.15f ? Random.Range(3f, 6f) : Random.Range(0.3f, 1.2f);
                    nextHouse = travelled + kind.width + gap;
                }
            }
            foreach (Transform t in rows.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        /// <summary>Vrai si le point reste au bord de la rue (pas rapproché de la piste par un virage).</summary>
        private static bool StreetSpot(Frame[] f, Vector3 p, float minDistance) => MinDistanceToTrack(f, p) >= minDistance - 0.3f;

        private static void StreetProp(Transform parent, Vector3 p, Quaternion rot)
        {
            float roll = Random.value;
            if (roll < 0.4f)
            {
                var paint = new Dictionary<string, Material>
                {
                    ["Drum_Paint"] = ArtLibrary.Get("Drum_Paint", DrumColors[Random.Range(0, DrumColors.Length)]),
                };
                Transform t = ArtLibrary.Spawn("Dwoum", parent, paint);
                if (t != null) t.SetPositionAndRotation(p, rot);
            }
            else if (roll < 0.7f)
            {
                Transform t = ArtLibrary.Spawn("Kes", parent);
                if (t != null) t.SetPositionAndRotation(p, rot);
            }
            else
            {
                // Passant sur le trottoir, tourné vers la course.
                SpawnPerson(parent, p, rot, Random.value < 0.4f);
            }
            Occupied.Add((p, 0.9f));
        }

        /// <summary>Portique damier au-dessus de la ligne de départ, mâts sur les trottoirs.</summary>
        private static void CreateCheckerGate(SplineContainer line)
        {
            Frame f = FrameAt(line, 0f);
            Transform gate = ArtLibrary.Spawn("BanyeDamye", null);
            if (gate == null) return;
            gate.name = "PotayDamye";
            gate.SetPositionAndRotation(f.Pos, Quaternion.LookRotation(new Vector3(f.Fwd.x, 0f, f.Fwd.z)));
            Occupied.Add((f.Pos + f.Right * 9f, 1f));
            Occupied.Add((f.Pos - f.Right * 9f, 1f));
        }
    }
}
