using RallyAyiti.Kart;
using UnityEngine;

namespace RallyAyiti.Race
{
    /// <summary>
    /// Trigger posé en travers de la piste. Tous les checkpoints sont enfants d'un même parent,
    /// dans l'ordre du tour ; le premier est la ligne d'arrivée. L'index est donné par le RaceManager.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        public int Index { get; internal set; }

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (KartBodyLink.TryGetKart(other, out KartController kart) && kart.TryGetComponent(out LapTracker tracker))
                tracker.OnCheckpoint(this);
        }
    }
}
