using RallyAyiti.Core;
using RallyAyiti.Kart;
using RallyAyiti.PowerUps;
using RallyAyiti.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Test automatique : ouvre un circuit, met le kart du joueur en pilote automatique, lance ses bonus et son pouvoir,
    /// note la course dans Logs/rally.log et prend des captures dans Logs/. S'arrête seul après 75 s.
    /// </summary>
    [InitializeOnLoad]
    public static class RallyTester
    {
        private const string Key = "RallyAyiti.AutoTest";
        private const float Duration = 75f;
        private static readonly float[] RaceShots = { 2f, 8f, 18f, 30f, 45f, 60f };
        private static readonly float[] CareerShots = { 8f, 18f, 30f, 72f };
        private static readonly float[] MenuShots = { 2f, 4f, 6f, 8f, 10f, 12f };
        private const string EventKey = "RallyAyiti.TestEvent";
        private static float[] Shots = RaceShots;

        private static float _start, _nextLog, _nextAction;
        private static int _shot;
        private static string _scene;

        static RallyTester() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("Rally Ayiti/Test auto/Labadie")]
        private static void TestLabadie() => Run("Labadie");

        [MenuItem("Rally Ayiti/Test auto/Katye")]
        private static void TestKatye() => Run("Prototype");

        /// <summary>Épreuve 1 de la première série (course 2 tours) : vérifie les étoiles de l'écran de résultat.</summary>
        [MenuItem("Rally Ayiti/Test auto/Karyè 1-1")]
        private static void TestCareer()
        {
            SessionState.SetString(EventKey, "0,0");
            Run("Labadie");
        }

        /// <summary>Parcourt les écrans du menu (accueil, centre, garage, séries, épreuves, course rapide) avec une capture chacun.</summary>
        [MenuItem("Rally Ayiti/Test auto/Meni")]
        private static void TestMenu() => Run("Meni");

        private static void Run(string scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{scene}.unity");
            SessionState.SetString(Key, scene);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            _scene = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(_scene)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _start = (float)EditorApplication.timeSinceStartup;
                _nextLog = _nextAction = 0f;
                _shot = 0;
                Shots = _scene == "Meni" ? MenuShots : SessionState.GetString(EventKey, "") != "" ? CareerShots : RaceShots;
                Debug.Log($"Rally Ayiti TEST : départ sur {_scene}, gourdes {GameSettings.Gourdes}");
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                SessionState.EraseString(Key);
                SessionState.EraseString(EventKey);
                Debug.Log("Rally Ayiti TEST : fin");
            }
        }

        private static void MenuTick(float t)
        {
            var flow = Object.FindFirstObjectByType<RallyAyiti.UI.CareerMenu>();
            if (flow == null) return;
            RallyAyiti.UI.CareerMenu.Screen[] order =
            {
                RallyAyiti.UI.CareerMenu.Screen.Home, RallyAyiti.UI.CareerMenu.Screen.Hub, RallyAyiti.UI.CareerMenu.Screen.Garage,
                RallyAyiti.UI.CareerMenu.Screen.Series, RallyAyiti.UI.CareerMenu.Screen.Events, RallyAyiti.UI.CareerMenu.Screen.Quick,
            };
            if (_shot < Shots.Length && t > Shots[_shot] - 1f && flow.Current != order[_shot]) flow.Show(order[_shot]);
            if (_shot < Shots.Length && t > Shots[_shot])
            {
                ScreenCapture.CaptureScreenshot($"Logs/test_Meni_{_shot}.png");
                _shot++;
            }
            if (t > 13.5f) EditorApplication.ExitPlaymode();
        }

        private static void Tick()
        {
            float t = (float)EditorApplication.timeSinceStartup - _start;
            if (_scene == "Meni")
            {
                MenuTick(t);
                return;
            }
            var setup = Object.FindFirstObjectByType<RaceSetup>();
            RaceManager race = RaceManager.Current;
            if (setup == null || setup.Player == null || race == null) return;
            KartController kart = setup.Player;

            var ai = kart.GetComponent<AIDriver>();
            if (ai != null) kart.SetInput(ai);

            if (t > _nextAction)
            {
                _nextAction = t + 0.5f;
                var items = kart.GetComponent<PowerUpSystem>();
                if (items != null && items.Current != PowerUpType.None)
                {
                    PowerUpType held = items.Current;
                    if (items.TryUse()) Debug.Log($"Rally Ayiti TEST : bonus {held} lancé à {t:0}s");
                }
                var power = kart.GetComponent<PilotPower>();
                if (power != null && power.TryActivate()) Debug.Log($"Rally Ayiti TEST : pouvoir {power.PowerName} à {t:0}s");
            }

            if (t > _nextLog)
            {
                _nextLog = t + 5f;
                LapTracker me = race.Player;
                Debug.Log($"Rally Ayiti TEST : t={t:0}s phase={race.Phase} place={race.PositionOf(me)}/{race.Standings.Count} " +
                          $"tour={me.Lap} vitesse={kart.CurrentSpeed * 3.6f:0} km/h pièces={race.PlayerCoins} " +
                          $"touché={kart.IsStunned} bouclier={kart.IsShielded} fps={1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0}");
            }

            if (_shot < Shots.Length && t > Shots[_shot])
            {
                ScreenCapture.CaptureScreenshot($"Logs/test_{_scene}_{_shot}.png");
                _shot++;
            }

            if (t > Duration) EditorApplication.ExitPlaymode();
        }
    }
}
