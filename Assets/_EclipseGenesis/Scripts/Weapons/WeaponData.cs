using UnityEngine;

namespace EclipseGenesis.Weapons
{
    /// <summary>Balance and presentation data of one weapon. Every weapon shares WeaponController; only data differs.</summary>
    [CreateAssetMenu(menuName = "Eclipse Genesis/Weapons/Weapon Data", fileName = "WeaponData")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string id = "EG-47";
        public string displayName = "EG-47";
        public string description = "Rifle de asalto";
        public GameObject viewModelPrefab;

        [Header("Damage")]
        public float damage = 28f;
        [Tooltip("Full damage up to this distance (m).")]
        public float effectiveRange = 45f;
        public float maxRange = 200f;
        [Range(0f, 1f)] public float minDamageFactor = 0.6f;
        public float headshotMultiplier = 2f;

        [Header("Fire")]
        public float roundsPerMinute = 650f;
        public bool automatic = true;
        public int pellets = 1;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int reserveAmmo = 120;
        public float reloadTime = 2.3f;
        [Tooltip("Extra time when the magazine was completely empty (chambering).")]
        public float emptyReloadExtra = 0.4f;

        [Header("Accuracy (degrees)")]
        public float hipSpread = 2.2f;
        public float adsSpread = 0.25f;
        public float movingSpreadAdd = 1.6f;
        public float spreadPerShot = 0.35f;
        public float maxShotSpread = 2.5f;
        public float spreadRecovery = 6f;

        [Header("Recoil (degrees per shot)")]
        public float recoilVertical = 0.55f;
        public float recoilHorizontal = 0.22f;
        [Tooltip("Fraction of the vertical kick the camera returns on its own.")]
        [Range(0f, 1f)] public float recoilRecoveryFraction = 0.55f;
        public float recoilRecoverySpeed = 9f;
        [Range(0f, 1f)] public float adsRecoilMultiplier = 0.7f;

        [Header("Aiming")]
        public float adsTime = 0.22f;
        [Range(0.3f, 1f)] public float adsFovMultiplier = 0.78f;
        [Range(0.2f, 1f)] public float adsSensitivity = 0.75f;

        [Header("Handling")]
        public float sprintToFireTime = 0.12f;
        public float equipTime = 0.45f;

        [Header("Audio")]
        public AudioClip fireClip;
        public AudioClip reloadClip;
        public AudioClip dryFireClip;
        public float noiseRadius = 32f;

        public float SecondsPerShot => 60f / Mathf.Max(1f, roundsPerMinute);
    }
}
