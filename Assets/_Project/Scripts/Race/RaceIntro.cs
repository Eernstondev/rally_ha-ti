using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RallyAyiti.Race
{
    /// <summary>
    /// Survol cinématique avant le départ (comme Beach Buggy) : la caméra plonge du ciel vers la grille
    /// et finit derrière le joueur, puis le compte à rebours commence. Un tap ou une touche le passe.
    /// </summary>
    public sealed class RaceIntro : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera introCam;
        [Tooltip("Interface masquée pendant le survol.")]
        [SerializeField] private CanvasGroup hud;
        [SerializeField] private float duration = 3.8f;

        public IEnumerator Play(Transform target)
        {
            if (introCam == null || target == null) yield break;
            if (hud != null) hud.alpha = 0f;

            Vector3 fwd = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 p0 = target.position + fwd * 34f + right * 16f + Vector3.up * 13f;
            Vector3 p1 = target.position - fwd * 2f - right * 14f + Vector3.up * 6f;
            Vector3 p2 = target.position - fwd * 4f + Vector3.up * 1.7f;

            introCam.gameObject.SetActive(true);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                // Courbe de Bézier quadratique : grand arc autour de la grille.
                Vector3 pos = (1 - k) * (1 - k) * p0 + 2 * (1 - k) * k * p1 + k * k * p2;
                Vector3 look = target.position + Vector3.up * 1f + fwd * Mathf.Lerp(-6f, 6f, k);
                introCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                if (t > 0.3f && SkipPressed()) break;
                yield return null;
            }
            introCam.gameObject.SetActive(false);

            if (hud == null) yield break;
            for (float a = 0f; a < 1f; a += Time.deltaTime * 3f)
            {
                hud.alpha = a;
                yield return null;
            }
            hud.alpha = 1f;
        }

        private static bool SkipPressed() =>
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }
}
