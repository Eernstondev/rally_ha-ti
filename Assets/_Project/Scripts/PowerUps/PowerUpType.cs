namespace RallyAyiti.PowerUps
{
    /// <summary>Bonus des caisses de marché (voir le document de conception).</summary>
    public enum PowerUpType
    {
        None,
        /// <summary>Noix de coco lancée tout droit, rebondit sur les murs.</summary>
        Kokoye,
        /// <summary>Mangues laissées derrière soi, font déraper.</summary>
        MangoTonbe,
        /// <summary>Trois noix de coco en éventail.</summary>
        TwaKokoye,
        /// <summary>Fusée qui suit le kart de devant.</summary>
        Fize,
        /// <summary>Peau de banane posée derrière : tête-à-queue.</summary>
        PoBannann,
        /// <summary>Souffle de conque : onde qui repousse tout autour.</summary>
        Lanbi,
        /// <summary>Café haïtien : 3 s de vitesse.</summary>
        Kafe,
        /// <summary>Bloque le prochain projectile ou piège.</summary>
        Parasol,
        /// <summary>Orage : tous les karts devant sont ralentis.</summary>
        Loraj,
        /// <summary>Jet de pikliz : brouille la vue du kart de devant.</summary>
        Pikliz,
        /// <summary>Pilote automatique en super vitesse, invulnérable.</summary>
        TaptapFou,
    }
}
