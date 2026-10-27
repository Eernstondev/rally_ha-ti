using System.Collections;
using System.Text;
using RallyAyiti.Audio;
using RallyAyiti.Core;
using RallyAyiti.Kart;
using RallyAyiti.PowerUps;
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
        [Header("Façon Beach Buggy")]
        [SerializeField] private Text standingsText;
        [SerializeField] private Text coinsText;
        [SerializeField] private RectTransform powerFill;
        [SerializeField] private Text powerLabel;
        [SerializeField] private Image powerBackground;
        [Tooltip("Halo qui s'allume autour du bouton quand le pouvoir est prêt.")]
        [SerializeField] private Image powerHalo;
        [SerializeField] private CanvasGroup splat;
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
            race.CoinsChanged += OnCoins;
            PowerUpSystem.Splatted += OnSplatted;
        }

        private void OnDisable()
        {
            race.CountdownTick -= OnCountdown;
            race.RaceEnded -= OnRaceEnded;
            race.CoinsChanged -= OnCoins;
            PowerUpSystem.Splatted -= OnSplatted;
        }

        private void Start()
        {
            OnCoins(0);
            if (splat != null) splat.alpha = 0f;
            if (race.Player != null) _power = race.Player.GetComponent<PilotPower>();
            if (powerLabel != null && _power != null) powerLabel.text = _power.PowerName.ToUpperInvariant();
        }

        private PilotPower _power;
        private Image _powerFillImage;
        private float _standingsTimer, _splat;

        private void OnCoins(int coins)
        {
            if (coinsText != null) coinsText.text = $"{coins * RaceManager.CoinValue} G";
        }

        private void OnSplatted(KartController kart)
        {
            if (race.Player != null && kart == race.Player.Kart) _splat = 2.2f;
        }

        private void UpdateExtras(LapTracker player)
        {
            if ((_standingsTimer -= Time.deltaTime) <= 0f && standingsText != null)
            {
                _standingsTimer = 0.2f;
                var sb = new StringBuilder();
                for (int i = 0; i < race.Standings.Count; i++)
                {
                    LapTracker r = race.Standings[i];
                    string line = $"{i + 1}  {r.DisplayName}";
                    sb.AppendLine(r == player ? $"<color=#f6c22b>{line}</color>" : line);
                }
                standingsText.text = sb.ToString();
            }

            if (_power != null && powerFill != null)
            {
                // Jauge ronde (image « Filled ») ou ancienne jauge rectangulaire qui monte.
                if (_powerFillImage == null) _powerFillImage = powerFill.GetComponent<Image>();
                if (_powerFillImage != null && _powerFillImage.type == Image.Type.Filled) _powerFillImage.fillAmount = _power.Charge01;
                else powerFill.anchorMax = new Vector2(1f, _power.Charge01);
                if (powerHalo != null)
                {
                    Color h = powerHalo.color;
                    h.a = _power.Ready ? 0.55f + 0.45f * Mathf.Sin(Time.time * 6f) : 0f;
                    powerHalo.color = h;
                }
                if (powerBackground != null)
                {
                    // Opaque tant que la jauge se remplit ; quand elle est pleine, le contour doré clignote à travers.
                    float glow = _power.Ready ? 0.6f + 0.3f * Mathf.Sin(Time.time * 8f) : 0.92f;
                    Color c = powerBackground.color;
                    c.a = glow;
                    powerBackground.color = c;
                }
            }

            if (splat != null)
            {
                _splat = Mathf.Max(0f, _splat - Time.deltaTime);
                splat.alpha = Mathf.Clamp01(_splat / 0.8f);
            }
        }

        private void Update()
        {
            LapTracker player = race.Player;
            if (player == null) return;
            UpdateExtras(player);

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
            sb.AppendLine();
            sb.Append($"<color=#1fb5a8>+{race.Reward} goud</color>   ({race.PlayerCoins} pyès)   Total : {GameSettings.Gourdes} G");
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
