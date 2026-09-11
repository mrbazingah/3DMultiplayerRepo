using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract : NetworkBehaviour
{
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Transform camPivot;
    [SerializeField] float detectionRange;

    PlayerModelManager myModelManager;
    PlayerUIManager myUiManager;
    PlayerMovement myMovement;

    void Start()
    {
        myModelManager = GetComponent<PlayerModelManager>();
        myUiManager = GetComponent<PlayerUIManager>();
        myMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (!IsOwner) { return; }

        GameObject detectedObject = GetDetectedObject();
        if (detectedObject != null && detectedObject.layer == LayerMask.NameToLayer("Door") && detectedObject.TryGetComponent(out Door door))
        {
            bool isOpen = door.GetIsOpen();
            string action = isOpen ? "Close" : "Open";
            myUiManager.SetInteractField(true, action);
        }
        else if (detectedObject != null && detectedObject.layer == LayerMask.NameToLayer("Prop") && myMovement.GetPlayerTeam().Value == GameManager.Team.Props)
        {
            myUiManager.SetInteractField(true, "Swap");
        }
        else
        {
            myUiManager.SetInteractField(false, "");
        }
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner) { return; }

        GameObject detectedObject = GetDetectedObject();
        if (detectedObject != null && detectedObject.TryGetComponent(out Door door))
        {
            door.OpenCloseDoorServerRpc(transform.position);
        }
        else if (detectedObject != null && detectedObject.TryGetComponent(out Prop prop))
        {
            myModelManager.SwapModel(prop);
        }
    }

    GameObject GetDetectedObject()
    {
        if (Physics.Raycast(camPivot.position, camPivot.forward, out RaycastHit hit, detectionRange, interactLayer))
        {
            return hit.collider.gameObject;
        }

        return null;
    }
}
