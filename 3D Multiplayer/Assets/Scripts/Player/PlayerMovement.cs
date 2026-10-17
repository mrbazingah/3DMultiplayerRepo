using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    #region Variables
    [Header("Walking")]
    [SerializeField] float walkSpeed;
    [SerializeField] float runSpeed;
    [SerializeField] float crouchSpeed;
    [SerializeField] float crouchHeight;
    [SerializeField] float defaultHeight;

    [Header("Footsteps")]
    [SerializeField] float footstepSoundDelay;
    [SerializeField] GameObject footstepParticles;
    [SerializeField] Transform footstepSpawnPoint;
    [SerializeField] float despawnDelay;
    [SerializeField] float maxGroundDistance;
    [SerializeField] float offset;

    [Header("Jumping")]
    [SerializeField] float jumpForce;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float rayDistance;
    [SerializeField] float gravity;
    [SerializeField] float coyoteTime;

    [Header("Camera")]
    [SerializeField] float lookSpeed;
    [SerializeField] float lookXLimit;
    [SerializeField] Camera firstPersonCam;
    [SerializeField] Camera thirdPersonCam;
    [SerializeField] Transform camPivot;
    [SerializeField] AudioListener playerAudioListener;
    [SerializeField] LayerMask camCollisionLayers;
    [SerializeField] float camCollisionRadius;
    [SerializeField] float camReturnSpeed;
    [Space]
    [SerializeField] float zoomSpeed;
    [SerializeField] float minZoom;
    [SerializeField] float maxZoom;

    [Header("Spine Lean")]
    [SerializeField] float spinePitchMultiplier = 1.125f;
    [SerializeField] float spineMinPitch = -90f;
    [SerializeField] float spineMaxPitch = 90f;
    [SerializeField] float spinePitchSendThreshold = 1.5f;

    [Header("Other")]
    [SerializeField] PlayerInput playerInput;

    NetworkVariable<float> spinePitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    bool isRunning;
    bool isCrouching;
    bool isJumping;
    bool canMove;
    bool isWalking;

    float currentSpeed;
    float rotationX;
    float rotationY;
    float coyoteCounter;
    float desiredCamDistance;
    float currentCamDistance;
    float lastSentSpinePitch;
    float defaultAnimatorSpeed;
    float currentFootstepTime;

    Vector3 moveDirection;
    Vector3 camOffsetDirection;
    Vector2 movementInput;
    Vector2 lookInput;
    Vector2 zoomInput;

    Camera currentCam;

    Rigidbody myRigidbody;
    Animator myAnimator;
    Collider myCollider;
    PlayerModelManager myModelManager;
    PlayerData myData;
    #endregion

    public override void OnNetworkSpawn()
    {
        myRigidbody = GetComponent<Rigidbody>();
        myAnimator = GetComponentInChildren<Animator>();
        myCollider = GetComponent<Collider>();
        myModelManager = GetComponent<PlayerModelManager>();
        myData = GetComponent<PlayerData>();

        // Registers before the owner check since the server runs this for every player object and not just the ones it owns
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogWarning("GameManager not found, player not registered");
        }

        // If isn't owner, disable camera, input, audiolistener, rigidbody physics and this
        if (!IsOwner)
        {
            myRigidbody.isKinematic = true;

            thirdPersonCam.gameObject.SetActive(false);
            firstPersonCam.gameObject.SetActive(false);

            if (playerAudioListener != null)
            {
                playerAudioListener.enabled = false;
            }
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }

            enabled = false;
            return;
        }

        myRigidbody.isKinematic = false;

        // Set before SetCanMove() so clearing held input always has the real default to reset to
        defaultAnimatorSpeed = myAnimator.speed;
        currentSpeed = walkSpeed;

        SetCanMove(true);

        // Stores where the camera sits in the prefab so zoom and collision can move it along the same line
        camOffsetDirection = thirdPersonCam.transform.localPosition.normalized;
        desiredCamDistance = thirdPersonCam.transform.localPosition.magnitude;
        currentCamDistance = desiredCamDistance;

        rotationY = transform.rotation.eulerAngles.y;

        // Makes sure local player model has correct layer
        GameObject defaultVisuals = myModelManager.GetDefaultVisuals();
        myModelManager.SetLayerRecursively(defaultVisuals, LayerMask.NameToLayer("Player Visuals"));
    }

    public void SetCanMove(bool newCanMove)
    {
        canMove = newCanMove;

        Cursor.lockState = newCanMove ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !newCanMove;

        if (!newCanMove)
        {
            ClearHeldInput();
            movementInput = Vector3.zero;
        }
    }

    // Release events are ignored while canMove is false, so anything held when pausing would otherwise stay held after resuming
    void ClearHeldInput()
    {
        movementInput = Vector2.zero;
        lookInput = Vector2.zero;
        zoomInput = Vector2.zero;

        isRunning = false;
        isCrouching = false;
        currentSpeed = walkSpeed;

        if (myAnimator != null)
        {
            myAnimator.speed = defaultAnimatorSpeed;
        }

        if (myData.GetPlayerTeam().Value == GameManager.Team.Hunters)
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.height = defaultHeight;
            }
        }
    }

    // Shared check for shooting, swapping and locking so pausing or the lobby screen blocks all of them in one place
    public bool IsInputBlocked()
    {
        return !canMove;
    }

    public void UpdateCamLogic(GameManager.Team team)
    {
        bool isHunter = team == GameManager.Team.Hunters;
        firstPersonCam.enabled = isHunter;
        thirdPersonCam.enabled = !isHunter;

        currentCam = isHunter ? firstPersonCam : thirdPersonCam;
    }

    public void TeleportTo(Vector3 pos)
    {
        // The owner has authority over its own rigidbody so the move happens here
        if (!IsOwner) { return; }

        if (myRigidbody != null)
        {
            // Clears velocity so the player doesn't keep moving after being teleported
            myRigidbody.linearVelocity = Vector3.zero;
            myRigidbody.angularVelocity = Vector3.zero;
            myRigidbody.position = pos;
        }
        else
        {
            transform.position = pos;
        }
    }

    void Update()
    {
        FootstepEffects();
    }

    void FootstepEffects()
    {
        if (!isWalking || !IsGrounded()) { return; }

        float speedMultiplier = 1f;
        if (isCrouching)
        {
            speedMultiplier = 1 - crouchSpeed / walkSpeed;
        }
        else if (isRunning)
        {
            speedMultiplier = (walkSpeed / runSpeed) + 1;
        }

        if (currentFootstepTime > 0)
        {
            currentFootstepTime -= Time.deltaTime;
        }
        else if (currentFootstepTime <= 0)
        {
            PlayFootstepSfxRpc();
            SpawnFootstepParticlesRpc(footstepSpawnPoint.position, transform.rotation);

            currentFootstepTime = footstepSoundDelay / speedMultiplier;
        }
    }

    [Rpc(SendTo.Everyone)]
    void PlayFootstepSfxRpc()
    {
        AudioManager.Instance.PlaySfx(AudioManager.Instance.GetFootstepSfx(), transform);
    }

    [Rpc(SendTo.Everyone)]
    void SpawnFootstepParticlesRpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        if (!Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, maxGroundDistance, groundLayer, QueryTriggerInteraction.Ignore)) { return; }

        Vector3 position = hit.point + hit.normal * offset;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * spawnRot;

        GameObject spawned = Instantiate(footstepParticles, position, rotation);
        Destroy(spawned, despawnDelay);
    }

    void FixedUpdate()
    {
        Movement();
        ApplyGravity();
        CoyoteTime();

        myRigidbody.MoveRotation(Quaternion.Euler(0, rotationY, 0));
    }

    public void OnMove(InputValue value)
    {
        if (!IsOwner || !canMove) { return; }

        movementInput = value.Get<Vector2>();
    }

    // Pass through input
    public void OnRun(InputValue value)
    {
        if (!IsOwner || !canMove || isCrouching) { return; }

        isRunning = value.isPressed;
        currentSpeed = isRunning ? runSpeed : walkSpeed;

        float speed = isRunning ? defaultAnimatorSpeed + (1 - walkSpeed / runSpeed) : defaultAnimatorSpeed;
        myAnimator.speed = speed;
    }

    void Movement()
    {
        // Uses the player's own forward and right so movement follows where they're facing
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        moveDirection = (forward * movementInput.y) + (right * movementInput.x);

        isWalking = moveDirection.magnitude > 0;
        myAnimator.SetBool("isWalking", isWalking);

        Vector3 targetVelocity = moveDirection * currentSpeed;

        // Keeps the current y velocity so gravity and jumping aren't overwritten
        myRigidbody.linearVelocity = new Vector3(targetVelocity.x, myRigidbody.linearVelocity.y, targetVelocity.z);
    }

    // Pass through input
    public void OnCrouch(InputValue value)
    {
        if (!IsOwner || !canMove) { return; }

        isCrouching = value.isPressed;
        isRunning = false;

        currentSpeed = isCrouching ? crouchSpeed : walkSpeed;

        float speed = isCrouching ? defaultAnimatorSpeed - (1 - walkSpeed / runSpeed) : defaultAnimatorSpeed;
        myAnimator.speed = speed;

        if (myData.GetPlayerTeam().Value == GameManager.Team.Hunters)
        {
            CapsuleCollider collider = GetComponent<CapsuleCollider>();
            collider.height = isCrouching ? crouchHeight : defaultHeight;
        }
    }

    public void OnJump(InputValue value)
    {
        if (!IsOwner || !canMove) { return; }

        // Allows the jump if the player left the ground within the coyote window
        if (!value.isPressed || coyoteCounter <= 0 || isJumping) { return; }

        // Clears any downward velocity first so a jump taken late in the coyote window reaches full height
        myRigidbody.linearVelocity = new Vector3(myRigidbody.linearVelocity.x, 0, myRigidbody.linearVelocity.z);
        myRigidbody.AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);

        // Stops the same window being used for a second jump before landing
        coyoteCounter = 0;

        StartCoroutine(JumpRoutine());
    }

    // Prevent double jumps
    IEnumerator JumpRoutine()
    {
        isJumping = true;

        yield return new WaitForSeconds(0.2f);

        isJumping = false;
    }

    void ApplyGravity()
    {
        float velocity = myRigidbody.linearVelocity.y;
        if (IsGrounded() && (velocity < 0 || velocity > 0))
        {
            velocity = 0f;
        }
        else
        {
            velocity = gravity * Time.fixedDeltaTime;
        }

        myRigidbody.linearVelocity -= new Vector3(0, velocity, 0);
    }

    void CoyoteTime()
    {
        if (IsGrounded())
        {
            // Refills the timer while grounded so the player always has the full window when they walk off
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.fixedDeltaTime;
        }
    }

    public bool IsGrounded()
    {
        if (myCollider == null) return false;

        // Casts a ray down from the bottom of the current collider to check for ground
        Vector3 colOrigin = myCollider.bounds.center;
        colOrigin.y = myCollider.bounds.min.y + 0.1f; // Make sure ray sits above ground, otherwise it will go past the ground and return false

        return Physics.Raycast(colOrigin, Vector3.down, rayDistance, groundLayer);
    }

    public void SetPlayerCollider(Collider newCol)
    {
        // Called by PlayerModelManager so the ground check uses the prop model's collider after a swap
        myCollider = newCol;
    }

    public void OnLook(InputValue value)
    {
        if (!IsOwner || !canMove) { return; }

        lookInput = value.Get<Vector2>();
    }

    public void OnZoom(InputValue value)
    {
        // Only props can zoom since hunters are in first person
        if (!IsOwner || !canMove || myData.GetPlayerTeam().Value == GameManager.Team.Hunters) { return; }

        zoomInput = value.Get<Vector2>();
    }

    void LateUpdate()
    {
        // Pivot handles looking up and down and gets clamped, the body handles looking left and right in FixedUpdate()
        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        camPivot.localRotation = Quaternion.Euler(rotationX, 0, 0);
        rotationY += (lookInput.x * lookSpeed);

        PublishSpinePitch();

        // Resets input so the camera stops moving when there's no new input this frame
        lookInput = Vector2.zero;

        CameraZoom();

        // Runs after the pivot has rotated so the cast points where the camera actually ends up this frame
        CameraCollision();
    }

    void PublishSpinePitch()
    {
        float target = Mathf.Clamp(rotationX * spinePitchMultiplier, spineMinPitch, spineMaxPitch);

        // Writing every frame would send a packet per frame for a change nobody can see
        if (Mathf.Abs(target - lastSentSpinePitch) < spinePitchSendThreshold) { return; }

        spinePitch.Value = target;
        lastSentSpinePitch = target;
    }

    void CameraZoom()
    {
        // Only sets how far back the camera wants to be, CameraCollision() does the actual moving
        desiredCamDistance -= zoomInput.y * zoomSpeed * Time.deltaTime;
        desiredCamDistance = Mathf.Clamp(desiredCamDistance, minZoom, maxZoom);

        // Scroll input is a delta so it has to be cleared, otherwise the camera keeps zooming after one scroll
        zoomInput = Vector2.zero;
    }

    void CameraCollision()
    {
        // Hunters are in first person so the camera sits on the pivot and has nothing to pull in
        if (myData.GetPlayerTeam().Value == GameManager.Team.Hunters) { return; }

        Vector3 direction = camPivot.TransformDirection(camOffsetDirection);

        // Starts the cast in front of the pivot so the sphere isn't already overlapping a wall the player is stood against
        Vector3 origin = camPivot.position - direction * camCollisionRadius;

        float targetDistance = desiredCamDistance;

        // Sphere cast instead of a ray so the camera doesn't slip through corners and thin gaps that a thin ray would miss
        if (Physics.SphereCast(origin, camCollisionRadius, direction, out RaycastHit hit, desiredCamDistance + camCollisionRadius, camCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            // Subtracts the offset so the distance is measured from the pivot again and not from the shifted origin
            targetDistance = hit.distance - camCollisionRadius;
        }

        // Never lets the camera collapse onto the pivot even when the cast is blocked right at the start
        targetDistance = Mathf.Max(minZoom, targetDistance);

        if (targetDistance < currentCamDistance)
        {
            // Pulls in instantly, otherwise the camera would spend a few frames inside the wall
            currentCamDistance = targetDistance;
        }
        else
        {
            // Pushes back out slowly so the camera doesn't snap when the obstacle clears
            currentCamDistance = Mathf.Lerp(currentCamDistance, targetDistance, camReturnSpeed * Time.deltaTime);
        }

        thirdPersonCam.transform.localPosition = camOffsetDirection * currentCamDistance;
    }

    public Quaternion GetPlayerRotation()
    {
        return transform.rotation;
    }

    public Camera GetCurrentCam()
    {
        return currentCam;
    }

    public NetworkVariable<float> GetSpinePitch()
    {
        return spinePitch;
    }

    public bool GetIsCrouching()
    {
        return isCrouching;
    }

    public bool GetIsWalking()
    {
        return isWalking;
    }

    public bool GetIsRunning()
    {
        return isRunning;
    }

    public float GetCrouchSpeed()
    {
        return crouchSpeed;
    }

    public float GetWalkSpeed()
    {
        return walkSpeed;
    }

    public float GetRunSpeed()
    {
        return runSpeed;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(this);
        }
    }
}