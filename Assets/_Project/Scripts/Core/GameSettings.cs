using UnityEngine;

namespace RallyAyiti.Core
{
    /// <summary>Choix faits au menu (pilote, circuit), gardés entre les sessions.</summary>
    public static class GameSettings
    {
        public const string MenuScene = "Meni";
        private const string PilotKey = "RallyAyiti.Pilot";
        private const string TrackKey = "RallyAyiti.Track";

        /// <summary>Index du pilote dans le catalogue, -1 si aucun choix n'a encore été fait.</summary>
        public static int Pilot
        {
            get => PlayerPrefs.GetInt(PilotKey, -1);
            set
            {
                PlayerPrefs.SetInt(PilotKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Gourdes du joueur, gagnées en course, dépensées au garage.</summary>
        public static int Gourdes
        {
            get => PlayerPrefs.GetInt("RallyAyiti.Gourdes", 0);
            set
            {
                PlayerPrefs.SetInt("RallyAyiti.Gourdes", Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public const int MaxUpgrade = 4;
        private static readonly int[] UpgradeCosts = { 400, 900, 1600, 2500 };

        /// <summary>Prix du prochain niveau, ou -1 si le maximum est atteint.</summary>
        public static int UpgradeCost(int currentLevel) =>
            currentLevel >= 0 && currentLevel < UpgradeCosts.Length ? UpgradeCosts[currentLevel] : -1;

        /// <summary>Niveau d'amélioration (0 à 4) d'une stat (0 vitesse, 1 accélération, 2 maniabilité) d'un véhicule.</summary>
        public static int Upgrade(string vehicle, int stat) => PlayerPrefs.GetInt($"RallyAyiti.Up.{vehicle}.{stat}", 0);

        /// <summary>Achète le niveau suivant si les gourdes suffisent.</summary>
        public static bool TryBuyUpgrade(string vehicle, int stat)
        {
            int level = Upgrade(vehicle, stat);
            int cost = UpgradeCost(level);
            if (cost < 0 || Gourdes < cost) return false;
            Gourdes -= cost;
            PlayerPrefs.SetInt($"RallyAyiti.Up.{vehicle}.{stat}", level + 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Nom de la scène du circuit choisi.</summary>
        public static string Track
        {
            get => PlayerPrefs.GetString(TrackKey, "Labadie");
            set
            {
                PlayerPrefs.SetString(TrackKey, value);
                PlayerPrefs.Save();
            }
        }
    }
}
