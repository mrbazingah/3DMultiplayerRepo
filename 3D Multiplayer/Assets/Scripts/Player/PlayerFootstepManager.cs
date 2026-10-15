using System.Collections.Generic;
using System.Drawing;
using Unity.Netcode;
using UnityEngine;

public class PlayerFootstepManager : NetworkBehaviour
{
    [SerializeField] GameObject footstepPrefab;
    [SerializeField] Transform spawnPoint;
    [SerializeField] float offset;
    [SerializeField] float footstepTime;
    [SerializeField] float maxFootstepCount;
    [SerializeField] float maxVisableFootsteps;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float maxGroundDistance = 10f;
    [SerializeField] bool abilityActive;
    [SerializeField] List<GameObject> spawnedFootsteps = new List<GameObject>();

    float currentFootstepTime;

    PlayerMovement myMovement;
    PlayerData myData;

    public override void OnNetworkSpawn()
    {
        myMovement = GetComponent<PlayerMovement>();
        myData = GetComponent<PlayerData>();

    }

    void Update()
    {
        if (!IsOwner) { return; }

        HandleFootsteps();
    }

    void HandleFootsteps()
    {
        if (myMovement == null || !myMovement.GetIsWalking()) { return; }

        float speedMultiplier = (myMovement.GetWalkSpeed() / myMovement.GetRunSpeed()) + 1;

        if (currentFootstepTime > 0)
        {
            currentFootstepTime -= Time.deltaTime;
        }
        else
        {
            SpawnFootstepRpc(spawnPoint.position, transform.rotation);

            currentFootstepTime = myMovement.GetIsRunning() ? footstepTime / speedMultiplier : footstepTime;
        }

        UpdateFootstepVisibilityRpc();
    }

    [Rpc(SendTo.Everyone)]
    void UpdateFootstepVisibilityRpc()
    {
        for (int i = 0; i < spawnedFootsteps.Count; i++)
        {
            bool isVisible = i + maxVisableFootsteps < spawnedFootsteps.Count && abilityActive;
            spawnedFootsteps[i].SetActive(isVisible);

            // Change color
            MeshRenderer footstepRenderer = spawnedFootsteps[i].GetComponent<MeshRenderer>();
            if (footstepRenderer != null && myData != null)
            {
                foreach (Material mat in footstepRenderer.materials)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", myData.GetPlayerColor().Value);
                    }
                    if (mat.HasProperty("_Outline_Color"))
                    {
                        mat.SetColor("_Outline_Color", myData.GetPlayerColor().Value);
                    }
                }
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    void SpawnFootstepRpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        if (!Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, maxGroundDistance, groundLayer, QueryTriggerInteraction.Ignore)) { return; }

        // Lifts it slightly off the surface so it doesn't flicker into the ground, and tilts it to match slopes
        Vector3 position = hit.point + hit.normal * offset;
        position.y += offset;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * spawnRot;

        GameObject spawned = Instantiate(footstepPrefab, position, rotation);
        spawnedFootsteps.Add(spawned);

        

        if (spawnedFootsteps.Count > maxFootstepCount)
        {
            Destroy(spawnedFootsteps[0]);
            spawnedFootsteps.RemoveAt(0);
        }
    }

    public override void OnNetworkDespawn()
    {
        foreach (GameObject footstep in spawnedFootsteps)
        {
            Destroy(footstep);
        }

        spawnedFootsteps.Clear();
    }
}