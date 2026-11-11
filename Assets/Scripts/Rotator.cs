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

    [Header("Pivot Settings")]
    [Tooltip("Rotate around the visual centre of the object's renderers instead of its pivot")]
    public bool rotateAroundCenter = true;

    private bool isRotating;

    private void Start()
    {
        isRotating = startRotating;
    }

    private void Update()
    {
        if (!isRotating)
            return;

        float angle = rotationSpeed * Time.deltaTime;

        if (rotateAroundCenter)
        {
            // Recalculated each frame so it stays correct if the object
            // moves, animates, or is scaled.
            Vector3 center = GetWorldCenter();

            // The axis is in local space, so convert it to world space.
            Vector3 worldAxis = transform.TransformDirection(GetRotationAxis());

            transform.RotateAround(center, worldAxis, angle);
        }
        else
        {
            transform.Rotate(GetRotationAxis(), angle, Space.Self);
        }
    }

    private Vector3 GetWorldCenter()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        // No renderers? Fall back to the pivot.
        if (renderers.Length == 0)
            return transform.position;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds.center;
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
