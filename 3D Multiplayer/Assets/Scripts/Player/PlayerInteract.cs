using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract : NetworkBehaviour
{
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Transform camPivot;
    [SerializeField] float detectionRange;

    PlayerModelManager playerModelManager;

    void Start()
    {
        playerModelManager = GetComponent<PlayerModelManager>();
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner) { return; }

        GameObject detectedObject = DetectObject();
        if (detectedObject != null && detectedObject.TryGetComponent(out Door door))
        {
            door.OpenCloseDoorServerRpc(transform.position);
        }
        else if (detectedObject != null && detectedObject.TryGetComponent(out Prop prop))
        {
            playerModelManager.DetectProp(prop);
        }
    }

    GameObject DetectObject()
    {
        if (Physics.Raycast(camPivot.position, camPivot.forward, out RaycastHit hit, detectionRange, interactLayer))
        {
            return hit.collider.gameObject;
        }

        return null;
    }
}
