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
        Debug.Log("Shoot 1"); 

        if (!IsOwner || isShooting || isReloading || !canShoot || ammo.Value <= 0) { return; }

        Debug.Log("Shoot 2");

        StartCoroutine(ShootingDelay());
    }

    IEnumerator ShootingDelay()
    {
        isShooting = true;
        ShootServerRpc(cam.transform.position, cam.transform.forward);

        yield return new WaitForSeconds(shootDelay);

        isShooting = false;

        Debug.Log("Shoot 3");
    }

    [Rpc(SendTo.Server)]
    void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        Debug.Log("Shoot 4");

        if (ammo.Value <= 0 || isReloading) { return; }

        Debug.Log("Shoot 5");

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
        if (!IsOwner || isReloading || isShooting || !canShoot || ammo.Value == maxAmmo) { return; }

        StartCoroutine(ReloadingDelay());
    }

    IEnumerator ReloadingDelay()
    {
        isReloading = true;

        myUiManager.UpdateAmmoText("...", maxAmmo.ToString());

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