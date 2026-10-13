using UnityEngine;
using System.Collections.Generic;

public class PlayerFootstepManager : MonoBehaviour
{
    [SerializeField] GameObject footstepPrefab;
    [SerializeField] Transform spawnPoint;
    [SerializeField] float offset;
    [SerializeField] float footstepTime;
    [SerializeField] float maxFootstepCount;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float maxGroundDistance = 10f;

    List<GameObject> spawnedFootsteps = new List<GameObject>();

    float currentFootstepTime;

    PlayerMovement myMovement;

    void Start()
    {
        myMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (myMovement == null || !myMovement.GetIsWalking()) { return; }

        float speedMultiplier = (myMovement.GetWalkSpeed() / myMovement.GetRunSpeed()) + 1;

        if (currentFootstepTime > 0)
        {
            currentFootstepTime -= Time.deltaTime;
        }
        else
        {
            SpawnFootstep();

            currentFootstepTime = myMovement.GetIsRunning() ? footstepTime / speedMultiplier : footstepTime;
        }
    }

    void SpawnFootstep()
    {
        if (!Physics.Raycast(spawnPoint.position, Vector3.down, out RaycastHit hit, maxGroundDistance, groundLayer, QueryTriggerInteraction.Ignore)) { return; }

        // Lifts it slightly off the surface so it doesn't flicker into the ground, and tilts it to match slopes
        Vector3 position = hit.point + hit.normal * offset;
        position.y += offset;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;

        GameObject spawned = Instantiate(footstepPrefab, position, rotation);
        spawnedFootsteps.Add(spawned);

        if (spawnedFootsteps.Count > maxFootstepCount)
        {
            Destroy(spawnedFootsteps[0]);
            spawnedFootsteps.RemoveAt(0);
        }
    }
}