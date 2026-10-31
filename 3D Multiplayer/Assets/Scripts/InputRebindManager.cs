using UnityEngine;
using UnityEngine.InputSystem;

public class InputRebindManager : MonoBehaviour
{
    [SerializeField] InputActionReference walkAction;
    
    PlayerInput myInput;

    void Awake()
    {
        myInput = GetComponent<PlayerInput>();
    }
}
