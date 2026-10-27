using System.Collections.Generic;
using RallyAyiti.Audio;
using RallyAyiti.Kart;
using RallyAyiti.Race;
using RallyAyiti.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RallyAyiti.EditorTools
{
    /// <summary>Scène « Meni » (choix du pilote et du circuit) et sons communs aux scènes.</summary>
    public static partial class PrototypeBuilder
    {
        private const string MenuScenePath = Root + "/Scenes/Meni.unity";

        private static readonly string[] VehicleLabels = { "Moto taxi", "Tap-tap", "Moto taxi", "Buggy plaj", "Buggy plaj", "Tap-tap" };

        private static readonly string[] Taglines =
        {
            "Li fofile nan tout ti chemen.",
            "Tap-tap la lou, men li pa janm lage.",
            "Premye nan demaraj, chak fwa.",
            "Li kondui sou ritm konpa.",
            "Dous nan viraj, danjere nan liy dwat.",
            "Pitit katye a, li konn chak twou.",
        };

        [MenuItem("Rally Ayiti/Construire le menu")]
        private static void BuildMenuFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Rally Ayiti : arrête le mode Play avant de reconstruire le menu.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildMenuScene();
        }

        [MenuItem("Rally Ayiti/Tout construire (menu + circuits)")]
        private static void BuildEverything()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(Katye);
            Build(Labadie);
            BuildMenuScene();
        }

        private static void BuildMenuScene()
        {
            if (!ArtLibrary.HasModel("TapTap")) return;
            RenderSetup.EnsureUrp();
            FixTextureImports();
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Data");
            Occupied.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UnityEngine.Random.InitState(7);
            ConfigureQuality();
            CreateLighting();
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 260f;
            CreatePostFx();

            Transform turntable = CreateShowroom(out GameObject[] models);
            CreateMenuDecor();
            CreateMenuCamera();
            CreateGameAudio();
            CreateMenuUi(turntable, models);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Rally Ayiti : menu construit. Ouvre Assets/_Project/Scenes/Meni et appuie sur Play.");
        }

        // ---------- Décor du menu ----------

        private static Transform CreateShowroom(out GameObject[] models)
        {
            Material slab = ArtLibrary.Get("Concrete_Slab");
            Quad("Sol", -160f, 160f, -160f, 160f, 0f, 0f, 6f, ArtLibrary.Get("Sand"), false);
            Prim(PrimitiveType.Cylinder, "Plato", null, new Vector3(0f, 0.12f, 0f), new Vector3(7.4f, 0.12f, 7.4f), slab, Quaternion.identity);
            Prim(PrimitiveType.Cylinder, "Bodi", null, new Vector3(0f, 0.1f, 0f), new Vector3(7.9f, 0.1f, 7.9f), Mat("Plato_Bodi", Sun, 0.6f, 0.8f), Quaternion.identity);

            var turntable = new GameObject("PlatoKiVire").transform;
            turntable.position = new Vector3(0f, 0.245f, 0f);
            turntable.rotation = Quaternion.Euler(0f, -35f, 0f);
            Occupied.Add((Vector3.zero, 6f));

            models = new GameObject[Pilots.Length];
            for (int i = 0; i < Pilots.Length; i++)
            {
                var holder = new GameObject(Pilots[i].Name);
                holder.transform.SetParent(turntable, false);
                SpawnPilotModel(Pilots[i], holder.transform);
                holder.SetActive(i == PlayerPilot);
                models[i] = holder;
            }
            return turntable;
        }

        private static void CreateMenuDecor()
        {
            var root = new GameObject("Dekò").transform;
            Quaternion facing = Quaternion.LookRotation(Vector3.back);
            Place("House_B", root, new Vector3(-9f, 0f, 19f), facing * Quaternion.Euler(0f, 12f, 0f), 5f, RandomWalls());
            Place("Boutik", root, new Vector3(6.5f, 0f, 17f), facing * Quaternion.Euler(0f, -8f, 0f), 4.2f, RandomWalls());
            Place("House_A", root, new Vector3(20f, 0f, 24f), facing * Quaternion.Euler(0f, -20f, 0f), 5.5f, RandomWalls());
            Place("House_A", root, new Vector3(-24f, 0f, 27f), facing * Quaternion.Euler(0f, 18f, 0f), 5.5f, RandomWalls());

            foreach (Vector3 p in new[] { new Vector3(-14f, 0f, 9f), new Vector3(13f, 0f, 9f), new Vector3(-3f, 0f, 24f), new Vector3(28f, 0f, 14f), new Vector3(-30f, 0f, 14f) })
                PlacePalm(root, p);
            Place("Bougainvillea", root, new Vector3(-4.5f, 0f, 14.5f), Quaternion.Euler(0f, 40f, 0f), 2f);
            Place("Bougainvillea", root, new Vector3(11f, 0f, 14f), Quaternion.Euler(0f, 160f, 0f), 2f);
            Place("Tree_Mango", root, new Vector3(-20f, 0f, 12f), Quaternion.Euler(0f, 70f, 0f), 4f);
            Place("BananaPlant", root, new Vector3(2.5f, 0f, 21f), Quaternion.Euler(0f, 10f, 0f), 1.5f);
            Place("BananaPlant", root, new Vector3(-12f, 0f, 15f), Quaternion.Euler(0f, 100f, 0f), 1.5f);
            foreach (Vector3 p in new[] { new Vector3(-7f, 0f, 9.5f), new Vector3(8f, 0f, 10f), new Vector3(16f, 0f, 16f), new Vector3(-17f, 0f, 19f) })
                Place("Bush", root, p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), 1.4f);
            Transform board = Place("Billboard", root, new Vector3(-1f, 0f, 30f), facing, 3.5f,
                new Dictionary<string, Material> { ["Billboard_Art"] = ArtLibrary.Get("Billboard_Art", null, "Billboard_Rally.png") });
            if (board != null) board.localScale = Vector3.one * 1.3f;

            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(UnityEngine.Random.Range(-16f, 16f), 0f, UnityEngine.Random.Range(11f, 16f));
                if (!Free(p, 0.6f)) continue;
                if (SpawnPerson(root, p, Quaternion.LookRotation(-p.normalized), UnityEngine.Random.value < 0.5f) != null)
                    Occupied.Add((p, 0.6f));
            }
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(UnityEngine.Random.Range(-40f, 40f), 0f, UnityEngine.Random.Range(6f, 40f));
                if (p.magnitude < 8f || !Free(p, 0.4f)) continue;
                Transform g = ArtLibrary.Spawn("GrassTuft", root);
                if (g == null) continue;
                g.SetPositionAndRotation(p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                g.localScale = Vector3.one * UnityEngine.Random.Range(0.8f, 1.4f);
            }
        }

        private static void CreateMenuCamera()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 32f;
            cam.farClipPlane = 400f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.position = new Vector3(0f, 2.3f, -8.6f);
            camGo.transform.LookAt(new Vector3(0f, 0.95f, 0f));
        }

        // ---------- Interface du menu ----------

        private static void CreateMenuUi(Transform turntable, GameObject[] models)
        {
            Transform canvas = CreateCanvas("Meni");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font display = DisplayFont();

            var logoGo = new GameObject("Logo", typeof(RectTransform));
            logoGo.transform.SetParent(canvas, false);
            var logoRt = (RectTransform)logoGo.transform;
            logoRt.anchorMin = logoRt.anchorMax = logoRt.pivot = new Vector2(0f, 1f);
            logoRt.anchoredPosition = new Vector2(36f, -20f);
            logoRt.sizeDelta = new Vector2(560f, 224f);
            var logo = logoGo.AddComponent<Image>();
            logo.sprite = ArtLibrary.LoadSprite("UI_Logo.png");
            logo.preserveAspect = true;
            logo.raycastTarget = false;

            // Pilote
            RectTransform pilot = Panel(canvas, "Pilot", new Vector2(0f, 0.5f), new Vector2(40f, -100f), new Vector2(620f, 600f),
                new Color(Night.r, Night.g, Night.b, 0.78f), null);
            Label(pilot, "Tit", display, 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(560f, 40f), Sun).text = "CHWAZI PILÒT";
            Text name = Label(pilot, "Non", display, 52, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(580f, 70f), Paper);
            Text vehicle = Label(pilot, "Machin", font, 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(560f, 44f), Lagoon);
            Text tagline = Label(pilot, "Deviz", font, 27, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(560f, 40f),
                new Color(Paper.r, Paper.g, Paper.b, 0.85f));
            tagline.fontStyle = FontStyle.Italic;
            Text power = Label(pilot, "Pouvwa", font, 25, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(580f, 40f), Sun);

            (RectTransform speed, Button upSpeed, Text upSpeedText) = StatRow(pilot, "Vitès", font, -300f, Sun);
            (RectTransform accel, Button upAccel, Text upAccelText) = StatRow(pilot, "Akselerasyon", font, -352f, Lagoon);
            (RectTransform handling, Button upHandling, Text upHandlingText) = StatRow(pilot, "Kontwòl", font, -404f, Hibiscus);

            // Cagnotte de gourdes, en haut à droite.
            Text gourdes = Label(canvas, "Goud", display, 48, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-44f, -30f), new Vector2(420f, 70f), Sun);

            var arrowBg = new Color(1f, 1f, 1f, 0.12f);
            Button prev = UiButton(pilot, "Anvan", "<", display, new Vector2(0.5f, 0f), new Vector2(-190f, 34f), new Vector2(120f, 90f), arrowBg, Paper);
            Button next = UiButton(pilot, "Apre", ">", display, new Vector2(0.5f, 0f), new Vector2(190f, 34f), new Vector2(120f, 90f), arrowBg, Paper);
            Text counter = Label(pilot, "Kont", display, 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(200f, 60f), Paper);

            // Circuits
            RectTransform tracksPanel = Panel(canvas, "Wout", new Vector2(1f, 0.5f), new Vector2(-40f, 60f), new Vector2(480f, 760f),
                new Color(Night.r, Night.g, Night.b, 0.6f), null);
            Label(tracksPanel, "Tit", display, 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(440f, 40f), Sun).text = "CHWAZI WOUT";
            (string scene, string title, string card)[] trackDefs =
            {
                ("Labadie", "Plaj Labadi", "Card_Labadie.png"),
                ("Prototype", "Katye a", "Card_Katye.png"),
            };
            var cards = new (Button button, RectTransform card, Outline frame)[trackDefs.Length];
            for (int i = 0; i < trackDefs.Length; i++)
                cards[i] = TrackCard(tracksPanel, trackDefs[i].card, new Vector2(0f, -86f - i * 290f));
            Text trackTitle = Label(tracksPanel, "NonWout", display, 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(440f, 60f), Sun);

            Button go = UiButton(canvas, "Ale", "ALE !", display, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(480f, 130f), Sun, Night);
            go.GetComponentInChildren<Text>().fontSize = 64;

            Label(canvas, "Konsey", font, 24, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 40f),
                new Color(Paper.r, Paper.g, Paper.b, 0.75f)).text = "← →  pilòt      ↑ ↓  wout      Antre  ale";

            var menu = canvas.gameObject.AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            SerializedProperty list = so.FindProperty("pilots");
            list.arraySize = Pilots.Length;
            for (int i = 0; i < Pilots.Length; i++)
            {
                KartStats stats = StatsFor(Pilots[i].Model);
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = Pilots[i].Name;
                e.FindPropertyRelative("vehicle").stringValue = VehicleLabels[i];
                e.FindPropertyRelative("tagline").stringValue = Taglines[i];
                e.FindPropertyRelative("vehicleKey").stringValue = Pilots[i].Model;
                e.FindPropertyRelative("power").stringValue = $"POUVWA : {PilotPowers[i].name} — {PilotPowers[i].description}";
                e.FindPropertyRelative("speed").floatValue = Mathf.Lerp(0.18f, 1f, Mathf.InverseLerp(24f, 31f, stats.maxSpeed));
                e.FindPropertyRelative("acceleration").floatValue = Mathf.Lerp(0.18f, 1f, Mathf.InverseLerp(8f, 20f, stats.acceleration));
                e.FindPropertyRelative("handling").floatValue = Mathf.Lerp(0.18f, 1f,
                    (Mathf.InverseLerp(70f, 115f, stats.steering) + Mathf.InverseLerp(40f, 75f, stats.grip)) * 0.5f);
                e.FindPropertyRelative("model").objectReferenceValue = models[i];
            }
            SerializedProperty tracks = so.FindProperty("tracks");
            tracks.arraySize = trackDefs.Length;
            for (int i = 0; i < trackDefs.Length; i++)
            {
                SerializedProperty e = tracks.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("scene").stringValue = trackDefs[i].scene;
                e.FindPropertyRelative("title").stringValue = trackDefs[i].title;
                e.FindPropertyRelative("button").objectReferenceValue = cards[i].button;
                e.FindPropertyRelative("card").objectReferenceValue = cards[i].card;
                e.FindPropertyRelative("frame").objectReferenceValue = cards[i].frame;
            }
            so.FindProperty("defaultPilot").intValue = PlayerPilot;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetRef(menu, "pilotName", name);
            SetRef(menu, "vehicleName", vehicle);
            SetRef(menu, "tagline", tagline);
            SetRef(menu, "pilotCounter", counter);
            SetRef(menu, "trackTitle", trackTitle);
            SetRef(menu, "speedBar", speed);
            SetRef(menu, "accelBar", accel);
            SetRef(menu, "handlingBar", handling);
            SetRef(menu, "previousButton", prev);
            SetRef(menu, "nextButton", next);
            SetRef(menu, "goButton", go);
            SetRef(menu, "turntable", turntable);
            SetRef(menu, "music", Clip("Music_Menu_Twoubadou"));
            SetRef(menu, "gourdesText", gourdes);
            SetRef(menu, "powerText", power);
            SetRefArray(menu, "upgradeButtons", new Object[] { upSpeed, upAccel, upHandling });
            SetRefArray(menu, "upgradeLabels", new Object[] { upSpeedText, upAccelText, upHandlingText });
        }

        /// <summary>
        /// Une ligne de caractéristique : nom, barre, bouton d'amélioration du garage.
        /// Renvoie la partie remplie de la barre, le bouton et son texte (prix).
        /// </summary>
        private static (RectTransform, Button, Text) StatRow(Transform parent, string label, Font font, float y, Color color)
        {
            Label(parent, label, font, 27, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(36f, y), new Vector2(220f, 40f), Paper).text = label;
            RectTransform bar = Panel(parent, label + "Ba", new Vector2(0f, 1f), new Vector2(240f, y - 9f), new Vector2(230f, 22f),
                new Color(1f, 1f, 1f, 0.14f), null);
            Button up = UiButton(parent, label + "Plis", "+", font, new Vector2(0f, 1f), new Vector2(484f, y + 2f), new Vector2(110f, 44f), color, Night);
            Text price = up.GetComponentInChildren<Text>();
            price.fontSize = 22;
            var fillGo = new GameObject("Ranpli", typeof(RectTransform));
            fillGo.transform.SetParent(bar, false);
            var fill = (RectTransform)fillGo.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var img = fillGo.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return (fill, up, price);
        }

        private static (Button button, RectTransform card, Outline frame) TrackCard(Transform parent, string sprite, Vector2 pos)
        {
            var go = new GameObject("Kat", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(400f, 267f);
            var img = go.AddComponent<Image>();
            img.sprite = ArtLibrary.LoadSprite(sprite);
            img.preserveAspect = true;
            var frame = go.AddComponent<Outline>();
            frame.effectColor = Sun;
            frame.effectDistance = new Vector2(7f, -7f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return (button, rt, frame);
        }

        // ---------- Briques d'interface communes ----------

        private static Font DisplayFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ArtLibrary.ArtRoot + "/Fonts/Bungee-Regular.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Transform CreateCanvas(string name)
        {
            var canvasGo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return canvasGo.transform;
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Color? outline)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            if (outline is { } c)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = c;
                o.effectDistance = new Vector2(4f, -4f);
            }
            return rt;
        }

        private static Button UiButton(Transform parent, string name, string caption, Font font, Vector2 anchor, Vector2 pos,
            Vector2 size, Color background, Color textColor)
        {
            RectTransform rt = Panel(parent, name, anchor, pos, size, background, null);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            Text label = Label(rt, "Teks", font, Mathf.RoundToInt(size.y * 0.42f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
                Vector2.zero, size, textColor);
            label.text = caption;
            return button;
        }

        /// <summary>Spectateur habillé au hasard (chemise, pantalon, teint).</summary>
        private static Transform SpawnPerson(Transform parent, Vector3 pos, Quaternion rot, bool cheering)
        {
            Color[] shirts = { Sun, Hibiscus, Lagoon, Paper, Mango, Violet, Hex(0x2a5bd7), Green, Hex(0xd94141) };
            Color[] pants = { Hex(0x2e3a5c), Hex(0x232326), Hex(0x8c7350), Hex(0xd9d6cf), Hex(0x4a5a3a) };
            Color[] skins = { Hex(0x5c3a24), Hex(0x4a2e1c), Hex(0x6e4630), Hex(0x3b2416) };
            var outfit = new Dictionary<string, Material>
            {
                ["Ppl_Shirt"] = ArtLibrary.Get("Ppl_Shirt", shirts[UnityEngine.Random.Range(0, shirts.Length)]),
                ["Ppl_Pants"] = ArtLibrary.Get("Ppl_Pants", pants[UnityEngine.Random.Range(0, pants.Length)]),
                ["Drv_Skin"] = ArtLibrary.Get("Drv_Skin", skins[UnityEngine.Random.Range(0, skins.Length)]),
            };
            Transform t = ArtLibrary.Spawn(cheering ? "Person_Cheer" : "Person_Stand", parent, outfit);
            if (t == null) return null;
            t.SetPositionAndRotation(pos, rot * Quaternion.Euler(0f, UnityEngine.Random.Range(-25f, 25f), 0f));
            t.localScale = Vector3.one * UnityEngine.Random.Range(0.92f, 1.06f);
            return t;
        }

        // ---------- Sons communs ----------

        private static void CreateGameAudio()
        {
            var audio = new GameObject("Son").AddComponent<GameAudio>();
            string[] files =
            {
                "Item_Pickup", "Kokoye_Throw", "Kokoye_Hit", "Mango_Splat", "Boost", "Countdown_Beep", "Countdown_Go", "Finish_Fanfare", "UI_Click",
                "Coin", "Lanbi", "Thunder", "Rocket", "Shield", "Power",
            };
            SetRefArray(audio, "clips", System.Array.ConvertAll(files, f => (Object)Clip(f)));
        }

        private static void CreateRaceAudio(Theme th, RaceManager race)
        {
            CreateGameAudio();
            var music = new GameObject("MizikAkAnbyans").AddComponent<RaceAudio>();
            SetRef(music, "race", race);
            SetRef(music, "music", Clip("Music_Race_Konpa"));
            SetRef(music, "ambience", Clip(th.Ambience));
        }

        // ---------- Ciel ----------

        private static Material SkyMaterial()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            string path = $"{Root}/Materials/Syel.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_SunDisk", 2f);
            mat.SetFloat("_SunSize", 0.035f);
            mat.SetFloat("_SunSizeConvergence", 6f);
            mat.SetFloat("_AtmosphereThickness", 0.8f);
            mat.SetColor("_SkyTint", new Color(0.42f, 0.6f, 0.92f));
            mat.SetColor("_GroundColor", new Color(0.46f, 0.48f, 0.43f));
            mat.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
