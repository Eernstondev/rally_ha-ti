using System;
using RallyAyiti.Audio;
using RallyAyiti.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RallyAyiti.UI
{
    /// <summary>
    /// Menu de départ : le kart du pilote choisi tourne sur son plateau, ses caractéristiques s'affichent,
    /// on choisit le circuit puis « ALE ! ». Souris, tactile, clavier (flèches, Entrée) et manette.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [Serializable]
        public struct PilotEntry
        {
            public string name;
            public string vehicle;
            public string tagline;
            public string vehicleKey;
            public string power;
            [Range(0f, 1f)] public float speed, acceleration, handling;
            public GameObject model;
        }

        [Serializable]
        public struct TrackEntry
        {
            public string scene;
            public string title;
            public Button button;
            public RectTransform card;
            public Outline frame;
        }

        [SerializeField] private PilotEntry[] pilots;
        [SerializeField] private TrackEntry[] tracks;
        [SerializeField] private int defaultPilot = 5;

        [Header("Interface")]
        [SerializeField] private Text pilotName;
        [SerializeField] private Text vehicleName;
        [SerializeField] private Text tagline;
        [SerializeField] private Text pilotCounter;
        [SerializeField] private Text trackTitle;
        [SerializeField] private RectTransform speedBar, accelBar, handlingBar;
        [SerializeField] private Button previousButton, nextButton, goButton;
        [Header("Garage")]
        [SerializeField] private Text gourdesText;
        [SerializeField] private Text powerText;
        [Tooltip("Vitesse, accélération, maniabilité.")]
        [SerializeField] private Button[] upgradeButtons;
        [SerializeField] private Text[] upgradeLabels;

        [Header("Plateau")]
        [SerializeField] private Transform turntable;
        [SerializeField] private float spinSpeed = 22f;
        [SerializeField] private AudioClip music;

        private int _pilot, _track;
        private float _pop = 1f;
        private float _speed, _accel, _handling;
        private bool _loading;

        private void Awake()
        {
            _pilot = GameSettings.Pilot;
            if (_pilot < 0 || _pilot >= pilots.Length) _pilot = Mathf.Clamp(defaultPilot, 0, pilots.Length - 1);
            _track = Mathf.Max(0, Array.FindIndex(tracks, t => t.scene == GameSettings.Track));

            previousButton.onClick.AddListener(() => ChangePilot(-1));
            nextButton.onClick.AddListener(() => ChangePilot(1));
            goButton.onClick.AddListener(Go);
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int stat = i;
                upgradeButtons[i].onClick.AddListener(() => BuyUpgrade(stat));
            }
            for (int i = 0; i < tracks.Length; i++)
            {
                int index = i;
                tracks[i].button.onClick.AddListener(() => SelectTrack(index));
            }

            if (music != null)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.clip = music;
                s.loop = true;
                s.volume = 0.5f;
                s.Play();
            }
            ShowPilot();
            ShowTrack();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (turntable != null) turntable.Rotate(0f, spinSpeed * dt, 0f, Space.World);

            PilotEntry p = pilots[_pilot];
            float k = 1f - Mathf.Exp(-8f * dt);
            _speed = Mathf.Lerp(_speed, Boosted(p.speed, 0), k);
            _accel = Mathf.Lerp(_accel, Boosted(p.acceleration, 1), k);
            _handling = Mathf.Lerp(_handling, Boosted(p.handling, 2), k);
            Fill(speedBar, _speed);
            Fill(accelBar, _accel);
            Fill(handlingBar, _handling);

            if (_pop < 1f && p.model != null)
            {
                _pop = Mathf.Min(1f, _pop + dt * 4f);
                float s = 1f + 0.12f * Mathf.Sin(_pop * Mathf.PI);
                p.model.transform.localScale = Vector3.one * Mathf.SmoothStep(0.6f, 1f, _pop) * s;
            }

            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i].card == null) continue;
                float target = i == _track ? 1.06f : 0.94f;
                tracks[i].card.localScale = Vector3.one * Mathf.Lerp(tracks[i].card.localScale.x, target, k);
            }

            ReadShortcuts();
        }

        private void ReadShortcuts()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) ChangePilot(-1);
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) ChangePilot(1);
                if (kb.upArrowKey.wasPressedThisFrame) SelectTrack((_track - 1 + tracks.Length) % tracks.Length);
                if (kb.downArrowKey.wasPressedThisFrame) SelectTrack((_track + 1) % tracks.Length);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Go();
            }

            Gamepad pad = Gamepad.current;
            if (pad == null) return;
            if (pad.leftShoulder.wasPressedThisFrame) ChangePilot(-1);
            if (pad.rightShoulder.wasPressedThisFrame) ChangePilot(1);
            if (pad.startButton.wasPressedThisFrame) Go();
        }

        private static void Fill(RectTransform bar, float value)
        {
            if (bar != null) bar.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        private void ChangePilot(int delta)
        {
            _pilot = (_pilot + delta + pilots.Length) % pilots.Length;
            GameAudio.Play(Sfx.UiClick);
            ShowPilot();
        }

        private void ShowPilot()
        {
            for (int i = 0; i < pilots.Length; i++)
                if (pilots[i].model != null) pilots[i].model.SetActive(i == _pilot);

            PilotEntry p = pilots[_pilot];
            pilotName.text = p.name;
            vehicleName.text = p.vehicle;
            tagline.text = p.tagline;
            pilotCounter.text = $"{_pilot + 1} / {pilots.Length}";
            if (powerText != null) powerText.text = p.power;
            _pop = 0f;
            ShowGarage();
        }

        /// <summary>Valeur de la barre avec les améliorations achetées (+6 % par niveau).</summary>
        private float Boosted(float baseValue, int stat) =>
            Mathf.Clamp01(baseValue + 0.06f * GameSettings.Upgrade(pilots[_pilot].vehicleKey, stat));

        private void ShowGarage()
        {
            if (gourdesText != null) gourdesText.text = $"{GameSettings.Gourdes} G";
            string v = pilots[_pilot].vehicleKey;
            for (int i = 0; i < upgradeLabels.Length && i < upgradeButtons.Length; i++)
            {
                int level = GameSettings.Upgrade(v, i);
                int cost = GameSettings.UpgradeCost(level);
                upgradeLabels[i].text = cost < 0 ? "MAX" : $"+ {cost}";
                upgradeButtons[i].interactable = cost >= 0 && GameSettings.Gourdes >= cost;
            }
        }

        private void BuyUpgrade(int stat)
        {
            if (!GameSettings.TryBuyUpgrade(pilots[_pilot].vehicleKey, stat)) return;
            GameAudio.Play(Sfx.Coin);
            ShowGarage();
        }

        private void SelectTrack(int index)
        {
            if (index != _track) GameAudio.Play(Sfx.UiClick);
            _track = index;
            ShowTrack();
        }

        private void ShowTrack()
        {
            for (int i = 0; i < tracks.Length; i++)
                if (tracks[i].frame != null) tracks[i].frame.enabled = i == _track;
            trackTitle.text = tracks[_track].title;
        }

        private void Go()
        {
            if (_loading) return;
            _loading = true;
            GameAudio.Play(Sfx.CountdownGo, 0.7f);
            GameSettings.Pilot = _pilot;
            GameSettings.Track = tracks[_track].scene;
            SceneManager.LoadScene(tracks[_track].scene);
        }
    }
}
