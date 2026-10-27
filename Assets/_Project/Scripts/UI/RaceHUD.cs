using System.Collections;
using System.Text;
using RallyAyiti.Audio;
using RallyAyiti.Core;
using RallyAyiti.Race;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RallyAyiti.UI
{
    /// <summary>Place, tour, chrono, vitesse, messages centraux (décompte, résultat) et tableau d'arrivée.</summary>
    public sealed class RaceHUD : MonoBehaviour
    {
        private static readonly string[] Places =
            { "Premye plas !", "Dezyèm plas", "Twazyèm plas", "Katriyèm plas", "Senkyèm plas", "Sizyèm plas" };

        [SerializeField] private RaceManager race;
        [SerializeField] private Text positionText;
        [SerializeField] private Text lapText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text centerText;
        [Header("Arrivée")]
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Text resultsText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private float resultsDelay = 2f;

        private int _pos = -1, _lap = -1, _speed = -1, _tenths = -1;
        private Coroutine _hide;

        private void Awake()
        {
            if (resultsPanel != null) resultsPanel.SetActive(false);
            if (retryButton != null) retryButton.onClick.AddListener(() => Load(SceneManager.GetActiveScene().name));
            if (menuButton != null) menuButton.onClick.AddListener(() => Load(GameSettings.MenuScene));
        }

        private void OnEnable()
        {
            race.CountdownTick += OnCountdown;
            race.RaceEnded += OnRaceEnded;
        }

        private void OnDisable()
        {
            race.CountdownTick -= OnCountdown;
            race.RaceEnded -= OnRaceEnded;
        }

        private void Update()
        {
            LapTracker player = race.Player;
            if (player == null) return;

            int pos = race.PositionOf(player);
            if (pos != _pos) { _pos = pos; positionText.text = $"{pos}/{race.Standings.Count}"; }

            int lap = Mathf.Min(player.Lap + 1, race.TotalLaps);
            if (lap != _lap) { _lap = lap; lapText.text = $"Tou {lap}/{race.TotalLaps}"; }

            int speed = Mathf.RoundToInt(Mathf.Abs(player.Kart.CurrentSpeed) * 3.6f);
            if (speed != _speed) { _speed = speed; speedText.text = $"{speed} km/h"; }

            float time = player.Finished ? player.FinishTime : race.RaceTime;
            int tenths = Mathf.FloorToInt(time * 10f);
            if (tenths != _tenths)
            {
                _tenths = tenths;
                timeText.text = FormatTime(time);
            }
        }

        private static string FormatTime(float time)
        {
            int tenths = Mathf.FloorToInt(time * 10f);
            return $"{tenths / 600}:{(tenths % 600) / 10f:00.0}";
        }

        private void OnCountdown(int n)
        {
            Show(n > 0 ? n.ToString() : "ALE !", n == 0 ? 1f : 0f);
        }

        private void OnRaceEnded()
        {
            int pos = race.PositionOf(race.Player);
            Show(Places[Mathf.Clamp(pos - 1, 0, Places.Length - 1)], resultsDelay);
            if (resultsPanel != null) StartCoroutine(ShowResults());
        }

        private IEnumerator ShowResults()
        {
            yield return new WaitForSeconds(resultsDelay);
            var sb = new StringBuilder();
            for (int i = 0; i < race.Standings.Count; i++)
            {
                LapTracker r = race.Standings[i];
                string time = r.Finished ? FormatTime(r.FinishTime) : "--:--";
                string line = $"{i + 1}.  {r.DisplayName,-18} {time}";
                sb.AppendLine(r.IsPlayer ? $"<color=#f6c22b>{line}</color>" : line);
            }
            resultsText.text = sb.ToString();
            resultsPanel.SetActive(true);
        }

        private static void Load(string scene)
        {
            GameAudio.Play(Sfx.UiClick);
            SceneManager.LoadScene(scene);
        }

        private void Show(string message, float hideAfter)
        {
            if (_hide != null) StopCoroutine(_hide);
            centerText.text = message;
            centerText.gameObject.SetActive(true);
            if (hideAfter > 0f) _hide = StartCoroutine(HideAfter(hideAfter));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            centerText.gameObject.SetActive(false);
            _hide = null;
        }
    }
}
