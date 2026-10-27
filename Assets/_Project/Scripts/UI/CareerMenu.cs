using RallyAyiti.Audio;
using RallyAyiti.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RallyAyiti.UI
{
    /// <summary>
    /// Navigation des menus façon Beach Buggy : accueil → centre (garage, course rapide, carrière) → séries → épreuves.
    /// Les cartes de séries et les tuiles d'épreuves sont construites à l'exécution à partir de <see cref="Career"/>.
    /// </summary>
    public sealed class CareerMenu : MonoBehaviour
    {
        public enum Screen { Home, Hub, Garage, Quick, Series, Events }

        [Header("Écrans")]
        [SerializeField] private GameObject home;
        [SerializeField] private GameObject hub;
        [SerializeField] private GameObject garage;
        [SerializeField] private GameObject quick;
        [SerializeField] private GameObject series;
        [SerializeField] private GameObject events;
        [Tooltip("Barre du haut (retour, étoiles, gourdes), cachée sur l'accueil.")]
        [SerializeField] private GameObject topBar;

        [Header("Boutons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button garageButton;
        [SerializeField] private Button quickButton;
        [SerializeField] private Button careerButton;

        [Header("Barre du haut")]
        [SerializeField] private Text starsText;
        [SerializeField] private Text gourdesText;

        [Header("Séries et épreuves")]
        [SerializeField] private RectTransform seriesRow;
        [SerializeField] private RectTransform eventsRow;
        [SerializeField] private Text seriesTitle;
        [SerializeField] private GameObject detail;
        [SerializeField] private Text detailTitle;
        [SerializeField] private Text detailInfo;
        [SerializeField] private Image[] detailStars;
        [SerializeField] private Button detailGo;

        [Header("Style")]
        [SerializeField] private Font font;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite starSprite;
        [SerializeField] private Sprite lockSprite;
        [Tooltip("Une image par série, dans l'ordre de Career.Series.")]
        [SerializeField] private Sprite[] seriesCards;

        private static readonly Color Blue = new(0.13f, 0.38f, 0.86f);
        private static readonly Color Gold = new(1f, 0.82f, 0.15f);
        private static readonly Color Paper = new(0.96f, 0.93f, 0.85f);
        private static readonly Color Dim = new(1f, 1f, 1f, 0.22f);
        private static readonly Color Locked = new(0.3f, 0.34f, 0.45f);

        private Screen _screen;
        private int _series, _event = -1;
        private bool _loading;

        public Screen Current => _screen;

        private void Start()
        {
            playButton.onClick.AddListener(() => Go(Screen.Hub));
            backButton.onClick.AddListener(Back);
            garageButton.onClick.AddListener(() => Go(Screen.Garage));
            quickButton.onClick.AddListener(() => Go(Screen.Quick));
            careerButton.onClick.AddListener(() => Go(Screen.Series));
            detailGo.onClick.AddListener(StartEvent);

            // Retour d'une course de carrière : on revient directement sur la série jouée.
            if (Career.Current is { } c)
            {
                _series = c.series;
                Show(Screen.Events);
            }
            else Show(Screen.Home);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
            if (_screen == Screen.Home && kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Go(Screen.Hub);
            Gamepad pad = Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame) Back();
        }

        private void Go(Screen screen)
        {
            GameAudio.Play(Sfx.UiClick);
            Show(screen);
        }

        private void Back()
        {
            switch (_screen)
            {
                case Screen.Events: Go(Screen.Series); break;
                case Screen.Garage:
                case Screen.Quick:
                case Screen.Series: Go(Screen.Hub); break;
                case Screen.Hub: Go(Screen.Home); break;
            }
        }

        public void Show(Screen screen)
        {
            _screen = screen;
            home.SetActive(screen == Screen.Home);
            hub.SetActive(screen == Screen.Hub);
            garage.SetActive(screen == Screen.Garage);
            quick.SetActive(screen == Screen.Quick);
            series.SetActive(screen == Screen.Series);
            events.SetActive(screen == Screen.Events);
            topBar.SetActive(screen != Screen.Home);
            RefreshTopBar();
            if (screen == Screen.Series) BuildSeries();
            if (screen == Screen.Events) BuildEvents();
        }

        public void RefreshTopBar()
        {
            if (starsText != null) starsText.text = $"{Career.TotalStars}/{Career.Series.Length * Career.StarsPerSeries}";
            if (gourdesText != null) gourdesText.text = $"{GameSettings.Gourdes} G";
        }

        // ---------- Séries ----------

        private void BuildSeries()
        {
            Clear(seriesRow);
            for (int s = 0; s < Career.Series.Length; s++)
            {
                SeriesDef def = Career.Series[s];
                bool open = Career.SeriesUnlocked(s);
                RectTransform card = Box(seriesRow, def.Title, new Vector2(440f, 580f), open ? Blue : Locked);
                Label(card, def.Title.ToUpperInvariant(), 34, new Vector2(0f, -24f), new Vector2(420f, 60f), TextAnchor.UpperCenter, Paper, new Vector2(0.5f, 1f));

                Image picture = Icon(card, s < seriesCards.Length ? seriesCards[s] : null, new Vector2(0f, 20f), new Vector2(390f, 300f), new Vector2(0.5f, 0.5f));
                picture.color = open ? Color.white : new Color(0.35f, 0.35f, 0.4f);
                picture.preserveAspect = true;

                if (open)
                {
                    Icon(card, starSprite, new Vector2(-70f, 52f), new Vector2(64f, 64f), new Vector2(0.5f, 0f)).color = Gold;
                    Label(card, $"{Career.SeriesStars(s)}/{Career.StarsPerSeries}", 40, new Vector2(40f, 52f), new Vector2(220f, 64f), TextAnchor.MiddleCenter, Paper, new Vector2(0.5f, 0f));
                    int index = s;
                    AddButton(card, () => { _series = index; Go(Screen.Events); });
                }
                else
                {
                    Icon(card, lockSprite, new Vector2(0f, 20f), new Vector2(170f, 170f), new Vector2(0.5f, 0.5f));
                    Icon(card, starSprite, new Vector2(-50f, 52f), new Vector2(56f, 56f), new Vector2(0.5f, 0f)).color = Gold;
                    Label(card, $"{def.StarsToUnlock}", 40, new Vector2(30f, 52f), new Vector2(160f, 64f), TextAnchor.MiddleCenter, Paper, new Vector2(0.5f, 0f));
                }
            }
        }

        // ---------- Épreuves ----------

        private void BuildEvents()
        {
            SeriesDef def = Career.Series[_series];
            if (seriesTitle != null) seriesTitle.text = $"{def.Title.ToUpperInvariant()}   ·   {Career.SeriesStars(_series)}/{Career.StarsPerSeries}";
            Clear(eventsRow);
            for (int e = 0; e < def.Events.Length; e++)
            {
                bool open = Career.EventUnlocked(_series, e);
                RectTransform tile = Box(eventsRow, $"Epre{e + 1}", new Vector2(250f, 250f), open ? Blue : Locked);
                int stars = Career.Stars(_series, e);
                for (int k = 0; k < 3; k++)
                    Icon(tile, starSprite, new Vector2(-62f + k * 62f, -18f), new Vector2(58f, 58f), new Vector2(0.5f, 1f)).color = k < stars ? Gold : Dim;
                if (open)
                {
                    Label(tile, (e + 1).ToString(), 92, new Vector2(0f, -8f), new Vector2(240f, 120f), TextAnchor.MiddleCenter, Paper, new Vector2(0.5f, 0.5f));
                    Label(tile, Career.KindLabel(def.Events[e].Kind), 22, new Vector2(0f, 22f), new Vector2(240f, 34f), TextAnchor.MiddleCenter, Gold, new Vector2(0.5f, 0f));
                    int index = e;
                    AddButton(tile, () => SelectEvent(index));
                }
                else
                {
                    Icon(tile, lockSprite, new Vector2(0f, -14f), new Vector2(110f, 110f), new Vector2(0.5f, 0.5f));
                }
            }
            // Par défaut : l'épreuve qui vient d'être jouée, sinon la première encore incomplète.
            int pick = 0;
            for (int e = 0; e < def.Events.Length; e++)
            {
                if (!Career.EventUnlocked(_series, e)) break;
                pick = e;
                if (Career.Stars(_series, e) < 3) break;
            }
            SelectEvent(Career.Current is { } c && c.series == _series ? c.ev : pick, true);
        }

        private void SelectEvent(int ev, bool silent = false)
        {
            if (!silent) GameAudio.Play(Sfx.UiClick);
            _event = ev;
            EventDef def = Career.Series[_series].Events[ev];
            detail.SetActive(true);
            detailTitle.text = $"{ev + 1}. {def.Title.ToUpperInvariant()}";
            string track = def.Scene == Career.Katye ? "Katye a" : "Plaj Labadi";
            detailInfo.text = $"<color=#f6c22b>{Career.KindLabel(def.Kind)}</color>   ·   {track}   ·   {def.Laps} tou\n" +
                              $"{Career.GoalText(def)}\nRekonpans : {def.Reward} goud";
            int stars = Career.Stars(_series, ev);
            for (int k = 0; k < detailStars.Length; k++) detailStars[k].color = k < stars ? Gold : Dim;
        }

        private void StartEvent()
        {
            if (_loading || _event < 0) return;
            _loading = true;
            GameAudio.Play(Sfx.CountdownGo, 0.7f);
            EventDef def = Career.Series[_series].Events[_event];
            Career.Current = (_series, _event);
            if (!Career.PilotUnlocked(GameSettings.Pilot)) GameSettings.Pilot = 5;
            GameSettings.Track = def.Scene;
            SceneManager.LoadScene(def.Scene);
        }

        // ---------- Briques ----------

        private static void Clear(Transform parent)
        {
            // Détacher avant de détruire : la mise en page ne compte plus les anciennes tuiles dès cette image.
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private RectTransform Box(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = panelSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -8f);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;
            return rt;
        }

        private static Image Icon(Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject("Ikon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private Text Label(Transform parent, string text, int size, Vector2 pos, Vector2 box, TextAnchor align, Color color, Vector2 anchor)
        {
            var go = new GameObject("Teks", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.7f);
            o.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        private static void AddButton(RectTransform target, UnityEngine.Events.UnityAction action)
        {
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = target.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(action);
        }
    }
}
