using UnityEngine;

namespace RallyAyiti.Core
{
    public enum EventKind
    {
        /// <summary>Course classique : 3 étoiles pour la 1re place, 2 pour la 2e, 1 pour la 3e.</summary>
        Kous,
        /// <summary>Contre la montre, seul sur la piste : étoiles selon le temps.</summary>
        KontLamont,
        /// <summary>Ramasser des pièces de gourde pendant la course : étoiles selon le nombre.</summary>
        RanmaseGoud,
        /// <summary>Duel contre le boss de la série : le battre donne 3 étoiles et débloque ce pilote.</summary>
        Dwel,
    }

    public sealed class EventDef
    {
        public readonly string Title;
        public readonly EventKind Kind;
        public readonly string Scene;
        public readonly int Laps;
        /// <summary>Seuils des étoiles 3, 2, 1 : secondes (contre la montre) ou pièces (ramasser).</summary>
        public readonly float[] Goals;
        public readonly int Reward;
        /// <summary>Index du pilote boss (duel), -1 sinon.</summary>
        public readonly int Boss;

        public EventDef(string title, EventKind kind, string scene, int laps, int reward, float[] goals = null, int boss = -1)
        {
            Title = title; Kind = kind; Scene = scene; Laps = laps; Reward = reward; Goals = goals; Boss = boss;
        }
    }

    public sealed class SeriesDef
    {
        public readonly string Title;
        public readonly string Card;
        public readonly int StarsToUnlock;
        public readonly EventDef[] Events;

        public SeriesDef(string title, string card, int starsToUnlock, params EventDef[] events)
        {
            Title = title; Card = card; StarsToUnlock = starsToUnlock; Events = events;
        }
    }

    /// <summary>
    /// Carrière façon Beach Buggy : séries d'épreuves, jusqu'à 3 étoiles par épreuve, séries débloquées par le total
    /// d'étoiles, pilotes débloqués en battant le boss de chaque série (ou achetés au garage).
    /// </summary>
    public static class Career
    {
        public const string Labadie = "Labadie";
        public const string Katye = "Prototype";

        /// <summary>Pilotes disponibles dès le départ (index du catalogue) : Manmzèl Loudena et Ti Djo.</summary>
        private static readonly int[] StarterPilots = { 0, 5 };

        /// <summary>Prix d'un pilote au garage pour qui ne veut pas attendre le duel.</summary>
        public const int PilotPrice = 3000;

        public static readonly SeriesDef[] Series =
        {
            new("Ti Seri Lakay", "Card_Labadie.png", 0,
                new EventDef("Premye vire", EventKind.Kous, Labadie, 2, 150),
                new EventDef("Kont lamont plaj", EventKind.KontLamont, Labadie, 2, 150, new[] { 58f, 64f, 80f }),
                new EventDef("Lari Katye", EventKind.Kous, Katye, 2, 200),
                new EventDef("Goud nan lari", EventKind.RanmaseGoud, Katye, 2, 200, new[] { 20f, 14f, 8f }),
                new EventDef("Dwèl ak Nadège", EventKind.Dwel, Labadie, 3, 400, boss: 2)),
            new("Koup Kokoye", "Card_Katye.png", 8,
                new EventDef("Kous solèy kouche", EventKind.Kous, Katye, 3, 250),
                new EventDef("Goud sou plaj", EventKind.RanmaseGoud, Labadie, 3, 250, new[] { 32f, 24f, 14f }),
                new EventDef("Kont lamont Katye", EventKind.KontLamont, Katye, 2, 250, new[] { 56f, 62f, 78f }),
                new EventDef("Gwo kous plaj", EventKind.Kous, Labadie, 3, 300),
                new EventDef("Dwèl ak Bòs Wilfrid", EventKind.Dwel, Katye, 3, 600, boss: 1)),
            new("Gran Pri Ayiti", "Card_Labadie.png", 20,
                new EventDef("Labadi rapid", EventKind.KontLamont, Labadie, 3, 350, new[] { 86f, 94f, 115f }),
                new EventDef("Lari cho", EventKind.Kous, Katye, 3, 350),
                new EventDef("Lapli goud", EventKind.RanmaseGoud, Katye, 3, 350, new[] { 34f, 26f, 16f }),
                new EventDef("Final plaj", EventKind.Kous, Labadie, 4, 450),
                new EventDef("Dwèl ak Judens Konpa", EventKind.Dwel, Labadie, 4, 900, boss: 3)),
        };

        /// <summary>Épreuve en cours (null = course rapide hors carrière). Non sauvegardé : vaut pour la session.</summary>
        public static (int series, int ev)? Current;

