using RallyAyiti.Audio;
using RallyAyiti.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RallyAyiti.UI
{
    /// <summary>Bouton pause (et touche Échap / Start) : fige la course et propose Kontinye, Rejwe, Meni.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        private bool _paused;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (pauseButton != null) pauseButton.onClick.AddListener(Toggle);
            if (resumeButton != null) resumeButton.onClick.AddListener(Toggle);
            if (retryButton != null) retryButton.onClick.AddListener(() => Load(SceneManager.GetActiveScene().name));
            if (menuButton != null) menuButton.onClick.AddListener(() => Load(GameSettings.MenuScene));
        }

        private void Update()
        {
            bool key = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (key) Toggle();
        }

        private void OnDestroy()
        {
            // Ne jamais laisser le jeu figé en quittant la scène.
            if (_paused) SetPaused(false);
        }

        private void Toggle()
        {
            SetPaused(!_paused);
            GameAudio.Play(Sfx.UiClick);
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (panel != null) panel.SetActive(paused);
        }

        private void Load(string scene)
        {
            SetPaused(false);
            GameAudio.Play(Sfx.UiClick);
            SceneManager.LoadScene(scene);
        }
    }
}
