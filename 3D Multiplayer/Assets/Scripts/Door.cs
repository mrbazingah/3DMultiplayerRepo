using Unity.Netcode;
using UnityEngine;

public class Door : NetworkBehaviour
{
    [SerializeField] Transform doorHinge;
    [SerializeField] float lerpSpeed;

    NetworkVariable<float> currentAngle = new NetworkVariable<float>();

    float openAngle1 = 90f;
    float openAngle2 = -90f;
    float closedAngle = 0f;
    bool isOpen;

    Quaternion closedRotation;
    Vector3 closedForward;

    void Awake()
    {
        closedRotation = doorHinge.localRotation;
        closedForward = doorHinge.forward;
    }

    [Rpc(SendTo.Server)]
    public void OpenCloseDoorServerRpc(Vector3 playerPosition)
    {
        isOpen = !isOpen;

        if (!isOpen)
        {
            currentAngle.Value = closedAngle;
            return;
        }

        Vector3 doorToPlayer = playerPosition - doorHinge.position;
        float angleToPlayer = Vector3.SignedAngle(closedForward, doorToPlayer, Vector3.up);

        currentAngle.Value = angleToPlayer > 0 ? openAngle2 : openAngle1;
    }

    void Update()
    {
        RotateHinge();
    }

    void RotateHinge()
    {
        Quaternion targetRotation = closedRotation * Quaternion.Euler(0f, currentAngle.Value, 0f);
        doorHinge.localRotation = Quaternion.Lerp(doorHinge.localRotation, targetRotation, Time.deltaTime * lerpSpeed);
    }
}