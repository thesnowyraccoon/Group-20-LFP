using UnityEngine;

/// <summary>
/// Keeps a glow / light-ray effect BEHIND an object from the camera's point of view,
/// no matter which angle the player looks from.
///
/// Put this on the GameObject that holds the light-ray (and soft glow) Particle System.
/// Particles that use Render Alignment = View always face the camera, so the only thing
/// that has to follow the camera is the POSITION of the effect.
/// </summary>
public class BackdropBillboard : MonoBehaviour
{
    [Tooltip("The object to sit behind (the key). Leave empty to use this object's parent.")]
    public Transform target;

    [Tooltip("How far behind the target (away from the camera) the effect sits, in metres. " +
             "Make this bigger than half the key's thickness so the key is always in front.")]
    public float distanceBehind = 0.5f;

    [Tooltip("Lift the effect's centre above the target's pivot, e.g. to the middle of the key.")]
    public float heightOffset = 0f;

    Camera cam;

    void LateUpdate()
    {
        if (cam == null || !cam.isActiveAndEnabled)
        {
            cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cam == null) return;
        }

        Transform t = target != null ? target : transform.parent;
        if (t == null) return;

        Vector3 focus = t.position + Vector3.up * heightOffset;

        // Direction from the camera to the object = "behind" the object.
        Vector3 away = focus - cam.transform.position;
        if (away.sqrMagnitude < 0.0001f) return;
        away.Normalize();

        transform.position = focus + away * distanceBehind;
        transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }
}