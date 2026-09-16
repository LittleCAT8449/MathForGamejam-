using UnityEngine;

/// <summary>
/// Attach this component to the camera. MoveTo accepts a world-space XY position;
/// the camera's current Z position is preserved.
/// </summary>
public class CameraSmoothMove : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float smoothTime = 0.25f;
    private Vector2 targetWorldPosition;
    private Vector2 currentVelocity;
    private Vector2 initialWorldPosition;

    private void Awake()
    {
        Vector3 position = transform.position;
        initialWorldPosition = new Vector2(position.x, position.y);
        targetWorldPosition = initialWorldPosition;
    }

    private void Update()
    {
        Vector3 position = transform.position;
        Vector2 currentPosition = new Vector2(position.x, position.y);
        Vector2 nextPosition = Vector2.SmoothDamp(
            currentPosition,
            targetWorldPosition,
            ref currentVelocity,
            smoothTime);

        transform.position = new Vector3(nextPosition.x, nextPosition.y, position.z);
    }

    /// <summary>
    /// Smoothly moves the camera to the given world-space XY position.
    /// </summary>
    public void MoveTo(Vector2 worldPosition)
    {
        targetWorldPosition = worldPosition;
    }

    /// <summary>
    /// Smoothly moves the camera back to the XY position it had when the scene started.
    /// </summary>
    public void MoveToInitialPosition()
    {
        MoveTo(initialWorldPosition);
    }
}
