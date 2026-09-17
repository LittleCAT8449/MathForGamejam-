using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to a clickable 2D reset object. Resets presses, deployed machines,
/// number tokens, and settlement contents.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class GameResetClick : MonoBehaviour
{
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;
    [Header("冲压后的返回提示")]
    [Tooltip("第一次冲压后点击返回采矿时显示的弹窗对象。请在场景中提前设为未激活。")]
    [SerializeField] private GameObject resetConfirmationObject;

    /// <summary>
    /// True while the reset confirmation object is visible. Other scene input
    /// scripts use this gate so the modal prompt blocks world interactions.
    /// </summary>
    public static bool IsModalOpen { get; private set; }

    private void Awake()
    {
        // Always include this button's own layer in the raycast mask.
        clickableLayers |= 1 << gameObject.layer;

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        CloseResetConfirmation();
    }

    private void Start()
    {
        if (inputCamera == null)
        {
            Debug.LogWarning($"{name}：没有找到输入摄像机，请指定 Input Camera 或将摄像机 Tag 设为 MainCamera。", this);
        }
    }

    private void Update()
    {
        if (IsModalOpen || inputCamera == null || Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (WasClicked(Mouse.current.position.ReadValue()))
        {
            ResetRound();
        }
    }

    /// <summary>
    /// Can also be connected to a Unity UI Button's OnClick event.
    /// </summary>
    public void ResetRound()
    {
        if (IsModalOpen)
        {
            return;
        }

        if (HasAnyStampStarted())
        {
            OpenResetConfirmation();
            return;
        }

        PerformResetRound();
    }

    /// <summary>
    /// Connect this to the confirmation object's “重新开始” button.
    /// </summary>
    public void ConfirmResetRound()
    {
        CloseResetConfirmation();
        PerformResetRound();
    }

    /// <summary>
    /// Connect this to the confirmation object's “否” button.
    /// </summary>
    public void CancelResetRound()
    {
        CloseResetConfirmation();
    }

    /// <summary>
    /// Resets the current round without opening the confirmation prompt. This
    /// is used by LevelManager after a level has been completed successfully.
    /// </summary>
    public void ResetRoundImmediately()
    {
        CloseResetConfirmation();
        PerformResetRound();
    }

    private void PerformResetRound()
    {
        StampingMachine[] presses = FindObjectsByType<StampingMachine>(FindObjectsSortMode.None);
        foreach (StampingMachine press in presses)
        {
            press.ResetPress();
        }

        MiningMachineDeploymentArea[] deploymentAreas =
            FindObjectsByType<MiningMachineDeploymentArea>(FindObjectsSortMode.None);
        foreach (MiningMachineDeploymentArea area in deploymentAreas)
        {
            area.ReturnAllDeployedMachines();
        }

        NumberArea[] numberAreas = FindObjectsByType<NumberArea>(FindObjectsSortMode.None);
        foreach (NumberArea area in numberAreas)
        {
            area.ClearNumbers();
        }

        SettlementArea[] settlementAreas = FindObjectsByType<SettlementArea>(FindObjectsSortMode.None);
        foreach (SettlementArea area in settlementAreas)
        {
            area.ClearNumbers();
        }

        // Also catches tokens produced by a press that are not tracked by an area.
        NumberToken[] remainingTokens = FindObjectsByType<NumberToken>(FindObjectsSortMode.None);
        foreach (NumberToken token in remainingTokens)
        {
            if (token != null)
            {
                Destroy(token.gameObject);
            }
        }

        MiningMachinePowerSystem[] powerSystems =
            FindObjectsByType<MiningMachinePowerSystem>(FindObjectsSortMode.None);
        foreach (MiningMachinePowerSystem powerSystem in powerSystems)
        {
            powerSystem.RecalculatePower();
        }

        CameraSmoothMove[] cameras = FindObjectsByType<CameraSmoothMove>(FindObjectsSortMode.None);
        foreach (CameraSmoothMove cameraMover in cameras)
        {
            cameraMover.MoveToInitialPosition();
        }

        Debug.Log("重置完成：冲压机和摄像机归位、数字清除、采矿机返回待选区。", this);
    }

    private bool HasAnyStampStarted()
    {
        if (StampingMachine.HasStartedAnyStampingThisRound)
        {
            Debug.Log("返回采矿检查：本局已开始冲压，需要确认。", this);
            return true;
        }

        StampingMachine[] presses = FindObjectsByType<StampingMachine>(FindObjectsSortMode.None);
        foreach (StampingMachine press in presses)
        {
            if (press != null && press.HasStartedStamping)
            {
                Debug.Log("返回采矿检查：检测到冲压机已开始冲压，需要确认。", this);
                return true;
            }
        }

        Debug.Log("返回采矿检查：尚未检测到冲压，直接执行重置。", this);
        return false;
    }

    private void OpenResetConfirmation()
    {
        if (resetConfirmationObject == null)
        {
            Debug.LogWarning(
                "返回采矿需要确认弹窗，但没有指定 Reset Confirmation Object。",
                this);
            return;
        }

        resetConfirmationObject.SetActive(true);
        IsModalOpen = true;
        Debug.Log("检测到本局已经开始冲压，打开返回采矿确认提示。", this);
    }

    private void CloseResetConfirmation()
    {
        IsModalOpen = false;

        if (resetConfirmationObject != null &&
            resetConfirmationObject != gameObject)
        {
            resetConfirmationObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // Do not leave the static input gate enabled if the scene is unloaded
        // while the confirmation object is visible.
        IsModalOpen = false;
    }

    private bool WasClicked(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, clickableLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<GameResetClick>() == this)
            {
                return true;
            }
        }

        return false;
    }
}
