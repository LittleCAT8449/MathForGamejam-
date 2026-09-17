using UnityEngine;

/// <summary>
/// Put this component on the operation-selection UI object. Connect the four
/// public methods to the Add, Subtract, Multiply and Divide buttons.
/// Selecting an operation closes the UI and keeps that operation for the next
/// press cycle.
/// </summary>
public class StampOperationSelector : MonoBehaviour
{
    [SerializeField] private StampingMachine stampingMachine;
    [SerializeField] private GameObject uiObjectToClose;

    private void Awake()
    {
        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        if (uiObjectToClose == null)
        {
            uiObjectToClose = gameObject;
        }
    }

    private void Start()
    {
        if (stampingMachine == null)
        {
            Debug.LogWarning(
                $"{name}：没有找到 StampingMachine，请在 Inspector 中指定。",
                this);
        }
    }

    /// <summary>
    /// Connect this method to the addition button.
    /// </summary>
    public void SelectAdd()
    {
        SelectOperation(StampOperation.Add);
    }

    /// <summary>
    /// Connect this method to the subtraction button.
    /// </summary>
    public void SelectSubtract()
    {
        SelectOperation(StampOperation.Subtract);
    }

    /// <summary>
    /// Connect this method to the multiplication button.
    /// </summary>
    public void SelectMultiply()
    {
        SelectOperation(StampOperation.Multiply);
    }

    /// <summary>
    /// Connect this method to the division button.
    /// </summary>
    public void SelectDivide()
    {
        SelectOperation(StampOperation.Divide);
    }

    private void SelectOperation(StampOperation selectedOperation)
    {
        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        if (stampingMachine == null)
        {
            Debug.LogWarning("无法设置冲压运算：没有找到 StampingMachine。", this);
            return;
        }

        stampingMachine.SetOperation(selectedOperation);

        CloseUI();

        Debug.Log($"已选择冲压运算：{GetOperationName(selectedOperation)}。", this);
    }

    /// <summary>
    /// Connect this method to a separate Close button when the player should
    /// be able to hide the operation UI without changing the operation.
    /// </summary>
    public void CloseUI()
    {
        if (uiObjectToClose != null)
        {
            uiObjectToClose.SetActive(false);
        }
    }

    private string GetOperationName(StampOperation selectedOperation)
    {
        switch (selectedOperation)
        {
            case StampOperation.Add:
                return "加法";
            case StampOperation.Subtract:
                return "减法";
            case StampOperation.Multiply:
                return "乘法";
            case StampOperation.Divide:
                return "除法";
            default:
                return selectedOperation.ToString();
        }
    }
}
