using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach this component to the in-scene object that starts mining.
/// Clicking it creates one number token for every powered deployed machine.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StartMiningObject : MonoBehaviour
{
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private MiningMachineDeploymentArea deploymentArea;
    [SerializeField] private NumberArea numberArea;

    private void Awake()
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (deploymentArea == null)
        {
            deploymentArea = FindFirstObjectByType<MiningMachineDeploymentArea>();
        }

        if (numberArea == null)
        {
            numberArea = FindFirstObjectByType<NumberArea>();
        }
    }

    private void Start()
    {
        if (inputCamera == null)
        {
            Debug.LogWarning($"{name}：没有找到输入摄像机，请指定 Input Camera 或将摄像机 Tag 设为 MainCamera。", this);
        }

        if (deploymentArea == null)
        {
            Debug.LogWarning($"{name}：没有找到 MiningMachineDeploymentArea。", this);
        }

        if (numberArea == null)
        {
            Debug.LogWarning($"{name}：没有找到 NumberArea，请指定数字区。", this);
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
            BeginMining();
        }
    }

    /// <summary>
    /// Creates one number object for each powered deployed machine.
    /// </summary>
    public void BeginMining()
    {
        if (deploymentArea == null)
        {
            Debug.LogWarning("无法开始采矿：没有指定开采区域。", this);
            return;
        }

        if (numberArea == null)
        {
            Debug.LogWarning("无法开始采矿：没有指定数字区。", this);
            return;
        }

        int totalOutput = 0;
        int workingMachineCount = 0;

        foreach (MiningMachineItem machine in deploymentArea.DeployedMachines)
        {
            if (machine == null)
            {
                continue;
            }

            int output = machine.ProductionNumber;
            string state = machine.IsPowered ? "已通电" : "未通电";

            if (machine.IsPowered)
            {
                NumberToken token = numberArea.SpawnNumber(output);
                if (token == null)
                {
                    Debug.LogWarning($"采矿机 {machine.name} 已通电，但数字物体生成失败。", machine);
                    continue;
                }

                workingMachineCount++;
                totalOutput += output;
                Debug.Log($"采矿机 {machine.name}：{state}，生成数字 {output}。", machine);
            }
            else
            {
                Debug.Log($"采矿机 {machine.name}：{state}，本次没有产出。", machine);
            }
        }

        Debug.Log($"开始采矿：{workingMachineCount} 台机器工作，总产出 {totalOutput}。", this);
    }

    private bool WasClicked(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, clickableLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<StartMiningObject>() == this)
            {
                return true;
            }
        }

        return false;
    }
}
