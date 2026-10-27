namespace RallyAyiti.Kart
{
    /// <summary>Commandes d'une image. Le joueur et l'IA produisent exactement la même structure.</summary>
    public struct KartInput
    {
        public float Steer;     // -1 gauche, +1 droite
        public float Throttle;  // 0..1
        public bool Brake;
        public bool Drift;      // maintenu
        public bool UseItem;    // pressé pendant cette image
        public bool Horn;       // pressé pendant cette image
        public bool Power;      // pouvoir du pilote, pressé pendant cette image
    }

    public interface IKartInput
    {
        KartInput ReadInput();
    }
}
