using UnityEngine;

namespace RallyAyiti.PowerUps
{
    /// <summary>Trigger plus large que la noix de coco : touche le kart avant le choc physique.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KokoyeHitbox : MonoBehaviour
    {
        private KokoyeProjectile _projectile;

        private void Awake() => _projectile = GetComponentInParent<KokoyeProjectile>();

        private void OnTriggerEnter(Collider other)
        {
            if (_projectile != null) _projectile.OnHitbox(other);
        }
    }
}