        public static EventDef CurrentEvent => Current is { } c ? Series[c.series].Events[c.ev] : null;

        public const int StarsPerSeries = 15;

        public static int Stars(int series, int ev) => PlayerPrefs.GetInt($"RallyAyiti.Stars.{series}.{ev}", 0);

        public static int SeriesStars(int series)
        {
            int total = 0;
            for (int e = 0; e < Series[series].Events.Length; e++) total += Stars(series, e);
            return total;
        }

        public static int TotalStars
        {
            get
            {
                int total = 0;
                for (int s = 0; s < Series.Length; s++) total += SeriesStars(s);
                return total;
            }
        }

        public static bool SeriesUnlocked(int series) => TotalStars >= Series[series].StarsToUnlock;

        /// <summary>Une épreuve s'ouvre quand la précédente a au moins une étoile.</summary>
        public static bool EventUnlocked(int series, int ev) =>
            SeriesUnlocked(series) && (ev == 0 || Stars(series, ev - 1) > 0);

        /// <summary>Étoiles obtenues (0 à 3) pour un résultat de course.</summary>
        public static int Evaluate(EventDef ev, bool finished, int place, float time, int pieces)
        {
            if (!finished) return 0;
            switch (ev.Kind)
            {
                case EventKind.Kous:
                    return place switch { 1 => 3, 2 => 2, 3 => 1, _ => 0 };
                case EventKind.Dwel:
                    return place == 1 ? 3 : 0;
                case EventKind.KontLamont:
                    return time <= ev.Goals[0] ? 3 : time <= ev.Goals[1] ? 2 : time <= ev.Goals[2] ? 1 : 0;
                case EventKind.RanmaseGoud:
                    return pieces >= ev.Goals[0] ? 3 : pieces >= ev.Goals[1] ? 2 : pieces >= ev.Goals[2] ? 1 : 0;
                default:
                    return 0;
            }
        }

        /// <summary>Garde le meilleur score ; renvoie le nombre de nouvelles étoiles gagnées.</summary>
        public static int Record(int series, int ev, int stars)
        {
            int best = Stars(series, ev);
            if (stars <= best) return 0;
            PlayerPrefs.SetInt($"RallyAyiti.Stars.{series}.{ev}", stars);
            PlayerPrefs.Save();
            return stars - best;
        }

        /// <summary>Texte de l'objectif affiché avant la course et sur l'écran de résultat.</summary>
        public static string GoalText(EventDef ev) => ev.Kind switch
        {
            EventKind.Kous => "Fini 1e pou 3 zetwal, 2yèm pou 2, 3yèm pou 1",
            EventKind.Dwel => "Bat bòs la pou debloke pilòt li",
            EventKind.KontLamont => $"Fini anba {Clock(ev.Goals[0])} pou 3 zetwal",
            EventKind.RanmaseGoud => $"Ranmase {ev.Goals[0]:0} pyès pou 3 zetwal",
            _ => "",
        };

        public static string KindLabel(EventKind kind) => kind switch
        {
            EventKind.Kous => "KOUS",
            EventKind.KontLamont => "KONT LAMONT",
            EventKind.RanmaseGoud => "RANMASE GOUD",
            EventKind.Dwel => "DWÈL",
            _ => "",
        };

        public static string Clock(float seconds) => $"{(int)seconds / 60}:{seconds % 60f:00.0}";

        public static bool PilotUnlocked(int pilot)
        {
            foreach (int p in StarterPilots)
                if (p == pilot) return true;
            return PlayerPrefs.GetInt($"RallyAyiti.Pilot.{pilot}.Unlocked", 0) == 1;
        }

        public static void UnlockPilot(int pilot)
        {
            PlayerPrefs.SetInt($"RallyAyiti.Pilot.{pilot}.Unlocked", 1);
            PlayerPrefs.Save();
        }

        /// <summary>Achat d'un pilote bloqué avec des gourdes.</summary>
        public static bool TryBuyPilot(int pilot)
        {
            if (PilotUnlocked(pilot) || GameSettings.Gourdes < PilotPrice) return false;
            GameSettings.Gourdes -= PilotPrice;
            UnlockPilot(pilot);
            return true;
        }

        /// <summary>Série dont le boss débloque ce pilote, -1 s'il n'y en a pas.</summary>
        public static int BossSeries(int pilot)
        {
            for (int s = 0; s < Series.Length; s++)
                foreach (EventDef e in Series[s].Events)
                    if (e.Kind == EventKind.Dwel && e.Boss == pilot) return s;
            return -1;
        }
    }
}
