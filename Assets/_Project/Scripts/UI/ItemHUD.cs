using RallyAyiti.PowerUps;
using RallyAyiti.Race;
using UnityEngine;
using UnityEngine.UI;

namespace RallyAyiti.UI
{
    /// <summary>Case du bonus tenu par le joueur, en haut au centre.</summary>
    public sealed class ItemHUD : MonoBehaviour
    {
        [SerializeField] private RaceManager race;
        [SerializeField] private RectTransform slot;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite kokoye;
        [SerializeField] private Sprite mango;

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

        private void OnChanged(PowerUpType type)
        {
            icon.enabled = type != PowerUpType.None;
            icon.sprite = type == PowerUpType.Kokoye ? kokoye : mango;
            if (type != PowerUpType.None) _pop = 0.25f;
        }

        private void Update()
        {
            if (_pop <= 0f) return;
            _pop -= Time.deltaTime;
            slot.localScale = Vector3.one * (1f + Mathf.Max(0f, _pop) * 1.4f);
        }
    }
}
