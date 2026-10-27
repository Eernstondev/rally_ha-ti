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

            RectTransform board = Panel(canvas, "Klasman", new Vector2(0f, 1f), new Vector2(30f, -215f), new Vector2(330f, 250f),
                new Color(Night.r, Night.g, Night.b, 0.5f), null);
            Text standings = Label(board, "Lis", font, 27, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(300f, 230f), Paper);
            standings.lineSpacing = 1.05f;
            standings.supportRichText = true;

            Text coins = Label(canvas, "Goud", display, 40, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-40f, -162f), new Vector2(300f, 56f), Sun);

            // Bouton de pouvoir : carré doré qui se remplit, simule la gâchette gauche de la manette.
            RectTransform button = Panel(canvas, "Pouvwa", new Vector2(1f, 0f), new Vector2(-490f, 330f), new Vector2(170f, 170f),
                new Color(Night.r, Night.g, Night.b, 0.9f), Sun);
            var fillGo = new GameObject("Jaj", typeof(RectTransform));
            fillGo.transform.SetParent(button, false);
            var fill = (RectTransform)fillGo.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(1f, 0f);
            fill.offsetMin = new Vector2(8f, 8f);
            fill.offsetMax = new Vector2(-8f, -8f);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(Sun.r, Sun.g, Sun.b, 0.85f);
            fillImg.raycastTarget = false;
            var onScreen = button.gameObject.AddComponent<OnScreenButton>();
            SetString(onScreen, "m_ControlPath", "<Gamepad>/leftShoulder");
            Text label = Label(button, "Teks", display, 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 160f), Paper);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = "POUVWA";

            SetRef(hud, "standingsText", standings);
            SetRef(hud, "coinsText", coins);
            SetRef(hud, "powerFill", fill);
            SetRef(hud, "powerLabel", label);
            SetRef(hud, "powerBackground", button.GetComponent<Image>());
            SetRef(hud, "splat", splat);
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
