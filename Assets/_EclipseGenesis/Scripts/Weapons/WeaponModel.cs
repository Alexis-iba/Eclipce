using UnityEngine;

namespace EclipseGenesis.Weapons
{
    /// <summary>
    /// Sockets and moving parts of a first-person weapon model. Any replacement model (e.g. a real EG-47 FBX)
    /// only has to provide these transforms for the controller, effects and procedural animation to keep working.
    /// </summary>
    public sealed class WeaponModel : MonoBehaviour
    {
        [Tooltip("Barrel tip: muzzle flash, tracer origin.")]
        public Transform muzzle;
        [Tooltip("Point that must sit on the camera axis when aiming down sights.")]
        public Transform sight;
        [Tooltip("Ejection port for casings.")]
        public Transform shellEject;
        [Tooltip("Magazine moved during reload.")]
        public Transform magazine;
        [Tooltip("Left hand group that follows the magazine during reload.")]
        public Transform leftHand;
        [Tooltip("Short-lived flash geometry, toggled per shot.")]
        public GameObject muzzleFlash;
        public Light muzzleLight;
    }
}
