using RallyAyiti.Kart;
using RallyAyiti.Race;
using Unity.Cinemachine;
using UnityEngine;

namespace RallyAyiti.Core
{
    /// <summary>
    /// Avant tout le reste : donne le volant au pilote choisi au menu, l'IA aux autres,
    /// et place le joueur en fond de grille avec la caméra derrière lui.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class RaceSetup : MonoBehaviour
    {
        [Tooltip("Karts dans l'ordre du catalogue de pilotes du menu.")]
        [SerializeField] private KartController[] karts;
        [Tooltip("Modèle de véhicule de chaque kart (clé des améliorations du garage).")]
        [SerializeField] private string[] vehicles;
        [SerializeField] private CinemachineCamera chaseCamera;
        [SerializeField] private KartCameraFx cameraFx;
        [Tooltip("Pilote utilisé quand on lance la scène sans passer par le menu.")]
        [SerializeField] private int defaultPilot = 5;
        [Tooltip("Kart dont la place de départ (fond de grille) revient au joueur.")]
        [SerializeField] private int playerGridSlot = 5;

        public KartController Player { get; private set; }

        private void Awake()
        {
            if (karts == null || karts.Length == 0) return;
            int chosen = GameSettings.Pilot;
            if (chosen < 0 || chosen >= karts.Length) chosen = Mathf.Clamp(defaultPilot, 0, karts.Length - 1);

            int slot = Mathf.Clamp(playerGridSlot, 0, karts.Length - 1);
            if (chosen != slot) SwapStart(karts[chosen].transform, karts[slot].transform);

            for (int i = 0; i < karts.Length; i++)
            {
                KartController kart = karts[i];
                bool isPlayer = i == chosen;
                var human = kart.GetComponent<PlayerKartInput>();
                var ai = kart.GetComponent<AIDriver>();
                if (human != null) human.enabled = isPlayer;
                if (ai != null) ai.enabled = !isPlayer;
                kart.SetInput(isPlayer ? (IKartInput)human : ai);
                if (kart.TryGetComponent(out LapTracker tracker)) tracker.SetPlayer(isPlayer);
            }

            Player = karts[chosen];
            if (vehicles != null && chosen < vehicles.Length)
            {
                string v = vehicles[chosen];
                Player.SetUpgrades(GameSettings.Upgrade(v, 0), GameSettings.Upgrade(v, 1), GameSettings.Upgrade(v, 2));
            }
            Transform target = Player.transform;
            if (chaseCamera != null)
            {
                chaseCamera.Follow = target;
                chaseCamera.LookAt = target;
                chaseCamera.PreviousStateIsValid = false;
            }
            if (cameraFx != null) cameraFx.Kart = Player;

            Camera main = Camera.main;
            if (main != null)
                main.transform.SetPositionAndRotation(target.position - target.forward * 8f + Vector3.up * 3.5f, target.rotation);
        }

        private static void SwapStart(Transform a, Transform b)
        {
            a.GetPositionAndRotation(out Vector3 pa, out Quaternion ra);
            b.GetPositionAndRotation(out Vector3 pb, out Quaternion rb);
            a.SetPositionAndRotation(pb, rb);
            b.SetPositionAndRotation(pa, ra);
        }
    }
}
