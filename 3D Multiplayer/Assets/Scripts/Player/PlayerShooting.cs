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

    Camera cam;

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
        OnAmmoChanged(0, ammo.Value);

        NetworkVariable<GameManager.Team> team = myMovement.GetPlayerTeam();
        team.OnValueChanged += OnTeamChanged;
        SetCanShoot(team.Value);

        if (team.Value == GameManager.Team.Hunters)
        {
            cam = myMovement.GetCurrentCam();
        }
    }

    void OnTeamChanged(GameManager.Team previousTeam, GameManager.Team newTeam)
    {
        SetCanShoot(newTeam);
        cam = myMovement.GetCurrentCam();
    }

    public void SetCanShoot(GameManager.Team team)
    {
        canShoot = team == GameManager.Team.Hunters;
        // Shows different gun models for owner and clients
        myModelManager.SetGunModelsActive(IsOwner && canShoot, !IsOwner && canShoot);
        myUiManager.SetAmmoTextActive(IsOwner && canShoot);
        myAnimator.SetBool("gunEquipped", canShoot);
    }

    public void OnShoot(InputValue value)
    {
        if (ammo.Value <= 0)
        {
            StartCoroutine(ReloadRoutine());
        }

        if (!IsOwner || isShooting || isReloading.Value || !canShoot || ammo.Value <= 0) { return; }

        StartCoroutine(ShootingDelay());
    }

    IEnumerator ShootingDelay()
    {
        isShooting = true;
        ShootServerRpc(cam.transform.position, cam.transform.forward);

        yield return new WaitForSeconds(shootDelay);

        isShooting = false;
    }

    [Rpc(SendTo.Server)]
    void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        if (ammo.Value <= 0 || isReloading.Value) { return; }

        ammo.Value--;

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

    void OnAmmoChanged(int oldValue, int newValue)
    {
        myUiManager.UpdateAmmoText(newValue.ToString(), maxAmmo.ToString());
    }

    public void OnReload(InputValue value)
    {
        if (!IsOwner || isReloading.Value || isShooting || !canShoot || ammo.Value == maxAmmo) { return; }

        ReloadServerRpc();
    }

    [Rpc(SendTo.Server)]
    void ReloadServerRpc()
    {
        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading.Value = true;

        myUiManager.UpdateAmmoText("...", maxAmmo.ToString());

        // Might want to change delay to server side
        yield return new WaitForSeconds(reloadDelay);

        ammo.Value = maxAmmo;

        isReloading.Value = false;
    }

    public override void OnNetworkDespawn()
    {
        ammo.OnValueChanged -= OnAmmoChanged;
        myMovement.GetPlayerTeam().OnValueChanged -= OnTeamChanged;
    }
}