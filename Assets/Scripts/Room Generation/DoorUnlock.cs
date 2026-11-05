using UnityEngine;

/// <summary>
/// Visible collectible used to unlock a room's exit door.
///
/// - Floats up and down (using LOCAL position, so moving/rotating the room
///   after it spawns doesn't break the animation)
/// - Rotates continuously
/// - Optional idle particles and pickup effect
/// - Trigger collider; opens the RoomDoorController when the player touches it
///
/// Place this script on the visible PlayerHolder / capsule object.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorUnlockItem : MonoBehaviour
{
    // ------------------------------------------------------------
    // FLOATING
    // ------------------------------------------------------------

    [Header("Floating Animation")]

    [Tooltip("How far the item moves above and below its starting position (in the parent's local units).")]
    public float floatHeight = 0.35f;

    [Tooltip("How quickly the item moves up and down.")]
    public float floatSpeed = 2f;

    // ------------------------------------------------------------
    // ROTATION
    // ------------------------------------------------------------

    [Header("Rotation")]

    [Tooltip("How quickly the collectible rotates around its Y axis.")]
    public float rotationSpeed = 90f;

    // ------------------------------------------------------------
    // PARTICLES
    // ------------------------------------------------------------

    [Header("Particle Effects")]

    [Tooltip("Optional particle system that stays around the item while it is available.")]
    public ParticleSystem idleParticles;

    [Tooltip("Optional particle effect that plays when the player collects the item.")]
    public GameObject pickupEffectPrefab;

    [Tooltip("How long the pickup effect remains before being destroyed.")]
    public float pickupEffectLifetime = 3f;

    // ------------------------------------------------------------
    // VISUAL
    // ------------------------------------------------------------

    [Header("Visual")]

    [Tooltip("The visible model of the collectible. Leave empty if the model is on this object.")]
    public GameObject visualObject;

    // ------------------------------------------------------------
    // SAFETY
    // ------------------------------------------------------------

    [Header("Safety")]

    [Tooltip("Seconds after the room becomes active during which touching the item is ignored. " +
             "Stops it being collected instantly if the player spawns on top of it.")]
    public float pickupGraceTime = 0.5f;

    // ------------------------------------------------------------
    // INTERNAL
    // ------------------------------------------------------------

    private Vector3 startLocalPosition;
    private RoomDoorController doorController;
    private Collider itemCollider;
    private bool collected;
    private float enabledTime;

    // ------------------------------------------------------------
    // LIFECYCLE
    // ------------------------------------------------------------

    void Awake()
    {
        // LOCAL position stays valid even when the room is rotated or
        // re-centred by DungeonGenerator after it has been instantiated.
        startLocalPosition = transform.localPosition;

        itemCollider = GetComponent<Collider>();
        itemCollider.isTrigger = true;
    }

    void Start()
    {
        doorController = GetComponentInParent<RoomDoorController>();

        if (doorController == null)
        {
            Debug.LogWarning(
                "DoorUnlockItem: Could not find a RoomDoorController " +
                "in the parent hierarchy of " + gameObject.name
            );
        }
    }

    void OnEnable()
    {
        // Rooms are switched on/off by RoomChainManager, so (re)start the
        // grace timer and the idle particles every time the room activates.
        enabledTime = Time.time;

        if (!collected && idleParticles != null)
        {
            idleParticles.Play();
        }
    }

    void Update()
    {
        if (collected) return;

        AnimateFloatingItem();
        RotateItem();
    }

    // ------------------------------------------------------------
    // ANIMATION
    // ------------------------------------------------------------

    void AnimateFloatingItem()
    {
        float verticalOffset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        Vector3 newPosition = startLocalPosition;
        newPosition.y += verticalOffset;

        transform.localPosition = newPosition;
    }

    void RotateItem()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    // ------------------------------------------------------------
    // PLAYER COLLISION
    // ------------------------------------------------------------

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (Time.time < enabledTime + pickupGraceTime) return;

        CharacterController player = other.GetComponentInParent<CharacterController>();
        if (player == null) return;

        CollectItem();
    }

    // Covers the case where the player is already standing inside the
    // trigger when the grace period ends.
    void OnTriggerStay(Collider other)
    {
        if (collected) return;
        if (Time.time < enabledTime + pickupGraceTime) return;

        CharacterController player = other.GetComponentInParent<CharacterController>();
        if (player == null) return;

        CollectItem();
    }

    // ------------------------------------------------------------
    // COLLECT
    // ------------------------------------------------------------

    void CollectItem()
    {
        if (collected) return;
        collected = true;

        Debug.Log("DoorUnlockItem: collected " + name);

        if (idleParticles != null)
        {
            idleParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (pickupEffectPrefab != null)
        {
            GameObject effect = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, pickupEffectLifetime);
        }

        if (itemCollider != null)
        {
            itemCollider.enabled = false;
        }

        if (visualObject != null)
        {
            visualObject.SetActive(false);
        }
        else
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                r.enabled = false;
            }
        }

        if (doorController != null)
        {
            doorController.Open();
        }
        else
        {
            Debug.LogWarning(
                "DoorUnlockItem: No RoomDoorController found. " +
                "The collectible was collected, but the door could not be opened."
            );
        }
    }
}