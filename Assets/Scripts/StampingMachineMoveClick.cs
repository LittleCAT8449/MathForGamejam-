using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to a clickable Sprite/2D object to call StampingMachine.Move().
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StampingMachineMoveClick : MonoBehaviour
{
    [SerializeField] private StampingMachine stampingMachine;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;
    [Header("拉杆 Z 轴旋转")]
    [Tooltip("点击冲压时拉杆绕自身 Z 轴旋转的角度，单位为度。")]
    [SerializeField] private float rotationAngle = 90f;

    private Quaternion initialLocalRotation;
    private bool leverRotationPrepared;
    private Coroutine restoreRotationCoroutine;

    private void Awake()
    {
        // Keep the button clickable even when its GameObject is moved to a
        // custom layer and the serialized mask was not updated.
        clickableLayers |= 1 << gameObject.layer;

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        initialLocalRotation = transform.localRotation;
    }

    private void Start()
    {
        if (inputCamera == null)
        {
            Debug.LogWarning($"{name}：没有找到输入摄像机，请设置 Input Camera 或将摄像机 Tag 设为 MainCamera。", this);
        }

        if (stampingMachine == null)
        {
            Debug.LogWarning($"{name}：没有找到 StampingMachine，请在 Inspector 中指定。", this);
        }
    }

    private void Update()
    {
        if (GameResetClick.IsModalOpen || inputCamera == null || Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (WasClicked(Mouse.current.position.ReadValue()))
        {
            CallMove();
        }
    }

    /// <summary>
    /// Can also be connected to a Unity UI Button's OnClick event.
    /// </summary>
    public void CallMove()
    {
        if (GameResetClick.IsModalOpen)
        {
            return;
        }

        if (stampingMachine == null)
        {
            Debug.LogWarning("无法启动冲压：没有指定 StampingMachine。", this);
            return;
        }

        bool canStartMove = stampingMachine.CanStartMove;
        if (canStartMove)
        {
            GameAudioManager.Instance?.PlayLeverPull();
            RotateLever();
        }

        stampingMachine.Move();

        if (canStartMove)
        {
            StartRestoreRotationWhenPressReturns();
        }
    }

    /// <summary>
    /// Rotates the lever using the configured Z-axis angle. This can be
    /// connected to a separate button or called from another script.
    /// </summary>
    public void CallRotate()
    {
        if (GameResetClick.IsModalOpen || stampingMachine == null)
        {
            return;
        }

        RotateLever();
    }

    private bool RotateLever()
    {
        if (stampingMachine == null || !stampingMachine.CanStartMove)
        {
            return false;
        }

        if (leverRotationPrepared)
        {
            return true;
        }

        transform.localRotation = initialLocalRotation *
                                  Quaternion.Euler(0f, 0f, rotationAngle);
        leverRotationPrepared = true;
        return true;
    }

    private void StartRestoreRotationWhenPressReturns()
    {
        if (restoreRotationCoroutine != null)
        {
            StopCoroutine(restoreRotationCoroutine);
        }

        restoreRotationCoroutine = StartCoroutine(RestoreRotationWhenPressReturns());
    }

    private IEnumerator RestoreRotationWhenPressReturns()
    {
        while (stampingMachine != null && !stampingMachine.CanStartMove)
        {
            yield return null;
        }

        RestoreLeverRotation();
        restoreRotationCoroutine = null;
    }

    private void RestoreLeverRotation()
    {
        transform.localRotation = initialLocalRotation;
        leverRotationPrepared = false;
    }

    private void OnDisable()
    {
        if (restoreRotationCoroutine != null)
        {
            StopCoroutine(restoreRotationCoroutine);
            restoreRotationCoroutine = null;
        }

        RestoreLeverRotation();
    }

    private bool WasClicked(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, clickableLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<StampingMachineMoveClick>() == this)
            {
                return true;
            }
        }

        return false;
    }
}
