using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles firearm mechanics for ranged weapon levels (14-20).
/// Features: Raycast shooting (left-click), ADS aiming (right-click hold),
/// ammo with auto-reload, muzzle flash, and recoil.
///
/// Activated by PlayerController when a ranged weapon type is equipped.
/// While active, disables melee attack logic.
/// </summary>
public class GunController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    //  CONFIGURATION
    // ════════════════════════════════════════════════════════════════════════

    [Header("Shooting")]
    [Tooltip("Damage per bullet hit.")]
    public int bulletDamage = 45;

    [Tooltip("Maximum raycast range in units.")]
    public float fireRange = 100f;

    [Tooltip("Seconds between shots (fire rate).")]
    public float fireRate = 0.12f;

    [Tooltip("Number of rays per shot (1 = single, >1 = shotgun spread).")]
    public int raysPerShot = 1;

    [Tooltip("Spread angle in degrees (0 = perfectly accurate).")]
    public float spreadAngle = 1.5f;

    [Tooltip("Spread angle when aiming down sights.")]
    public float adsSpreadAngle = 0.3f;

    [Header("Ammo")]
    [Tooltip("Magazine capacity.")]
    public int magazineSize = 80;

    [Tooltip("Total reserve ammo.")]
    public int reserveAmmo = 240;

    [Tooltip("Time in seconds to reload.")]
    public float reloadTime = 2.5f;

    [Header("Aiming (ADS)")]
    [Tooltip("Field of view when aiming down sights.")]
    public float adsFOV = 45f;

    [Tooltip("Speed of FOV transition when entering/exiting ADS.")]
    public float adsTransitionSpeed = 10f;

    [Tooltip("Camera offset when aiming (over-right-shoulder).")]
    public Vector3 adsCameraOffset = new Vector3(0.75f, 2.25f, -2.8f);

    [Tooltip("Normal third-person camera offset.")]
    public Vector3 normalCameraOffset = new Vector3(0f, 3.4f, -7.2f);

    [Header("Recoil")]
    [Tooltip("Vertical recoil per shot (degrees).")]
    public float verticalRecoil = 1.2f;

    [Tooltip("Horizontal recoil randomness (degrees).")]
    public float horizontalRecoil = 0.5f;

    [Tooltip("Recoil recovery speed.")]
    public float recoilRecovery = 8f;

    [Header("Visual Effects")]
    [Tooltip("Layers that bullets can hit.")]
    public LayerMask hitLayers = ~0;

    // ════════════════════════════════════════════════════════════════════════
    //  STATE
    // ════════════════════════════════════════════════════════════════════════

    private int currentAmmo;
    private bool isReloading;
    private float reloadTimer;
    private float fireTimer;
    private bool isAiming;
    private float currentRecoilPitch;
    private float currentRecoilYaw;
    private float normalFOV;
    private bool isActive;

    // References (set by PlayerController)
    private PlayerController playerController;
    private Camera activeCamera;
    private GameObject muzzleFlashObj;

    // HUD ammo display
    private TMPro.TextMeshProUGUI ammoText;

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Activates the gun controller with the specified weapon stats.
    /// Called by PlayerController.EquipWeaponForLevel() for ranged levels.
    /// </summary>
    public void Activate(int damage, float range, GameManager.WeaponType weaponType)
    {
        isActive = true;
        bulletDamage = damage;
        fireRange = range;

        // Configure per weapon type
        switch (weaponType)
        {
            case GameManager.WeaponType.Flamethrower:
                fireRate = 0.08f;
                raysPerShot = 5;
                spreadAngle = 10f;
                adsSpreadAngle = 6f;
                magazineSize = 200;
                reserveAmmo = 400;
                verticalRecoil = 0.3f;
                break;

            case GameManager.WeaponType.Rifle:
                fireRate = 0.1f;
                raysPerShot = 1;
                spreadAngle = 2.0f;
                adsSpreadAngle = 0.5f;
                magazineSize = 90;
                reserveAmmo = 270;
                verticalRecoil = 1.5f;
                break;

            case GameManager.WeaponType.Sniper:
                fireRate = 1.8f;
                raysPerShot = 1;
                spreadAngle = 0.5f;
                adsSpreadAngle = 0.05f;
                magazineSize = 10;
                reserveAmmo = 40;
                verticalRecoil = 5.0f;
                adsFOV = 25f;
                break;

            case GameManager.WeaponType.Explosive:
                fireRate = 2.0f;
                raysPerShot = 1;
                spreadAngle = 0.3f;
                adsSpreadAngle = 0.1f;
                magazineSize = 4;
                reserveAmmo = 12;
                verticalRecoil = 6.0f;
                break;

            default:
                fireRate = 0.15f;
                raysPerShot = 1;
                spreadAngle = 2.5f;
                adsSpreadAngle = 0.8f;
                magazineSize = 80;
                reserveAmmo = 240;
                verticalRecoil = 1.2f;
                break;
        }

        currentAmmo = magazineSize;
        isReloading = false;
        fireTimer = 0f;
        isAiming = false;

        playerController = GetComponent<PlayerController>();

        CreateAmmoHUD();
        UpdateAmmoHUD();
    }

    /// <summary>
    /// Deactivates the gun controller (switching back to melee weapon).
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
        isAiming = false;
        isReloading = false;

        if (ammoText != null)
            ammoText.gameObject.SetActive(false);

        // Restore camera
        RestoreCamera();
    }

    public bool IsActive => isActive;
    public bool IsAiming => isAiming;
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;

    // ════════════════════════════════════════════════════════════════════════
    //  UPDATE LOOP
    // ════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (!isActive) return;

        fireTimer -= Time.deltaTime;

        HandleAimInput();
        HandleFireInput();
        HandleReload();
        UpdateRecoilRecovery();
        UpdateADSCamera();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  AIM (Right-Click Hold)
    // ════════════════════════════════════════════════════════════════════════

    private void HandleAimInput()
    {
        // Right mouse button = aim
        bool aimPressed = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (aimPressed && !isAiming)
            EnterADS();
        else if (!aimPressed && isAiming)
            ExitADS();
    }

    private void EnterADS()
    {
        isAiming = true;

        // Store normal FOV
        Camera cam = GetActiveCamera();
        if (cam != null)
            normalFOV = cam.fieldOfView;
    }

    private void ExitADS()
    {
        isAiming = false;
        RestoreCamera();
    }

    private void UpdateADSCamera()
    {
        Camera cam = GetActiveCamera();
        if (cam == null) return;

        // Smooth FOV transition
        float targetFOV = isAiming ? adsFOV : (normalFOV > 10f ? normalFOV : 60f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, adsTransitionSpeed * Time.deltaTime);

        // Move third-person camera to over-shoulder position when aiming
        CameraController orbitCtrl = cam.GetComponent<CameraController>();
        if (orbitCtrl != null)
        {
            Vector3 targetOffset = isAiming ? adsCameraOffset : normalCameraOffset;
            orbitCtrl.offset = Vector3.Lerp(orbitCtrl.offset, targetOffset, adsTransitionSpeed * Time.deltaTime);
        }
    }

    private void RestoreCamera()
    {
        Camera cam = GetActiveCamera();
        if (cam == null) return;

        if (normalFOV > 10f)
            cam.fieldOfView = normalFOV;

        CameraController orbitCtrl = cam.GetComponent<CameraController>();
        if (orbitCtrl != null)
            orbitCtrl.offset = normalCameraOffset;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FIRE (Left-Click)
    // ════════════════════════════════════════════════════════════════════════

    private void HandleFireInput()
    {
        if (isReloading) return;

        // Left mouse = fire
        bool fireHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (!fireHeld) return;

        if (currentAmmo <= 0)
        {
            StartReload();
            return;
        }

        if (fireTimer > 0f) return;

        Fire();
    }

    private void Fire()
    {
        fireTimer = fireRate;
        currentAmmo--;
        UpdateAmmoHUD();

        Camera cam = GetActiveCamera();
        if (cam == null) return;

        // Determine spread
        float spread = isAiming ? adsSpreadAngle : spreadAngle;

        GameManager.WeaponType wType = GameManager.Instance != null
            ? GameManager.Instance.GetWeaponTypeForLevel(GameManager.Instance.currentLevel)
            : GameManager.WeaponType.Rifle;

        for (int i = 0; i < raysPerShot; i++)
        {
            // Apply spread to ray direction
            Vector3 forward = cam.transform.forward;
            if (spread > 0.01f)
            {
                forward = Quaternion.Euler(
                    Random.Range(-spread, spread),
                    Random.Range(-spread, spread),
                    0f
                ) * forward;
            }

            if (Physics.Raycast(cam.transform.position, forward, out RaycastHit hit,
                fireRange, hitLayers, QueryTriggerInteraction.Ignore))
            {
                // Damage target
                EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeDamage(bulletDamage, byPlayer: true);
                }
                else
                {
                    Actor actor = hit.collider.GetComponentInParent<Actor>();
                    if (actor != null)
                        actor.TakeDamage(bulletDamage);
                }

                // Explosive weapons: AoE damage at impact point
                if (wType == GameManager.WeaponType.Explosive)
                {
                    float radius = GameManager.Instance != null
                        ? GameManager.Instance.GetWeaponExplosionRadiusForLevel(GameManager.Instance.currentLevel)
                        : 5f;
                    ApplyExplosionDamage(hit.point, radius);
                }

                SpawnImpactEffect(hit.point, hit.normal);
            }
        }

        // Muzzle flash
        ShowMuzzleFlash();

        // Camera recoil
        ApplyRecoil();

        // Play attack animation
        if (playerController != null)
        {
            CharacterVisualAnimationPlayer visualAnim = playerController.GetThirdPersonBody() != null
                ? playerController.GetThirdPersonBody().GetComponentInChildren<CharacterVisualAnimationPlayer>(true)
                : null;
            if (visualAnim != null)
                visualAnim.PlayAttack();
        }
    }

    private void ApplyExplosionDamage(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, hitLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            EnemyController enemy = col.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                float dist = Vector3.Distance(center, col.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / radius);
                int dmg = Mathf.RoundToInt(bulletDamage * (0.4f + 0.6f * falloff));
                enemy.TakeDamage(dmg, byPlayer: true);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RELOAD
    // ════════════════════════════════════════════════════════════════════════

    private void HandleReload()
    {
        // Manual reload with R key
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame
            && !isReloading && currentAmmo < magazineSize && reserveAmmo > 0)
        {
            StartReload();
        }

        if (!isReloading) return;

        reloadTimer -= Time.deltaTime;
        if (reloadTimer <= 0f)
            FinishReload();
    }

    private void StartReload()
    {
        if (reserveAmmo <= 0 || currentAmmo >= magazineSize) return;

        isReloading = true;
        reloadTimer = reloadTime;

        if (ammoText != null)
            ammoText.text = "RELOADING...";
    }

    private void FinishReload()
    {
        isReloading = false;

        int needed = magazineSize - currentAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        currentAmmo += toLoad;
        reserveAmmo -= toLoad;

        UpdateAmmoHUD();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RECOIL
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyRecoil()
    {
        currentRecoilPitch -= verticalRecoil * (isAiming ? 0.4f : 1f);
        currentRecoilYaw += Random.Range(-horizontalRecoil, horizontalRecoil);
    }

    private void UpdateRecoilRecovery()
    {
        if (Mathf.Abs(currentRecoilPitch) < 0.01f && Mathf.Abs(currentRecoilYaw) < 0.01f) return;

        float recover = recoilRecovery * Time.deltaTime;
        currentRecoilPitch = Mathf.Lerp(currentRecoilPitch, 0f, recover);
        currentRecoilYaw = Mathf.Lerp(currentRecoilYaw, 0f, recover);

        // Apply to player rotation
        if (playerController != null)
        {
            transform.Rotate(Vector3.up * currentRecoilYaw * Time.deltaTime);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  VISUAL EFFECTS
    // ════════════════════════════════════════════════════════════════════════

    private void ShowMuzzleFlash()
    {
        if (playerController == null || playerController.equippedWeaponObject == null) return;

        // Find or create muzzle point at weapon tip
        Transform muzzle = playerController.equippedWeaponObject.transform.Find("MuzzlePoint");
        if (muzzle == null)
        {
            // Create muzzle flash at weapon's forward tip
            if (muzzleFlashObj == null)
            {
                muzzleFlashObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                muzzleFlashObj.name = "MuzzleFlash";
                muzzleFlashObj.transform.localScale = Vector3.one * 0.08f;
                Destroy(muzzleFlashObj.GetComponent<Collider>());
                Renderer r = muzzleFlashObj.GetComponent<Renderer>();
                if (r != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                    mat.color = new Color(1f, 0.9f, 0.3f, 1f);
                    r.material = mat;
                }
            }

            muzzleFlashObj.transform.SetParent(playerController.equippedWeaponObject.transform, false);
            muzzleFlashObj.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        }

        if (muzzleFlashObj != null)
        {
            muzzleFlashObj.SetActive(true);
            CancelInvoke(nameof(HideMuzzleFlash));
            Invoke(nameof(HideMuzzleFlash), 0.05f);
        }
    }

    private void HideMuzzleFlash()
    {
        if (muzzleFlashObj != null)
            muzzleFlashObj.SetActive(false);
    }

    private void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        // Simple impact spark (created procedurally)
        GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        impact.name = "BulletImpact";
        impact.transform.position = position;
        impact.transform.localScale = Vector3.one * 0.06f;
        Destroy(impact.GetComponent<Collider>());

        Renderer r = impact.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(1f, 0.7f, 0.2f, 1f);
            r.material = mat;
        }

        Destroy(impact, 0.1f);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HUD
    // ════════════════════════════════════════════════════════════════════════

    private void CreateAmmoHUD()
    {
        if (ammoText != null) { ammoText.gameObject.SetActive(true); return; }

        // Create ammo display on HUD canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject ammoObj = new GameObject("AmmoText");
        ammoObj.transform.SetParent(canvas.transform, false);
        ammoText = ammoObj.AddComponent<TMPro.TextMeshProUGUI>();
        ammoText.fontSize = 24;
        ammoText.color = Color.white;
        ammoText.alignment = TMPro.TextAlignmentOptions.BottomRight;

        RectTransform rt = ammoText.rectTransform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-20f, 20f);
        rt.sizeDelta = new Vector2(200f, 50f);
    }

    private void UpdateAmmoHUD()
    {
        if (ammoText == null) return;
        ammoText.text = $"{currentAmmo} / {reserveAmmo}";
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private Camera GetActiveCamera()
    {
        if (playerController != null)
        {
            Camera c = playerController.ActiveCamera;
            if (c != null) return c;
        }
        return Camera.main;
    }
}
