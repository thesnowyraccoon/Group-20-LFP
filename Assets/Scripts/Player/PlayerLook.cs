using Unity.Cinemachine;
using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    CinemachineInputAxisController cam;

    public float xSensitivity = 12f;
    public float ySensitivity = 10f;
    public float scrollSensitivity = 5;

    void Awake()
    {
        cam = FindAnyObjectByType<CinemachineInputAxisController>();
    }

    void FixedUpdate()
    {
        if (cam == null) return;

        SetSensitivity();
    }

    void SetSensitivity()   // Changes Cinemachine Camera sensitivities
    {
        var controllers = cam.Controllers;
        if (controllers.Count > 0) controllers[0].Input.Gain = xSensitivity;
        if (controllers.Count > 1) controllers[1].Input.Gain = ySensitivity;
        if (controllers.Count > 2) controllers[2].Input.Gain = scrollSensitivity;
    }
}
