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
        private static readonly string[] Places = { "1e !", "2yèm", "3yèm", "4yèm", "5yèm", "6yèm" };

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
        [Tooltip("Traits de vitesse plein écran, visibles pendant les boosts.")]
        [SerializeField] private Image speedLines;
        [Header("Arrivée")]
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Text resultsText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private float resultsDelay = 2f;
        [Header("Carrière")]
        [Tooltip("Trois étoiles de l'écran de résultat (dorées si gagnées).")]
        [SerializeField] private Image[] resultStars;
        [Tooltip("Titre et objectif de l'épreuve, affichés au départ.")]
        [SerializeField] private Text eventText;

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
            EventDef ev = Career.CurrentEvent;
            if (eventText != null)
            {
                eventText.text = ev != null ? $"{Career.KindLabel(ev.Kind)} · {ev.Title.ToUpperInvariant()}\n<size=26>{Career.GoalText(ev)}</size>" : "";
                eventText.supportRichText = true;
                if (ev != null) StartCoroutine(HideEventText(11f));
            }
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

            if (speedLines != null)
            {
                // Apparaissent vite au boost, s'effacent doucement ; légère pulsation pour donner du mouvement.
                bool boost = player.Kart.IsBoosting;
                float target = boost ? 0.35f + 0.45f * player.Kart.Speed01 : 0f;
                Color c = speedLines.color;
                c.a = Mathf.MoveTowards(c.a, target, Time.deltaTime * (boost ? 4f : 1.5f));
                speedLines.color = c;
                speedLines.transform.localScale = Vector3.one * (1.04f + 0.04f * Mathf.Sin(Time.time * 30f));
            }

            if (splat != null)
            {
                _splat = Mathf.Max(0f, _splat - Time.deltaTime);
                splat.alpha = Mathf.Clamp01(_splat / 0.8f);
            }
        }

        private float _centerPop;

        /// <summary>Le message central arrive en grand puis se pose (effet « claque » des comptes à rebours arcade).</summary>
        private void AnimateCenter()
        {
            if (_centerPop <= 0f || centerText == null) return;
            _centerPop = Mathf.Max(0f, _centerPop - Time.unscaledDeltaTime * 4f);
            float t = 1f - _centerPop;
            float scale = t < 0.6f ? Mathf.Lerp(2.2f, 0.9f, t / 0.6f) : Mathf.Lerp(0.9f, 1f, (t - 0.6f) / 0.4f);
            centerText.transform.localScale = Vector3.one * scale;
            Color c = centerText.color;
            c.a = Mathf.Clamp01(t * 3f);
            centerText.color = c;
        }

        private void Update()
        {
            AnimateCenter();
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
            Show(n > 0 ? n.ToString() : "ALE !!", n == 0 ? 1f : 0f);
        }

        private void OnRaceEnded()
        {
            int pos = race.PositionOf(race.Player);
            Show(Places[Mathf.Clamp(pos - 1, 0, Places.Length - 1)], resultsDelay);
            if (race.Player != null && race.Player.Finished && pos <= 3) StartCoroutine(Confetti(race.Player.transform, pos == 1 ? 7 : 4));
            if (resultsPanel != null) StartCoroutine(ShowResults());
        }

        private static readonly Color[] ConfettiColors =
        {
            new(0.96f, 0.76f, 0.17f), new(0.12f, 0.71f, 0.66f), new(0.93f, 0.3f, 0.45f), new(0.25f, 0.45f, 0.95f), Color.white,
        };

        /// <summary>Gerbes d'étoiles multicolores autour du kart du joueur quand il monte sur le podium.</summary>
        private IEnumerator Confetti(Transform kart, int bursts)
        {
            for (int i = 0; i < bursts && kart != null; i++)
            {
                Vector3 p = kart.position + Vector3.up * 1.5f + Random.insideUnitSphere * 1.6f;
                RaceFx.Sparkle(p, ConfettiColors[i % ConfettiColors.Length], 26);
                yield return new WaitForSeconds(0.18f);
            }
        }

        private IEnumerator ShowResults()
        {
            yield return new WaitForSeconds(resultsDelay);
            string career = race.Player != null ? CareerResult(race.Player) : "";
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
            if (career.Length > 0) sb.Append("\n").Append(career);
            resultsText.text = sb.ToString();
            resultsPanel.SetActive(true);
        }

        private IEnumerator HideEventText(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (eventText != null) eventText.text = "";
        }

        /// <summary>Épreuve de carrière : étoiles gagnées, bonus de gourdes pour les nouvelles étoiles, pilote débloqué.</summary>
        private string CareerResult(LapTracker player)
        {
            EventDef ev = Career.CurrentEvent;
            if (ev == null || !(Career.Current is { } c))
            {
                if (resultStars != null) foreach (Image s in resultStars) if (s != null) s.gameObject.SetActive(false);
                return "";
            }
            int place = race.PositionOf(player);
            int stars = Career.Evaluate(ev, player.Finished, place, player.FinishTime, race.PlayerCoins);
            int gained = Career.Record(c.series, c.ev, stars);
            int bonus = ev.Reward * gained / 3;
            if (bonus > 0) GameSettings.Gourdes += bonus;
            if (resultStars != null)
                for (int i = 0; i < resultStars.Length; i++)
                    if (resultStars[i] != null)
                    {
                        resultStars[i].gameObject.SetActive(true);
                        resultStars[i].color = i < stars ? new Color(1f, 0.82f, 0.15f) : new Color(1f, 1f, 1f, 0.22f);
                    }

            var sb = new StringBuilder();
            sb.Append($"<color=#f6c22b>{ev.Title}</color>   ");
            sb.Append(ev.Kind switch
            {
                EventKind.KontLamont => $"Tan : {FormatTime(player.FinishTime)}",
                EventKind.RanmaseGoud => $"Pyès : {race.PlayerCoins}",
                _ => $"Plas : {place}",
            });
            if (bonus > 0) sb.Append($"\n<color=#1fb5a8>+{bonus} goud pou {gained} nouvo zetwal</color>");
            if (ev.Kind == EventKind.Dwel && stars > 0 && ev.Boss >= 0 && !Career.PilotUnlocked(ev.Boss))
            {
                Career.UnlockPilot(ev.Boss);
                sb.Append("\n<color=#e2367a>NOUVO PILÒT DEBLOKE !</color>");
            }
            return sb.ToString();
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
            _centerPop = 1f;
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
