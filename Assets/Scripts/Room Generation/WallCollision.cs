using UnityEngine;

public class WallCollision : MonoBehaviour
{
    private void Start()
    {
        Collider[] colliders =
            Physics.OverlapBox(
                transform.position,
                GetHalfExtents(),
                transform.rotation
            );

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            if (collider.CompareTag("Wall"))
            {
                Destroy(gameObject);
                return;
            }
        }
    }


    Vector3 GetHalfExtents()
    {
        BoxCollider box =
            GetComponent<BoxCollider>();

        if (box != null)
        {
            return Vector3.Scale(
                box.size * 0.5f,
                transform.lossyScale
            );
        }

        return Vector3.one * 0.01f;
    }
}
