using System.Linq;
using RallyAyiti.PowerUps;
using RallyAyiti.Race;
using RallyAyiti.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Ce qui fait l'esprit « kart arcade mobile » : pouvoirs des pilotes, fusée, peau de banane,
    /// gourdes sur la piste, effets, classement et jauge de pouvoir à l'écran.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        /// <summary>Pouvoir de chaque pilote, dans l'ordre du catalogue (document de conception).</summary>
        private static readonly (PilotPowerType type, string name, string description)[] PilotPowers =
        {
            (PilotPowerType.LapliMango, "Lapli mango", "Lage yon lapli mango dèyè l"),
            (PilotPowerType.Repare, "Repare", "Repare kat la epi pati an boost"),
            (PilotPowerType.BouclyeElektrik, "Bouclye", "Bouclye elektrik 4 segonn"),
            (PilotPowerType.OnnKonpa, "Onn konpa", "Onn son ki toudi tout moun bò kote l"),
            (PilotPowerType.Swen, "Swen", "Swen epi pwoteje l 3 segonn"),
            (PilotPowerType.KlaksonJeyan, "Klaksòn jeyan", "Klaksòn ki pouse tout kat ki toupre"),
        };

        private static MangoTrap BananaPrefab()
        {
            var go = new GameObject("PoBannann");
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.1f;
            trigger.center = new Vector3(0f, 0.4f, 0f);
            go.AddComponent<MangoTrap>();
            Transform peel = ArtLibrary.Spawn("BananaPeel", go.transform);
            if (peel != null) peel.localScale = Vector3.one * 1.6f;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/PoBannann.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<MangoTrap>();
        }

        private static HomingRocket RocketPrefab()
        {
            var go = new GameObject("Fize");
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.9f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            go.AddComponent<HomingRocket>();
            Transform body = ArtLibrary.Spawn("Rocket", go.transform);
            if (body != null) body.localScale = Vector3.one * 1.4f;
            ParticleSystem smoke = Particles("Lafimen", go.transform, new Vector3(0f, 0f, -1.4f), new Color(1f, 0.85f, 0.6f, 0.8f), 0.45f, 2f, 70f, 0.5f);
            ParticleSystem.MainModule main = smoke.main;
            main.playOnAwake = true;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Fize.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<HomingRocket>();
        }

        private static void CreateRaceFx()
        {
            var fx = new GameObject("Efè").AddComponent<RaceFx>();
            SetRef(fx, "waveMaterial", ArtLibrary.Get("Fx_Wave"));
            SetRef(fx, "shieldMaterial", ArtLibrary.Get("Fx_Shield"));
            SetRef(fx, "boltMaterial", ArtLibrary.Get("Fx_Bolt"));
            SetRef(fx, "sparkles", SparkleSystem(fx.transform));
        }

        /// <summary>Lignes de pièces de gourde en zigzag le long du tracé, entre les caisses de bonus.</summary>
        private static void CreateCoins(SplineContainer line, Theme th)
        {
            var root = new GameObject("Goud").transform;
            float length = line.CalculateLength();
            float[] rows = { 0.16f, 0.29f, 0.37f, 0.6f, 0.66f, 0.82f, 0.93f };
            foreach (float t0 in rows)
            {
                if (Mathf.Abs(t0 - th.RampT) < 0.02f) continue;
                float side = Random.value < 0.5f ? -1f : 1f;
                for (int k = 0; k < 6; k++)
                {
                    Frame f = FrameAt(line, t0 + k * 4.5f / length);
                    float lateral = side * Mathf.Sin(k * 0.6f) * 3.5f;
                    var go = new GameObject("Pyès");
                    go.transform.SetParent(root, false);
                    go.transform.SetPositionAndRotation(f.Pos + f.Right * lateral, Quaternion.LookRotation(f.Fwd));
                    var box = go.AddComponent<SphereCollider>();
                    box.isTrigger = true;
                    box.radius = 1.1f;
                    box.center = new Vector3(0f, 1f, 0f);
                    var coin = go.AddComponent<CoinPickup>();
                    Transform visual = ArtLibrary.Spawn("Coin", go.transform);
                    if (visual == null) continue;
                    visual.localScale = Vector3.one * 1.5f;
                    visual.localPosition = new Vector3(0f, 1f, 0f);
                    foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    SetRef(coin, "visual", visual);
                }
            }
        }

        /// <summary>Classement des pilotes, gourdes ramassées, bouton de pouvoir avec sa jauge, pikliz plein écran.</summary>
        private static void CreateArcadeHud(Transform canvas, RaceHUD hud, Font font, Font display)
        {
            // Pikliz : derrière tout le reste de l'interface.
            var splatGo = new GameObject("Pikliz", typeof(RectTransform));
            splatGo.transform.SetParent(canvas, false);
            splatGo.transform.SetAsFirstSibling();
            var splatRt = (RectTransform)splatGo.transform;
            splatRt.anchorMin = Vector2.zero;
            splatRt.anchorMax = Vector2.one;
            splatRt.offsetMin = splatRt.offsetMax = Vector2.zero;
            var splatImg = splatGo.AddComponent<Image>();
            splatImg.sprite = ArtLibrary.LoadSprite("UI_Pikliz.png");
            splatImg.raycastTarget = false;
            var splat = splatGo.AddComponent<CanvasGroup>();
            splat.alpha = 0f;
            splat.blocksRaycasts = false;
            splat.interactable = false;

            Text coins = Label(canvas, "Goud", display, 34, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-230f, -180f), new Vector2(300f, 50f), Sun);

            // Bouton de pouvoir du pilote : rond à gauche sous la place, il se remplit puis s'illumine (gâchette gauche).
            RectTransform button = Panel(canvas, "Pouvwa", new Vector2(0f, 1f), new Vector2(40f, -170f), new Vector2(170f, 170f),
                new Color(Night.r, Night.g, Night.b, 0.9f), Sun);
            Image halo = RoundLayer(button, "Lalimye", "UI_Halo.png", Sun, -40f);
            halo.transform.SetAsFirstSibling();
            var fillGo = new GameObject("Jaj", typeof(RectTransform));
            fillGo.transform.SetParent(button, false);
            var fill = (RectTransform)fillGo.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(10f, 10f);
            fill.offsetMax = new Vector2(-10f, -10f);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = ArtLibrary.LoadSprite("UI_Round.png");
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Vertical;
            fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
            fillImg.fillAmount = 0f;
            fillImg.color = new Color(Sun.r, Sun.g, Sun.b, 0.9f);
            fillImg.raycastTarget = false;
            MakeRound(button.GetComponent<Image>(), Sun);
            var onScreen = button.gameObject.AddComponent<OnScreenButton>();
            SetString(onScreen, "m_ControlPath", "<Gamepad>/leftShoulder");
            Text label = Label(button, "Teks", display, 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 160f), Paper);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = "POUVWA";

            SetRef(hud, "coinsText", coins);
            SetRef(hud, "powerFill", fill);
            SetRef(hud, "powerLabel", label);
            SetRef(hud, "powerBackground", button.GetComponent<Image>());
            SetRef(hud, "powerHalo", halo);
            SetRef(hud, "splat", splat);
        }

        /// <summary>Barre de progression DEPA → FINI, flèches de virage sur les bords, bouton et menu pause.</summary>
        private static void CreateRaceGuides(Transform canvas, RaceManager race, SplineContainer line, Font display)
        {
            Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Barre de progression.
            RectTransform bar = Panel(canvas, "Pwogresyon", new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(820f, 14f),
                new Color(Night.r, Night.g, Night.b, 0.75f), null);
            var fillGo = new GameObject("Ranpli", typeof(RectTransform));
            fillGo.transform.SetParent(bar, false);
            var fill = (RectTransform)fillGo.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(Sun.r, Sun.g, Sun.b, 0.9f);
            fillImg.raycastTarget = false;
            Label(bar, "Depa", display, 22, TextAnchor.MiddleRight, new Vector2(0f, 0.5f), new Vector2(-14f, 0f), new Vector2(120f, 30f), Paper).text = "DEPA";
            Text finish = Label(bar, "Fini", display, 22, TextAnchor.MiddleLeft, new Vector2(1f, 0.5f), new Vector2(14f, 0f), new Vector2(120f, 30f), Paper);
            finish.text = "FINI";
            ((RectTransform)finish.transform).pivot = new Vector2(0f, 0.5f);
            ((RectTransform)bar.Find("Depa")).pivot = new Vector2(1f, 0.5f);

            var progress = bar.gameObject.AddComponent<RaceProgressBar>();
            SetRef(progress, "race", race);
            SetRef(progress, "track", bar);
            SetRef(progress, "fill", fill);
            SetRef(progress, "markerSprite", round);
            SetStringArray(progress, "pilotNames", Pilots.Select(p => p.Name).ToArray());
            SetColorArray(progress, "pilotColors", Pilots.Select(p => p.Helmet).ToArray());

            // Flèches de virage, à mi-hauteur sur les bords de l'écran.
            CanvasGroup Arrow(string name, string caption, Vector2 anchor, Vector2 pos)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(250f, 196f);
                if (caption == "<<") rt.localScale = new Vector3(-1f, 1f, 1f);
                var img = go.AddComponent<Image>();
                // Grande flèche blanche détourée, semi-transparente (repère de virage façon Beach Buggy).
                img.sprite = ArtLibrary.LoadSprite("UI_Arrow.png") ?? ArtLibrary.LoadSprite("UI_Chevron.png");
                img.color = new Color(1f, 1f, 1f, 0.9f);
                img.raycastTarget = false;
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(5f, -5f);
                var group = go.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                return group;
            }
            var turns = new GameObject("FlechViraj").AddComponent<TurnIndicator>();
            turns.transform.SetParent(canvas, false);
            SetRef(turns, "race", race);
            SetRef(turns, "line", line);
            SetRef(turns, "left", Arrow("FlechGoch", "<<", new Vector2(0f, 0.5f), new Vector2(60f, -40f)));
            SetRef(turns, "right", Arrow("FlechDwat", ">>", new Vector2(1f, 0.5f), new Vector2(-60f, -40f)));

            // Pause : petit bouton rond à droite de la case bonus, panneau centré.
            Button pauseButton = UiButton(canvas, "Poz", "II", display, new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(84f, 84f),
                new Color(Night.r, Night.g, Night.b, 0.85f), Paper);
            MakeRound(pauseButton.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.6f));
            RectTransform panel = Panel(canvas, "MeniPoz", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 560f),
                new Color(Night.r, Night.g, Night.b, 0.94f), Sun);
            Label(panel, "Tit", display, 64, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(560f, 90f), Sun).text = "POZ";
            Button resume = UiButton(panel, "Kontinye", "KONTINYE", display, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(420f, 96f), Green, Paper);
            Button retry = UiButton(panel, "Rejwe", "REJWE", display, new Vector2(0.5f, 0.5f), new Vector2(0f, -76f), new Vector2(420f, 96f), Lagoon, Paper);
            Button menu = UiButton(panel, "Meni", "MENI", display, new Vector2(0.5f, 0.5f), new Vector2(0f, -192f), new Vector2(420f, 96f), Hibiscus, Paper);

            var pause = canvas.gameObject.AddComponent<PauseMenu>();
            SetRef(pause, "pauseButton", pauseButton);
            SetRef(pause, "panel", panel.gameObject);
            SetRef(pause, "resumeButton", resume);
            SetRef(pause, "retryButton", retry);
            SetRef(pause, "menuButton", menu);
        }

        /// <summary>Particules étoilées additives émises à la demande par RaceFx.Sparkle (aucune émission continue).</summary>
        private static ParticleSystem SparkleSystem(Transform parent)
        {
            var go = new GameObject("Zetwal");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.gravityModifier = 0.35f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.7f;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = StarParticleMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        private static Material StarParticleMaterial()
        {
            string path = $"{Root}/Materials/Zetwal.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtLibrary.ArtRoot}/Textures/Fx_Star.png");
            if (tex != null) mat.mainTexture = tex;
            if (mat.HasProperty("_Surface"))
            {
                // Additif : les étoiles brillent par-dessus le décor.
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 2f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Caméra de survol (inactive) et composant RaceIntro, branché sur le RaceManager et le HUD.</summary>
        private static void CreateIntro(RaceManager race)
        {
            var go = new GameObject("KameraSiryol");
            var cam = go.AddComponent<Unity.Cinemachine.CinemachineCamera>();
            Unity.Cinemachine.LensSettings lens = cam.Lens;
            lens.FieldOfView = 55f;
            lens.FarClipPlane = 700f;
            cam.Lens = lens;
            go.SetActive(false);

            var intro = new GameObject("Entwodiksyon").AddComponent<RaceIntro>();
            SetRef(intro, "introCam", cam);
            GameObject hud = GameObject.Find("HUD");
            if (hud != null)
            {
                CanvasGroup group = hud.GetComponent<CanvasGroup>();
                if (group == null) group = hud.AddComponent<CanvasGroup>();
                SetRef(intro, "hud", group);
            }
            SetRef(race, "intro", intro);
        }

        /// <summary>Remplace un fond rectangulaire par un disque glacé, avec un anneau lumineux optionnel.</summary>
        private static void MakeRound(Image image, Color? ring)
        {
            Sprite round = ArtLibrary.LoadSprite("UI_Round.png");
            if (round == null) return;
            image.sprite = round;
            image.type = Image.Type.Simple;
            var outline = image.GetComponent<Outline>();
            if (outline != null) Object.DestroyImmediate(outline);
            if (ring is { } c) RoundLayer(image.transform, "Kontou", "UI_RoundRing.png", c, -6f);
        }

        /// <summary>Calque rond posé sur (ou autour de) un élément : anneau, halo.</summary>
        private static Image RoundLayer(Transform parent, string name, string sprite, Color color, float outset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(outset, outset);
            rt.offsetMax = new Vector2(-outset, -outset);
            var img = go.AddComponent<Image>();
            img.sprite = ArtLibrary.LoadSprite(sprite);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void SetColorArray(Object target, string field, Color[] values)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).colorValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStringArray(Object target, string field, string[] values)
        {
            SerializedProperty p = Prop(target, field, out SerializedObject so);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
