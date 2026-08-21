using UnityEngine;

public class Rotator : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Rotation Settings")]
    public RotationAxis axis = RotationAxis.X;

    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 30f;

    [Tooltip("Should the object be rotating when the scene starts?")]
    public bool startRotating = false;

    private bool isRotating;

    private void Start()
    {
        isRotating = startRotating;
    }

    private void Update()
    {
        if (!isRotating)
            return;

        transform.Rotate(
            GetRotationAxis(),
            rotationSpeed * Time.deltaTime,
            Space.Self
        );
    }

    private Vector3 GetRotationAxis()
    {
        switch (axis)
        {
            case RotationAxis.X:
                return Vector3.right;

            case RotationAxis.Y:
                return Vector3.up;

            case RotationAxis.Z:
                return Vector3.forward;

            default:
                return Vector3.right;
        }
    }

    // These functions can be called from Button OnClick().

    public void ToggleRotation()
    {
        isRotating = !isRotating;
    }

    public void StartRotation()
    {
        isRotating = true;
    }

    public void StopRotation()
    {
        isRotating = false;
    }


    // ============================================================
    // FUTURE VR
    // ============================================================

    /*
    
    No changes should be necessary here for VR.

    A future XR button/controller interaction can simply call:

        ToggleRotation();

    just like the desktop UI button does.

    */
}