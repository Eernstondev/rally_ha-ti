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
        private static readonly float[] Shots = { 2f, 8f, 18f, 30f, 45f, 60f };

        private static float _start, _nextLog, _nextAction;
        private static int _shot;
        private static string _scene;

        static RallyTester() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("Rally Ayiti/Test auto/Labadie")]
        private static void TestLabadie() => Run("Labadie");

        [MenuItem("Rally Ayiti/Test auto/Katye")]
        private static void TestKatye() => Run("Prototype");

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
                Debug.Log($"Rally Ayiti TEST : départ sur {_scene}, gourdes {GameSettings.Gourdes}");
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                SessionState.EraseString(Key);
                Debug.Log("Rally Ayiti TEST : fin");
            }
        }

        private static void Tick()
        {
            float t = (float)EditorApplication.timeSinceStartup - _start;
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
