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

    bool isShooting;
    bool isReloading;
    bool canShoot;

    Camera cam;

    PlayerMovement myMovement;
    PlayerUIManager uiManager;

    public override void OnNetworkSpawn()
    {
        myMovement = GetComponent<PlayerMovement>();
        uiManager = GetComponent<PlayerUIManager>();

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
    }

    public void OnShoot(InputValue value)
    {
        if (!IsOwner || isShooting || isReloading || !canShoot || ammo.Value <= 0) { return; }

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
        if (ammo.Value <= 0 || isReloading) { return; }

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
        uiManager.UpdateAmmoText(newValue.ToString(), maxAmmo.ToString());
    }

    public void OnReload(InputValue value)
    {
        if (!IsOwner || isReloading || isShooting || !canShoot || ammo.Value == maxAmmo) { return; }

        StartCoroutine(ReloadingDelay());
    }

    IEnumerator ReloadingDelay()
    {
        isReloading = true;

        uiManager.UpdateAmmoText("...", "");

        // Might want to change delay to server side
        yield return new WaitForSeconds(reloadDelay);

        ReloadServerRpc();

        isReloading = false;
    }

    [Rpc(SendTo.Server)]
    void ReloadServerRpc()
    {
        ammo.Value = maxAmmo;
    }

    public override void OnNetworkDespawn()
    {
        ammo.OnValueChanged -= OnAmmoChanged;
        myMovement.GetPlayerTeam().OnValueChanged -= OnTeamChanged;
    }
}