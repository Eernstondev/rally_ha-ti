using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Matériaux et modèles de Assets/_Project/Art. Chaque matériau des FBX (nommé dans Blender)
    /// est remplacé par un matériau Unity configuré ici : texture, relief, brillance, métal, émission.
    /// </summary>
    public static class ArtLibrary
    {
        public const string ArtRoot = "Assets/_Project/Art";
        private const string MatDir = ArtRoot + "/Materials";

        private readonly struct Def
        {
            public readonly Color Color;
            public readonly float Smooth, Metal, Emission;
            public readonly string Tex, Normal;
            public readonly bool Cutout, Transparent, Unlit;

            public Def(Color color, float smooth, float metal = 0f, string tex = null, string normal = null,
                float emission = 0f, bool cutout = false, bool transparent = false, bool unlit = false)
            {
                Color = color; Smooth = smooth; Metal = metal; Tex = tex; Normal = normal; Emission = emission;
                Cutout = cutout; Transparent = transparent; Unlit = unlit;
            }
        }

        private static Color C(float r, float g, float b) => new(r, g, b);

        private static readonly Dictionary<string, Def> Defs = new()
        {
            // Véhicules
            ["Chrome"] = new(C(0.92f, 0.92f, 0.94f), 0.88f, 1f),
            ["DarkMetal"] = new(C(0.11f, 0.11f, 0.12f), 0.45f, 0.7f),
            ["Glass"] = new(C(0.06f, 0.09f, 0.12f), 0.96f),
            ["Tire"] = new(C(0.045f, 0.045f, 0.05f), 0.15f),
            ["Rim"] = new(C(0.78f, 0.78f, 0.8f), 0.78f, 1f),
            ["Headlight"] = new(C(1f, 0.96f, 0.82f), 0.9f, 0f, emission: 2f),
            ["Taillight"] = new(C(0.92f, 0.1f, 0.08f), 0.75f, 0f, emission: 1.2f),
            ["SeatLeather"] = new(C(0.07f, 0.07f, 0.08f), 0.45f),
            ["TT_Cab"] = new(C(0.95f, 0.93f, 0.88f), 0.72f, 0.1f),
            ["TT_Stripe"] = new(C(0.85f, 0.14f, 0.2f), 0.65f),
            ["TT_Roof"] = new(C(0.96f, 0.74f, 0.14f), 0.6f),
            ["TT_SideArt"] = new(Color.white, 0.55f, tex: "TapTap_Side_A.png"),
            ["TT_RearArt"] = new(Color.white, 0.55f, tex: "TapTap_Rear.png"),
            ["TT_Sign"] = new(Color.white, 0.6f, tex: "TapTap_Sign.png"),
            ["Bag_Blue"] = new(C(0.2f, 0.35f, 0.7f), 0.1f),
            ["Bag_Red"] = new(C(0.75f, 0.15f, 0.15f), 0.1f),
            ["Sack"] = new(C(0.76f, 0.66f, 0.46f), 0.05f),
            ["Mattress"] = new(C(0.85f, 0.85f, 0.9f), 0.05f),
            ["Moto_Paint"] = new(C(0.82f, 0.12f, 0.14f), 0.7f, 0.2f),
            ["Moto_SeatCover"] = new(C(0.95f, 0.76f, 0.12f), 0.3f),
            ["Plate"] = new(C(0.95f, 0.95f, 0.92f), 0.5f),
            ["Buggy_Paint"] = new(C(0.1f, 0.66f, 0.62f), 0.7f, 0.2f),
            ["Frame"] = new(C(0.14f, 0.14f, 0.15f), 0.6f, 0.8f),
            // Pilotes
            ["Drv_Shirt"] = new(C(0.95f, 0.95f, 0.95f), 0.2f),
            ["Drv_Pants"] = new(C(0.16f, 0.22f, 0.38f), 0.15f),
            ["Drv_Skin"] = new(C(0.36f, 0.22f, 0.14f), 0.45f),
            ["Drv_Helmet"] = new(C(0.95f, 0.95f, 0.95f), 0.8f, 0.1f),
            ["Drv_Visor"] = new(C(0.05f, 0.06f, 0.08f), 0.97f),
            ["Drv_Shoes"] = new(C(0.9f, 0.9f, 0.9f), 0.4f),
            // Décor
            ["Wood"] = new(Color.white, 0.2f, tex: "Wood_Planks.png", normal: "Wood_Planks_N.png"),
            ["Wall_Paint"] = new(Color.white, 0.1f, tex: "Wall_Pink.png", normal: "Wall_N.png"),
            ["Concrete_Blocks"] = new(Color.white, 0.08f, tex: "Concrete_Blocks.png", normal: "Concrete_Blocks_N.png"),
            ["Concrete_Slab"] = new(C(0.62f, 0.6f, 0.56f), 0.1f, normal: "Wall_N.png"),
            ["Rebar"] = new(C(0.42f, 0.24f, 0.14f), 0.25f, 0.6f),
            ["WaterTank"] = new(C(0.05f, 0.05f, 0.06f), 0.55f),
            ["Door_Metal"] = new(C(0.14f, 0.34f, 0.55f), 0.5f, 0.4f),
            ["Iron"] = new(C(0.08f, 0.08f, 0.09f), 0.4f, 0.7f),
            ["Window_Dark"] = new(C(0.035f, 0.04f, 0.05f), 0.75f),
            ["Tin_Rust"] = new(Color.white, 0.4f, 0.5f, "Tin_Rust.png", "Tin_Rust_N.png"),
            ["Market_Cloth"] = new(Color.white, 0.1f, tex: "Market_Cloth.png"),
            ["Market_Produce"] = new(Color.white, 0.2f, tex: "Market_Produce.png"),
            ["Palm_Bark"] = new(Color.white, 0.1f, tex: "Palm_Bark.png", normal: "Palm_Bark_N.png"),
            ["Palm_Frond"] = new(Color.white, 0.3f, tex: "Palm_Frond.png", cutout: true),
            ["Coconut"] = new(C(0.36f, 0.42f, 0.13f), 0.45f),
            ["Paint_Red"] = new(C(0.82f, 0.12f, 0.14f), 0.5f),
            ["Paint_White"] = new(C(0.93f, 0.92f, 0.88f), 0.5f),
            ["Sign_Boutik"] = new(Color.white, 0.4f, tex: "Sign_Boutik.png"),
            ["Goods_0"] = new(C(0.9f, 0.7f, 0.2f), 0.3f),
            ["Goods_1"] = new(C(0.2f, 0.5f, 0.8f), 0.3f),
            ["Goods_2"] = new(C(0.85f, 0.2f, 0.2f), 0.3f),
            ["Goods_3"] = new(C(0.95f, 0.95f, 0.9f), 0.3f),
            ["Transformer"] = new(C(0.5f, 0.52f, 0.5f), 0.5f, 0.6f),
            // Circuit
            ["Asphalt"] = new(Color.white, 0.25f, tex: "Asphalt.png", normal: "Asphalt_N.png"),
            ["Track_Sand"] = new(Color.white, 0.12f, tex: "Track_Sand.png", normal: "Track_Sand_N.png"),
            ["Track_Asphalt"] = new(Color.white, 0.22f, tex: "Track_Asphalt.png", normal: "Track_Asphalt_N.png"),
            ["Skid"] = new(new Color(1f, 1f, 1f, 0.55f), 0f, tex: "Skid.png", transparent: true, unlit: true),
            ["Sand"] = new(Color.white, 0.05f, tex: "Sand.png", normal: "Sand_N.png"),
            ["Kerb"] = new(Color.white, 0.35f, tex: "Kerb.png"),
            // Rue haïtienne (Katye) : pavés, trottoirs, maisons gingerbread, mobilier.
            ["Track_Pave"] = new(Color.white, 0.18f, tex: "Track_Pave.png", normal: "Track_Pave_N.png"),
            ["Trotwa"] = new(Color.white, 0.12f, tex: "Trotwa.png"),
            ["Shutter_Paint"] = new(C(0.12f, 0.45f, 0.36f), 0.45f),
            ["Roof_Paint"] = new(C(0.7f, 0.16f, 0.14f), 0.4f, 0.3f),
            ["Lamp_Glow"] = new(C(1f, 0.82f, 0.5f), 0.5f, 0f, emission: 3f),
            ["Drum_Paint"] = new(C(0.15f, 0.32f, 0.7f), 0.45f, 0.5f),
            ["Checker"] = new(Color.white, 0.2f, tex: "Checker.png"),
            ["Boost_Pad"] = new(Color.white, 0.6f, tex: "Boost_Pad.png", emission: 1.4f),
            ["Morne"] = new(Color.white, 0.05f, tex: "Hill.png", normal: "Sand_N.png"),
            ["Sign_Labadi"] = new(Color.white, 0.4f, tex: "Sign_Labadi.png"),
            ["Pennants"] = new(Color.white, 0.3f, tex: "Pennants.png", cutout: true),
            // Végétation et entourage
            ["Leaves"] = new(Color.white, 0.18f, tex: "Leaves.png", normal: "Leaves_N.png"),
            ["Leaves_Dark"] = new(Color.white, 0.18f, tex: "Leaves_Dark.png", normal: "Leaves_Dark_N.png"),
            ["Bougainvillea"] = new(Color.white, 0.2f, tex: "Bougainvillea.png", normal: "Bougainvillea_N.png"),
            ["Banana_Leaf"] = new(Color.white, 0.35f, tex: "Banana_Leaf.png", cutout: true),
            ["Banana_Stem"] = new(C(0.45f, 0.55f, 0.28f), 0.25f),
            ["Bark_Dark"] = new(C(0.25f, 0.18f, 0.12f), 0.1f),
            ["Grass_Tuft"] = new(Color.white, 0.1f, tex: "Grass_Tuft.png", cutout: true),
            ["Rock"] = new(C(0.55f, 0.51f, 0.45f), 0.12f, normal: "Sand_N.png"),
            ["Ppl_Shirt"] = new(C(0.95f, 0.95f, 0.95f), 0.12f),
            ["Ppl_Pants"] = new(C(0.2f, 0.25f, 0.4f), 0.12f),
            ["Ppl_Hair"] = new(C(0.05f, 0.04f, 0.04f), 0.25f),
            // Accessoires Kenney (CC0) : tentes, tribunes, barrières, drapeaux, voitures garées.
            ["K_grey"] = new(C(0.93f, 0.93f, 0.92f), 0.3f),
            ["K_red"] = new(C(0.86f, 0.17f, 0.2f), 0.35f),
            ["K_grass"] = new(C(0.22f, 0.66f, 0.42f), 0.3f),
            ["K_road"] = new(C(0.28f, 0.28f, 0.3f), 0.2f),
            ["K_glass"] = new(C(0.45f, 0.62f, 0.8f), 0.8f),
            ["K_pylon"] = new(C(0.98f, 0.55f, 0.12f), 0.35f),
            ["K_defaultMat"] = new(C(0.82f, 0.82f, 0.8f), 0.3f),
            ["K_checkers"] = new(Color.white, 0.2f, tex: "Checker.png"),
            ["K_colormap"] = new(Color.white, 0.45f, tex: "Kenney_Colormap.png"),
            // Voitures Quaternius (CC0).
            ["QC_Yellow"] = new(C(0.96f, 0.74f, 0.12f), 0.55f, 0.1f),
            ["QC_Blue"] = new(C(0.18f, 0.42f, 0.85f), 0.55f, 0.1f),
            ["QC_LightBlue"] = new(C(0.5f, 0.74f, 0.95f), 0.55f, 0.1f),
            ["QC_White"] = new(C(0.93f, 0.93f, 0.92f), 0.55f, 0.1f),
            ["QC_Material"] = new(C(0.82f, 0.16f, 0.18f), 0.55f, 0.1f),
            ["QC_DarkOrange"] = new(C(0.88f, 0.42f, 0.1f), 0.4f),
            ["QC_Black"] = new(C(0.05f, 0.05f, 0.06f), 0.3f),
            ["QC_Grey"] = new(C(0.35f, 0.35f, 0.37f), 0.4f, 0.4f),
            ["QC_Windows"] = new(C(0.08f, 0.11f, 0.14f), 0.92f),
            ["QC_Headlights"] = new(C(1f, 0.95f, 0.82f), 0.8f, 0f, emission: 1.2f),
            ["QC_TailLights"] = new(C(0.85f, 0.1f, 0.08f), 0.7f, 0f, emission: 0.8f),
            // Personnages Quaternius (CC0) : pilotes et passants, teintés à la pose (peau, vêtements).
            ["Q_Skin"] = new(C(0.36f, 0.2f, 0.12f), 0.35f),
            ["Q_Face"] = new(C(0.97f, 0.96f, 0.93f), 0.5f),
            ["Q_Hair"] = new(C(0.06f, 0.045f, 0.04f), 0.3f),
            ["Q_Shirt"] = new(C(0.95f, 0.95f, 0.95f), 0.15f),
            ["Q_Pants"] = new(C(0.16f, 0.22f, 0.38f), 0.12f),
            ["Q_Belt"] = new(C(0.22f, 0.13f, 0.07f), 0.3f),
            ["Q_Hat"] = new(C(0.85f, 0.75f, 0.5f), 0.2f),
            ["Q_Detail"] = new(C(0.75f, 0.2f, 0.2f), 0.3f),
            ["Q_Details"] = new(C(0.85f, 0.7f, 0.2f), 0.4f),
            ["Q_Black"] = new(C(0.06f, 0.06f, 0.07f), 0.3f),
            ["Q_Brown"] = new(C(0.3f, 0.18f, 0.1f), 0.3f),
            ["Q_Main"] = new(C(0.95f, 0.95f, 0.95f), 0.15f),
            ["Q_Vest"] = new(C(0.95f, 0.55f, 0.1f), 0.2f),
            ["Q_Band"] = new(C(0.85f, 0.2f, 0.2f), 0.2f),
            ["Q_Clothes"] = new(C(0.95f, 0.95f, 0.95f), 0.15f),
            ["Q_DarkClothes"] = new(C(0.15f, 0.15f, 0.18f), 0.15f),
            ["Billboard_Art"] = new(Color.white, 0.45f, tex: "Billboard_Kafe.png"),
            ["Bleacher_Paint"] = new(C(0.12f, 0.35f, 0.65f), 0.45f),
            // Mer
            ["Water"] = new(Color.white, 0.94f, tex: "Water_Gradient.png", normal: "Water_N.png"),
            ["Foam"] = new(Color.white, 0f, tex: "Foam.png", transparent: true, unlit: true),
            ["Boat_Hull"] = new(Color.white, 0.5f, tex: "Boat_Hull.png"),
            ["Boat_Trim"] = new(C(0.85f, 0.15f, 0.2f), 0.5f),
            ["Boat_Net"] = new(C(0.2f, 0.4f, 0.55f), 0.05f),
            // Bonus
            ["Item_Crate"] = new(Color.white, 0.45f, tex: "Item_Crate.png", emission: 0.15f),
            ["Coconut_Husk"] = new(Color.white, 0.3f, tex: "Coconut_Husk.png"),
            ["Coconut_Shell"] = new(C(0.33f, 0.21f, 0.11f), 0.4f),
            ["Mango_Skin"] = new(Color.white, 0.65f, tex: "Mango_Skin.png"),
            ["Mango_Stem"] = new(C(0.3f, 0.22f, 0.1f), 0.3f),
            ["Mango_Leaf"] = new(C(0.15f, 0.45f, 0.15f), 0.5f),
            ["Coin_Gold"] = new(C(1f, 0.78f, 0.2f), 0.75f, 1f, emission: 0.25f),
            ["Coin_Rim"] = new(C(0.95f, 0.62f, 0.12f), 0.7f, 1f, emission: 0.2f),
            ["Banana_Peel"] = new(C(0.98f, 0.82f, 0.2f), 0.55f),
            ["Banana_Inner"] = new(C(0.98f, 0.93f, 0.62f), 0.3f),
            ["Banana_Tip"] = new(C(0.35f, 0.25f, 0.1f), 0.3f),
            ["Rocket_Body"] = new(C(0.85f, 0.12f, 0.12f), 0.7f, 0.3f),
            ["Rocket_Fin"] = new(C(0.15f, 0.35f, 0.8f), 0.6f),
            ["Rocket_Flame"] = new(C(1f, 0.55f, 0.1f), 0.2f, emission: 4f),
            // Effets (couleur réglée à l'exécution)
            ["Fx_Wave"] = new(Color.white, 0f, transparent: true, unlit: true),
            ["Fx_Shield"] = new(Color.white, 0.9f, transparent: true),
            ["Fx_Bolt"] = new(C(1f, 0.95f, 0.6f), 0f, unlit: true),
        };

        private static float _yawFix = float.NaN;

        /// <summary>Matériau configuré. tint remplace la couleur, texture remplace l'image (variantes de murs, de tap-taps).</summary>
        public static Material Get(string name, Color? tint = null, string texture = null)
        {
            EnsureFolder();
            if (!Defs.TryGetValue(name, out Def def)) def = new Def(Color.gray, 0.3f);

            string key = name;
            if (tint.HasValue) key += "_" + ColorUtility.ToHtmlStringRGB(tint.Value);
            if (texture != null) key += "_" + System.IO.Path.GetFileNameWithoutExtension(texture);
            string path = $"{MatDir}/{key}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            Configure(mat, def, tint, texture ?? def.Tex);
            return mat;
        }

        public static Shader LitShader() =>
            GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");

        private static Shader UnlitShader() =>
            GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Transparent");

        public static Sprite LoadSprite(string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Textures/{file}");

        private static void Configure(Material mat, Def def, Color? tint, string texture)
        {
            mat.shader = def.Unlit ? UnlitShader() : LitShader();
            mat.enableInstancing = true;
            Texture2D tex = texture != null ? LoadTexture(texture) : null;
            Color baseColor = tex != null ? tint ?? Color.white : tint ?? def.Color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (tex != null) mat.mainTexture = tex;
            if (def.Transparent) MakeTransparent(mat);

            if (def.Normal != null)
            {
                Texture2D normal = LoadTexture(def.Normal);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", def.Smooth);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", def.Smooth);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", def.Metal);

            if (def.Emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", baseColor * def.Emission);
                if (tex != null) mat.SetTexture("_EmissionMap", tex);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            if (def.Cutout)
            {
                mat.SetFloat("_Cutoff", 0.45f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f);
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            }
            EditorUtility.SetDirty(mat);
        }

        private static void MakeTransparent(Material mat)
        {
            void Set(string prop, float v)
            {
                if (mat.HasProperty(prop)) mat.SetFloat(prop, v);
            }
            Set("_Surface", 1f);
            Set("_Blend", 0f);
            Set("_SrcBlend", (float)BlendMode.SrcAlpha);
            Set("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            Set("_SrcBlendAlpha", (float)BlendMode.One);
            Set("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            Set("_ZWrite", 0f);
            Set("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Texture2D LoadTexture(string file)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Textures/{file}");
            if (tex == null) Debug.LogWarning($"Rally Ayiti : texture introuvable {file}");
            return tex;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(ArtRoot, "Materials");
        }

        // ---------- Modèles ----------

        public static bool HasModel(string model) => LoadModel(model) != null;

        private static GameObject LoadModel(string model) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Models/{model}.fbx");

        /// <summary>
        /// Instancie un modèle sous un conteneur, avant tourné vers +Z, matériaux remplacés.
        /// overrides : nom de matériau Blender → matériau à utiliser (couleurs de pilote, murs...).
        /// </summary>
        public static Transform Spawn(string model, Transform parent, Dictionary<string, Material> overrides = null)
        {
            GameObject asset = LoadModel(model);
            if (asset == null)
            {
                Debug.LogError($"Rally Ayiti : modèle introuvable {model}.fbx");
                return null;
            }
            var holder = new GameObject(model).transform;
            holder.SetParent(parent, false);
            var inst = (GameObject)Object.Instantiate(asset, holder);
            inst.name = model + "_Mesh";
            inst.transform.localRotation = Quaternion.Euler(0f, YawFix(), 0f) * inst.transform.localRotation;
            RemapMaterials(inst, overrides);
            return holder;
        }

        /// <summary>Rotation qui ramène l'avant des modèles (+Y dans Blender) vers +Z dans Unity, mesurée sur le tap-tap.</summary>
        private static float YawFix()
        {
            if (!float.IsNaN(_yawFix)) return _yawFix;
            _yawFix = 0f;
            GameObject asset = LoadModel("TapTap");
            if (asset == null) return _yawFix;
            var tmp = (GameObject)Object.Instantiate(asset);
            Transform front = FindDeep(tmp.transform, "Front");
            if (front != null)
            {
                Vector3 f = front.position - tmp.transform.position;
                _yawFix = -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            }
            Object.DestroyImmediate(tmp);
            return _yawFix;
        }

        private static void RemapMaterials(GameObject go, Dictionary<string, Material> overrides)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = CleanName(mats[i].name);
                    if (overrides != null && overrides.TryGetValue(n, out Material o)) mats[i] = o;
                    else if (Defs.ContainsKey(n)) mats[i] = Get(n);
                }
                r.sharedMaterials = mats;
            }
        }

        private static string CleanName(string n)
        {
            int dot = n.IndexOf('.');
            if (dot > 0) n = n.Substring(0, dot);
            return n.Replace(" (Instance)", "").Trim();
        }

        public static Transform FindDeep(Transform root, string prefix)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Transform t = queue.Dequeue();
                if (t.name.StartsWith(prefix)) return t;
                foreach (Transform c in t) queue.Enqueue(c);
            }
            return null;
        }

        public static List<Transform> FindAll(Transform root, string prefix)
        {
            var found = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix) && !t.name.Contains("_Mesh")) found.Add(t);
            return found;
        }

        /// <summary>Boîte englobante des meshes, exprimée dans l'espace du conteneur.</summary>
        public static Bounds LocalBounds(Transform holder)
        {
            bool any = false;
            var b = new Bounds();
            Matrix4x4 toHolder = holder.worldToLocalMatrix;
            foreach (MeshFilter mf in holder.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = toHolder * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? mb.min.x : mb.max.x,
                        (i & 2) == 0 ? mb.min.y : mb.max.y,
                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Met le modèle à la longueur voulue (axe Z), centré, posé sur le sol du parent.</summary>
        public static float FitLength(Transform holder, float targetLength)
        {
            Bounds lb = LocalBounds(holder);
            float scale = targetLength / Mathf.Max(0.01f, lb.size.z);
            holder.localScale = Vector3.one * scale;
            holder.localPosition = new Vector3(-lb.center.x * scale, -lb.min.y * scale, -lb.center.z * scale);
            return scale;
        }
    }
}
