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

    private void Awake()
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
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
        if (inputCamera == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
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
