using System.Collections.Generic;
using System.Linq;
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
            Crowd.Clear();
            _th = null;
            _hf = null;
            _streetFrames = null;

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
            CreateCrowdLife();

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
            Sprite panelSprite = ArtLibrary.LoadSprite("UI_Panel.png");
            Sprite starSprite = ArtLibrary.LoadSprite("UI_Star.png");
            Sprite lockSprite = ArtLibrary.LoadSprite("UI_Lock.png");
            Color blue = new(0.13f, 0.38f, 0.86f);
            Color deep = new(0.07f, 0.16f, 0.42f, 0.92f);

            // ---------- Accueil : grand logo, bouton JWE ----------
            RectTransform home = MenuScreen(canvas, "Akèy");
            Image logo = MenuImage(home, "Logo", ArtLibrary.LoadSprite("UI_Logo.png"), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 360f));
            logo.preserveAspect = true;
            Button play = GlossyButton(home, "Jwe", "JWE", display, new Vector2(1f, 0f), new Vector2(-70f, 70f), new Vector2(380f, 170f), Sun, Night, panelSprite);
            play.GetComponentInChildren<Text>().fontSize = 84;
            Label(home, "Konsey", font, 28, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 44f),
                new Color(Paper.r, Paper.g, Paper.b, 0.85f)).text = "Tape JWE pou kòmanse";

            // ---------- Centre : garage, course rapide, carrière ----------
            RectTransform hub = MenuScreen(canvas, "Sant");
            Button garageButton = GlossyButton(hub, "Garaj", "GARAJ", display, new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(330f, 200f), blue, Paper, panelSprite);
            Button quickButton = GlossyButton(hub, "KousRapid", "KOUS\nRAPID", display, new Vector2(0f, 0f), new Vector2(420f, 50f), new Vector2(330f, 200f), blue, Paper, panelSprite);
            Button careerButton = GlossyButton(hub, "Karye", "KARYÈ", display, new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(460f, 240f), Sun, Night, panelSprite);
            careerButton.GetComponentInChildren<Text>().fontSize = 80;
            foreach (Button b in new[] { garageButton, quickButton }) b.GetComponentInChildren<Text>().fontSize = 54;

            // ---------- Garage : pilote, caractéristiques, améliorations, pilote bloqué ----------
            RectTransform garage = MenuScreen(canvas, "Garaj");
            RectTransform pilot = Panel(garage, "Pilot", new Vector2(0f, 0.5f), new Vector2(40f, -40f), new Vector2(620f, 600f), deep, null);
            pilot.GetComponent<Image>().sprite = panelSprite;
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
            var arrowBg = new Color(1f, 1f, 1f, 0.12f);
            Button prev = UiButton(pilot, "Anvan", "<", display, new Vector2(0.5f, 0f), new Vector2(-190f, 34f), new Vector2(120f, 90f), arrowBg, Paper);
            Button next = UiButton(pilot, "Apre", ">", display, new Vector2(0.5f, 0f), new Vector2(190f, 34f), new Vector2(120f, 90f), arrowBg, Paper);
            Text counter = Label(pilot, "Kont", display, 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(200f, 60f), Paper);

            RectTransform lockBadge = Panel(garage, "Bloke", new Vector2(0.5f, 0f), new Vector2(130f, 50f), new Vector2(1000f, 100f),
                new Color(Hibiscus.r, Hibiscus.g, Hibiscus.b, 0.92f), null);
            lockBadge.GetComponent<Image>().sprite = panelSprite;
            MenuImage(lockBadge, "Kadna", lockSprite, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(76f, 76f));
            Text lockText = Label(lockBadge, "Teks", font, 28, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(870f, 90f), Paper);
            lockText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button buyPilot = GlossyButton(garage, "AchtePilot", $"ACHTE\n{RallyAyiti.Core.Career.PilotPrice} G", display, new Vector2(1f, 0f), new Vector2(-60f, 40f),
                new Vector2(330f, 140f), Sun, Night, panelSprite);
            buyPilot.GetComponentInChildren<Text>().fontSize = 40;

            // ---------- Course rapide : circuit, ALE ----------
            RectTransform quick = MenuScreen(canvas, "Rapid");
            RectTransform tracksPanel = Panel(quick, "Wout", new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(480f, 760f), deep, null);
            tracksPanel.GetComponent<Image>().sprite = panelSprite;
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
            Button go = GlossyButton(quick, "Ale", "ALE !", display, new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(460f, 170f), Sun, Night, panelSprite);
            go.GetComponentInChildren<Text>().fontSize = 72;

            // ---------- Séries ----------
            RectTransform series = MenuScreen(canvas, "Seri");
            Backdrop(series);
            RectTransform seriesRow = Row(series, "Kat", new Vector2(0f, 10f), new Vector2(1700f, 620f), 50f);
            Banner(series, "Chwazi", "CHWAZI SERI W AP KOURI A", display, deep, panelSprite);

            // ---------- Épreuves d'une série ----------
            RectTransform events = MenuScreen(canvas, "Epre");
            Backdrop(events);
            Text seriesTitle = Label(events, "TitSeri", display, 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1400f, 70f), Sun);
            RectTransform eventsRow = Row(events, "Tiil", new Vector2(0f, 150f), new Vector2(1600f, 280f), 34f);
            RectTransform detail = Panel(events, "Detay", new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(1400f, 340f), new Color(blue.r, blue.g, blue.b, 0.95f), null);
            detail.GetComponent<Image>().sprite = panelSprite;
            Text detailTitle = Label(detail, "Tit", display, 46, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(900f, 64f), Paper);
            Text detailInfo = Label(detail, "Enfo", font, 32, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(920f, 200f), Paper);
            detailInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailInfo.supportRichText = true;
            detailInfo.lineSpacing = 1.2f;
            var detailStars = new Image[3];
            for (int k = 0; k < 3; k++)
                detailStars[k] = MenuImage(detail, $"Zetwal{k}", starSprite, new Vector2(1f, 1f), new Vector2(-330f + k * 96f, -26f), new Vector2(88f, 88f));
            Button detailGo = GlossyButton(detail, "Jwe", "JWE !", display, new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(330f, 150f), Sun, Night, panelSprite);
            detailGo.GetComponentInChildren<Text>().fontSize = 64;

            // ---------- Barre du haut : retour, étoiles, gourdes ----------
            RectTransform top = MenuScreen(canvas, "BaAnwo");
            Button back = UiButton(top, "Retou", "<", display, new Vector2(0f, 1f), new Vector2(26f, -20f), new Vector2(110f, 110f), blue, Paper);
            MakeRound(back.GetComponent<Image>(), Paper);
            back.GetComponentInChildren<Text>().fontSize = 64;
            RectTransform starsBox = Panel(top, "Zetwal", new Vector2(1f, 1f), new Vector2(-400f, -24f), new Vector2(300f, 90f), deep, null);
            starsBox.GetComponent<Image>().sprite = panelSprite;
            MenuImage(starsBox, "Ikon", starSprite, new Vector2(0f, 0.5f), new Vector2(12f, 2f), new Vector2(74f, 74f)).color = Sun;
            Text starsText = Label(starsBox, "Kont", display, 38, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(200f, 70f), Paper);
            RectTransform goudBox = Panel(top, "Goud", new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(340f, 90f), deep, null);
            goudBox.GetComponent<Image>().sprite = panelSprite;
            Text gourdes = Label(goudBox, "Kont", display, 38, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(300f, 70f), Sun);

            // ---------- Composants ----------
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
            SetRef(menu, "lockBadge", lockBadge.gameObject);
            SetRef(menu, "lockText", lockText);
            SetRef(menu, "buyPilotButton", buyPilot);

            var flow = canvas.gameObject.AddComponent<CareerMenu>();
            SetRef(menu, "flow", flow);
            SetRef(flow, "home", home.gameObject);
            SetRef(flow, "hub", hub.gameObject);
            SetRef(flow, "garage", garage.gameObject);
            SetRef(flow, "quick", quick.gameObject);
            SetRef(flow, "series", series.gameObject);
            SetRef(flow, "events", events.gameObject);
            SetRef(flow, "topBar", top.gameObject);
            SetRef(flow, "playButton", play);
            SetRef(flow, "backButton", back);
            SetRef(flow, "garageButton", garageButton);
            SetRef(flow, "quickButton", quickButton);
            SetRef(flow, "careerButton", careerButton);
            SetRef(flow, "starsText", starsText);
            SetRef(flow, "gourdesText", gourdes);
            SetRef(flow, "seriesRow", seriesRow);
            SetRef(flow, "eventsRow", eventsRow);
            SetRef(flow, "seriesTitle", seriesTitle);
            SetRef(flow, "detail", detail.gameObject);
            SetRef(flow, "detailTitle", detailTitle);
            SetRef(flow, "detailInfo", detailInfo);
            SetRefArray(flow, "detailStars", detailStars.Cast<Object>().ToArray());
            SetRef(flow, "detailGo", detailGo);
            SetRef(flow, "font", display);
            SetRef(flow, "panelSprite", panelSprite);
            SetRef(flow, "starSprite", starSprite);
            SetRef(flow, "lockSprite", lockSprite);
            SetRefArray(flow, "seriesCards", RallyAyiti.Core.Career.Series.Select(c => (Object)ArtLibrary.LoadSprite(c.Card)).ToArray());
        }

        // ---------- Briques des écrans de menu ----------

        /// <summary>Écran plein cadre (enfant direct du canvas), activé par CareerMenu.</summary>
        private static RectTransform MenuScreen(Transform canvas, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Voile sombre derrière les écrans de carrière (le plateau reste deviné derrière).</summary>
        private static void Backdrop(Transform screen)
        {
            var go = new GameObject("Vwal", typeof(RectTransform));
            go.transform.SetParent(screen, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.03f, 0.08f, 0.2f, 0.62f);
        }

        /// <summary>Rangée centrée qui aligne des tuiles horizontalement (séries, épreuves).</summary>
        private static RectTransform Row(Transform parent, string name, Vector2 pos, Vector2 size, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rt;
        }

        private static void Banner(Transform parent, string name, string text, Font font, Color color, Sprite sprite)
        {
            RectTransform banner = Panel(parent, name, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1300f, 90f), color, null);
            banner.GetComponent<Image>().sprite = sprite;
            Label(banner, "Teks", font, 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1260f, 80f), Paper).text = text;
        }

        private static Image MenuImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Gros bouton bleu (ou doré) glacé aux coins arrondis, façon Beach Buggy.</summary>
        private static Button GlossyButton(Transform parent, string name, string caption, Font font, Vector2 anchor, Vector2 pos,
            Vector2 size, Color background, Color textColor, Sprite sprite)
        {
            Button b = UiButton(parent, name, caption, font, anchor, pos, size, background, textColor);
            var img = b.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            var shadow = b.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(0f, -8f);
            Text label = b.GetComponentInChildren<Text>();
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.lineSpacing = 0.9f;
            return b;
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
            price.fontSize = 24;
            // Petit texte : contour fin, sinon les chiffres deviennent illisibles.
            price.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            price.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.35f);
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
        private static readonly string[] CrowdBodies =
        {
            "Casual_Male", "Casual_Female", "Casual2_Male", "Casual3_Male", "Worker_Male", "OldClassy_Female",
            "Chef_Female", "Casual_Bald", "Doctor_Female_Young", "Casual3_Female",
        };

        /// <summary>Personnes posées pendant la construction de la scène, animées ensuite par CrowdLife.</summary>
        private static readonly List<(Transform person, bool cheering)> Crowd = new();

        /// <summary>Un seul composant anime toute la foule ; les personnes ne doivent pas être statiques (batching).</summary>
        private static void CreateCrowdLife()
        {
            Crowd.RemoveAll(c => c.person == null);
            if (Crowd.Count == 0) return;
            foreach ((Transform person, _) in Crowd)
                foreach (Transform t in person.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            var life = new GameObject("FoulVivan").AddComponent<RallyAyiti.Environment.CrowdLife>();
            SetRefArray(life, "people", Crowd.Select(c => (Object)c.person).ToArray());
            SerializedProperty p = Prop(life, "cheering", out SerializedObject so);
            p.arraySize = Crowd.Count;
            for (int i = 0; i < Crowd.Count; i++) p.GetArrayElementAtIndex(i).boolValue = Crowd[i].cheering;
            so.ApplyModifiedPropertiesWithoutUndo();
            Crowd.Clear();
        }

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
            // Passants ronds avec visage (Quaternius) ; anciens personnages en blocs si les modèles manquent.
            outfit["Q_Skin"] = ArtLibrary.Get("Q_Skin", SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)]);
            outfit["Q_Shirt"] = ArtLibrary.Get("Q_Shirt", shirts[UnityEngine.Random.Range(0, shirts.Length)]);
            outfit["Q_Main"] = outfit["Q_Shirt"];
            outfit["Q_Clothes"] = outfit["Q_Shirt"];
            outfit["Q_Pants"] = ArtLibrary.Get("Q_Pants", pants[UnityEngine.Random.Range(0, pants.Length)]);
            string body = $"Moun_{CrowdBodies[UnityEngine.Random.Range(0, CrowdBodies.Length)]}_{(cheering ? "Cheer" : "Stand")}";
            if (!ArtLibrary.HasModel(body)) body = cheering ? "Person_Cheer" : "Person_Stand";
            Transform t = ArtLibrary.Spawn(body, parent, outfit);
            if (t == null) return null;
            Crowd.Add((t, cheering));
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
