using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] float shootRange;
    [SerializeField] int damage;
    [SerializeField] float shootDelay;
    [SerializeField] LayerMask targetLayer;

    [Header("Reload")]
    [SerializeField] int maxAmmo;
    [SerializeField] float reloadDelay;

    NetworkVariable<int> ammo = new NetworkVariable<int>();
    NetworkVariable<bool> isReloading = new NetworkVariable<bool>();

    bool isShooting;
    bool canShoot;

    PlayerMovement myMovement;
    PlayerUIManager myUiManager;
    PlayerModelManager myModelManager;
    Animator myAnimator;

    public override void OnNetworkSpawn()
    {
        myMovement = GetComponent<PlayerMovement>();
        myUiManager = GetComponent<PlayerUIManager>();
        myModelManager = GetComponent<PlayerModelManager>();
        myAnimator = GetComponentInChildren<Animator>();

        if (IsServer)
        {
            ammo.Value = maxAmmo;
        }

        ammo.OnValueChanged += OnAmmoChanged;
        isReloading.OnValueChanged += OnReloadingChanged;
        OnAmmoChanged(0, ammo.Value);

        // Applies the current team since OnValueChanged only fires on a change
        NetworkVariable<GameManager.Team> team = myMovement.GetPlayerTeam();
        team.OnValueChanged += OnTeamChanged;
        SetCanShoot(team.Value == GameManager.Team.Hunters);
    }

    void OnTeamChanged(GameManager.Team previousTeam, GameManager.Team newTeam)
    {
        SetCanShoot(newTeam == GameManager.Team.Hunters);
    }

    // Only the team decides this, pausing goes through PlayerMovement.IsInputBlocked() instead
    void SetCanShoot(bool newCanShoot)
    {
        canShoot = newCanShoot;

        // Updates both ways so the gun and ammo text get hidden again if the player stops being a hunter
        myModelManager.SetGunModelsActive(IsOwner && canShoot, !IsOwner && canShoot);
        myUiManager.SetAmmoTextActive(IsOwner && canShoot);
        myAnimator.SetBool("gunEquipped", canShoot);
    }

    public void OnShoot(InputValue value)
    {
        if (!IsOwner || !canShoot || myMovement.IsInputBlocked()) { return; }
        if (isShooting || isReloading.Value) { return; }

        if (ammo.Value <= 0)
        {
            // Only the server can write ammo and isReloading, so the reload has to be requested instead of run locally
            ReloadServerRpc();
            return;
        }

        StartCoroutine(ShootingDelay());
    }

    IEnumerator ShootingDelay()
    {
        // Reads the camera when shooting instead of caching it on team change, the cached one could be the old third-person camera depending on callback order
        Camera cam = myMovement.GetCurrentCam();
        if (cam == null)
        {
            Debug.LogWarning("No camera to shoot from", this);
            yield break;
        }

        isShooting = true;
        ShootServerRpc(cam.transform.position, cam.transform.forward);

        yield return new WaitForSeconds(shootDelay);

        isShooting = false;
    }

    [Rpc(SendTo.Server)]
    void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        // Checks the team on the server too since canShoot only exists on the client
        if (myMovement.GetPlayerTeam().Value != GameManager.Team.Hunters) { return; }
        if (ammo.Value <= 0 || isReloading.Value) { return; }

        ammo.Value--;
        PlayShootSfxRpc();

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, shootRange, targetLayer))
        {
            Debug.Log("Hit target");

            PlayerHealth targetHealth = hit.transform.GetComponentInParent<PlayerHealth>();
            PlayerMovement targetMovement = hit.transform.GetComponentInParent<PlayerMovement>();

            if (targetHealth != null && targetMovement != null)
            {
                // Stop damage if target is on the same team as the shooter
                if (targetMovement.GetPlayerTeam().Value == myMovement.GetPlayerTeam().Value) { return; }

                targetHealth.TakeDamage(damage);
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    void PlayShootSfxRpc()
    {
        AudioManager.Instance.PlaySfx(AudioManager.Instance.shootSfx, transform);
    }

    void OnAmmoChanged(int oldValue, int newValue)
    {
        myUiManager.UpdateAmmoText(newValue.ToString(), maxAmmo.ToString());
    }

    // Runs on every client so the owner actually sees "...", before it was only set on the server's copy of the player
    void OnReloadingChanged(bool previous, bool current)
    {
        string ammoDisplay = current ? "..." : ammo.Value.ToString();
        myUiManager.UpdateAmmoText(ammoDisplay, maxAmmo.ToString());
    }

    public void OnReload(InputValue value)
    {
        if (!IsOwner || !canShoot || myMovement.IsInputBlocked()) { return; }
        if (isReloading.Value || isShooting || ammo.Value == maxAmmo) { return; }

        ReloadServerRpc();
    }

    [Rpc(SendTo.Server)]
    void ReloadServerRpc()
    {
        if (myMovement.GetPlayerTeam().Value != GameManager.Team.Hunters) { return; }

        // Stops a second reload starting if the request arrives twice
        if (isReloading.Value || ammo.Value == maxAmmo) { return; }

        StartCoroutine(ReloadRoutine());
    }

    // Server only
    IEnumerator ReloadRoutine()
    {
        isReloading.Value = true;

        PlayReloadSfxRpc();

        yield return new WaitForSeconds(reloadDelay);

        ammo.Value = maxAmmo;

        isReloading.Value = false;
    }

    [Rpc(SendTo.Everyone)]
    void PlayReloadSfxRpc()
    {
        AudioManager.Instance.PlaySfx(AudioManager.Instance.reloadSfx, transform);
    }

    public override void OnNetworkDespawn()
    {
        ammo.OnValueChanged -= OnAmmoChanged;
        isReloading.OnValueChanged -= OnReloadingChanged;
        myMovement.GetPlayerTeam().OnValueChanged -= OnTeamChanged;
    }
}