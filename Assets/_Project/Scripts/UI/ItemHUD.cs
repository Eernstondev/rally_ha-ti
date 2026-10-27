using RallyAyiti.PowerUps;
using RallyAyiti.Race;
using UnityEngine;
using UnityEngine.UI;

namespace RallyAyiti.UI
{
    /// <summary>Case du bonus tenu par le joueur, en haut au centre : icône et nom du bonus.</summary>
    public sealed class ItemHUD : MonoBehaviour
    {
        [SerializeField] private RaceManager race;
        [SerializeField] private RectTransform slot;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [Tooltip("Une icône par valeur de PowerUpType (la première, None, reste vide).")]
        [SerializeField] private Sprite[] icons;

        private PowerUpSystem _powerUps;
        private float _pop;

        private void Start()
        {
            if (race != null && race.Player != null) _powerUps = race.Player.GetComponent<PowerUpSystem>();
            if (_powerUps != null) _powerUps.Changed += OnChanged;
            OnChanged(PowerUpType.None);
        }

        private void OnDestroy()
        {
            if (_powerUps != null) _powerUps.Changed -= OnChanged;
        }

        [Tooltip("Durée de la roulette d'icônes avant d'afficher le bonus obtenu.")]
        [SerializeField] private float rollDuration = 0.8f;

        private PowerUpType _pending;
        private float _roll, _tick;

        private void OnChanged(PowerUpType type)
        {
            if (type == PowerUpType.None || rollDuration <= 0f)
            {
                _roll = 0f;
                Show(type);
                return;
            }
            // Roulette : les icônes défilent puis s'arrêtent sur le bonus tiré.
            _pending = type;
            _roll = rollDuration;
            _tick = 0f;
            if (label != null) label.text = "";
        }

        private void Show(PowerUpType type)
        {
            int i = (int)type;
            icon.enabled = type != PowerUpType.None;
            icon.sprite = icons != null && i < icons.Length ? icons[i] : null;
            if (label != null) label.text = i < PowerUpSystem.Names.Length ? PowerUpSystem.Names[i].ToUpperInvariant() : "";
            if (type != PowerUpType.None) _pop = 0.25f;
        }

        private void Update()
        {
            if (_roll > 0f)
            {
                _roll -= Time.deltaTime;
                if ((_tick -= Time.deltaTime) <= 0f && icons != null && icons.Length > 1)
                {
                    _tick = 0.07f;
                    icon.enabled = true;
                    icon.sprite = icons[Random.Range(1, icons.Length)];
                }
                if (_roll <= 0f) Show(_pending);
            }

            if (_pop <= 0f) return;
            _pop -= Time.deltaTime;
            slot.localScale = Vector3.one * (1f + Mathf.Max(0f, _pop) * 1.4f);
        }
    }
}
