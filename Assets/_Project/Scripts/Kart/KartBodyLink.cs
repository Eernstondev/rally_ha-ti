using UnityEngine;

namespace RallyAyiti.Kart
{
    /// <summary>
    /// Posé automatiquement sur la sphère physique par KartController.
    /// Permet aux triggers (checkpoints, boosts, bonus) de retrouver le kart à partir du collider touché.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartBodyLink : MonoBehaviour
    {
        public KartController Controller { get; internal set; }

        public static bool TryGetKart(Collider other, out KartController kart)
        {
            kart = null;
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || !rb.TryGetComponent(out KartBodyLink link)) return false;
            kart = link.Controller;
            return kart != null;
        }
    }
}
