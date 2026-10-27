using RallyAyiti.Kart;
using RallyAyiti.Core;
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
#if UNITY_EDITOR
            // Test automatique de l'éditeur : épreuve de carrière imposée (« série,épreuve »).
            string forced = UnityEditor.SessionState.GetString("RallyAyiti.TestEvent", "");
            string[] parts = forced.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int s) && int.TryParse(parts[1], out int e)) Career.Current = (s, e);
#endif
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
            ApplyCareerEvent(chosen);
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
                FitCameraToVehicle(chaseCamera, Player);
                chaseCamera.PreviousStateIsValid = false;
            }
            if (cameraFx != null) cameraFx.Kart = Player;

            Camera main = Camera.main;
            if (main != null)
                main.transform.SetPositionAndRotation(target.position - target.forward * 8f + Vector3.up * 3.5f, target.rotation);
        }

        /// <summary>
        /// Épreuve de carrière : nombre de tours ; contre la montre = seul en piste ; duel = le joueur contre le boss
        /// (un peu plus rapide), les autres karts sont retirés avant que le RaceManager ne compte les concurrents.
        /// </summary>
        private void ApplyCareerEvent(int chosen)
        {
            EventDef ev = Career.CurrentEvent;
            if (ev == null) return;
            RaceManager race = FindAnyObjectByType<RaceManager>();
            if (race != null) race.SetTotalLaps(ev.Laps);

            int boss = -1;
            if (ev.Kind == EventKind.Dwel)
            {
                boss = ev.Boss >= 0 && ev.Boss < karts.Length && ev.Boss != chosen ? ev.Boss : (chosen + 1) % karts.Length;
                if (karts[boss].TryGetComponent(out AIDriver ai)) ai.SetSkill(1.05f);
            }
            if (ev.Kind != EventKind.KontLamont && ev.Kind != EventKind.Dwel) return;
            for (int i = 0; i < karts.Length; i++)
                if (i != chosen && i != boss) karts[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Caméra proche façon Beach Buggy, mais qui recule et monte pour les grands véhicules (le tap-tap ne doit pas
        /// boucher la vue) : réglée d'après la hauteur et la longueur réelles du véhicule du joueur.
        /// </summary>
        private static void FitCameraToVehicle(CinemachineCamera cam, KartController kart)
        {
            var follow = cam.GetComponent<CinemachineFollow>();
            var composer = cam.GetComponent<CinemachineRotationComposer>();
            if (follow == null) return;
            Renderer[] renderers = kart.GetComponentsInChildren<Renderer>();
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            float height = b.size.y;
            float length = Mathf.Max(b.size.x, b.size.z);
            // Référence : buggy (~1,9 m de haut, ~3,5 m de long) avec la caméra (0 ; 1,45 ; -3,7).
            float up = Mathf.Max(0f, height - 1.9f);
            float back = Mathf.Max(0f, length - 3.5f);
            follow.FollowOffset = new Vector3(0f, 1.45f + up * 1.1f, -3.7f - back * 0.9f - up * 1.2f);
            if (composer != null) composer.TargetOffset = new Vector3(0f, 0.95f + up * 0.7f, 5f);
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
