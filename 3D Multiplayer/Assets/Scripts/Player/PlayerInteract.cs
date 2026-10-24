using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract : NetworkBehaviour
{
    [SerializeField] LayerMask interactLayer;
    [SerializeField] LayerMask blockingLayer;
    [SerializeField] Transform camPivot;
    [SerializeField] float detectionRange;

    PlayerModelManager myModelManager;
    PlayerUIManager myUiManager;
    PlayerData myData;

    void Awake()
    {
        myModelManager = GetComponent<PlayerModelManager>();
        myUiManager = GetComponent<PlayerUIManager>();
        myData = GetComponent<PlayerData>();
    }

    void Update()
    {
        if (!IsOwner) { return; }

        GameObject detectedObject = GetDetectedObject();
        if (detectedObject == null) 
        {
            myUiManager.SetInteractField(false, "");
            return; 
        }

        if (detectedObject.layer == LayerMask.NameToLayer("Door") && detectedObject.TryGetComponent(out Door door))
        {
            bool isOpen = door.GetIsOpen();
            string action = isOpen ? "Close" : "Open";
            myUiManager.SetInteractField(true, action);
        }
        else if (detectedObject.layer == LayerMask.NameToLayer("Prop") && myData.GetPlayerTeam().Value == GameManager.Team.Props)
        {
            myUiManager.SetInteractField(true, "Swap");
        }
        else if (detectedObject.layer == LayerMask.NameToLayer("LobbyUI"))
        {
            myUiManager.SetInteractField(true, "Interact");
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
        else if (detectedObject != null && detectedObject.TryGetComponent(out LobbyUIManager lobbyUi))
        {
            lobbyUi.OpenCloseCanvas(gameObject);
        }
    }

    GameObject GetDetectedObject()
    {
        LayerMask rayMask = interactLayer | blockingLayer;

        if (Physics.Raycast(camPivot.position, camPivot.forward, out RaycastHit hit, detectionRange, rayMask))
        {
            bool isInteractable = (interactLayer.value & (1 << hit.collider.gameObject.layer)) != 0;
            return isInteractable ? hit.collider.gameObject : null;
        }

        return null;
    }
}
