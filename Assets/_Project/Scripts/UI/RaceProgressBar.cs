using System.Collections.Generic;
using RallyAyiti.Race;
using UnityEngine;
using UnityEngine.UI;

namespace RallyAyiti.UI
{
    /// <summary>
    /// Barre de progression DEPA → FINI de toute la course, avec une pastille par pilote (couleur de son casque).
    /// La pastille du joueur est plus grosse et passe au premier plan.
    /// </summary>
    public sealed class RaceProgressBar : MonoBehaviour
    {
        [SerializeField] private RaceManager race;
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Sprite markerSprite;
        [SerializeField] private string[] pilotNames;
        [SerializeField] private Color[] pilotColors;
        [SerializeField] private float markerSize = 30f;
        [SerializeField] private float playerMarkerSize = 46f;

        private readonly Dictionary<LapTracker, RectTransform> _markers = new();

        private void LateUpdate()
        {
            if (race == null || track == null || race.CheckpointCount == 0) return;
            float total = race.TotalLaps * race.CheckpointCount;
            float width = track.rect.width;
            LapTracker player = race.Player;

            IReadOnlyList<LapTracker> racers = race.Standings;
            for (int i = 0; i < racers.Count; i++)
            {
                LapTracker r = racers[i];
                if (!_markers.TryGetValue(r, out RectTransform marker)) marker = _markers[r] = CreateMarker(r);
                float t = r.Finished ? 1f : Mathf.Clamp01(r.Progress / total);
                marker.anchoredPosition = new Vector2(t * width, 0f);
                float size = r == player ? playerMarkerSize : markerSize;
                marker.sizeDelta = new Vector2(size, size);
                if (r == player)
                {
                    marker.SetAsLastSibling();
                    if (fill != null) fill.anchorMax = new Vector2(t, 1f);
                }
            }
        }

        private RectTransform CreateMarker(LapTracker racer)
        {
            var go = new GameObject("Pilot_" + racer.DisplayName, typeof(RectTransform));
            go.transform.SetParent(track, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var image = go.AddComponent<Image>();
            image.sprite = markerSprite;
            image.raycastTarget = false;
            image.color = ColorOf(racer.DisplayName);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = racer == race.Player ? Color.white : new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(3f, -3f);
            return rt;
        }

        private Color ColorOf(string pilot)
        {
            if (pilotNames != null && pilotColors != null)
                for (int i = 0; i < pilotNames.Length && i < pilotColors.Length; i++)
                    if (pilotNames[i] == pilot) return pilotColors[i];
            return Color.white;
        }
    }
}
