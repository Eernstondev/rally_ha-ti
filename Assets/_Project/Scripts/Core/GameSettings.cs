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
