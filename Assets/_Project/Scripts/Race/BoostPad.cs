using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.Race
{
    /// <summary>Plaque d'accélération peinte au sol.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BoostPad : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float duration = 0.8f;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (KartBodyLink.TryGetKart(other, out KartController kart)) kart.ApplyBoost(duration);
        }
    }
}
