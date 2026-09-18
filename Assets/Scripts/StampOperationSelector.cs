using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put this component on the operation-selection UI object. Connect the four
/// public methods to the Add, Subtract, Multiply and Divide buttons.
/// Selecting an operation closes the UI and keeps that operation for the next
/// press cycle. The subtraction button starts with positive mode and toggles
/// to negative mode on the next selection after that reward is unlocked.
/// </summary>
public class StampOperationSelector : MonoBehaviour
{
    [SerializeField] private StampingMachine stampingMachine;
    [SerializeField] private GameObject uiObjectToClose;
    [SerializeField] private LevelManager levelManager;

    [Header("运算按钮")]
    [SerializeField] private Button addButton;
    [SerializeField] private Button subtractButton;
    [SerializeField] private Button multiplyButton;
    [SerializeField] private Button divideButton;

    [Header("减法模式显示")]
    [Tooltip("显示当前减法模式，例如：正数模式（大数-小数）。")]
    [SerializeField] private TMP_Text subtractModeLabel;

    private void Awake()
    {
        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        if (uiObjectToClose == null)
        {
            uiObjectToClose = gameObject;
        }

        RefreshButtons();
        RefreshSubtractModeLabel();
    }

    private void Start()
    {
        if (stampingMachine == null)
        {
            Debug.LogWarning(
                $"{name}：没有找到 StampingMachine，请在 Inspector 中指定。",
                this);
        }

        RefreshButtons();
        RefreshSubtractModeLabel();
    }

    private void OnEnable()
    {
        if (levelManager != null)
        {
            levelManager.OperationAvailabilityChanged += RefreshButtons;
        }

        RefreshButtons();
        RefreshSubtractModeLabel();
    }

    private void OnDisable()
    {
        if (levelManager != null)
        {
            levelManager.OperationAvailabilityChanged -= RefreshButtons;
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
        if (!EnsureStampingMachine() || !IsOperationAvailable(StampOperation.Subtract))
        {
            return;
        }

        if (stampingMachine.Operation != StampOperation.Subtract ||
            !stampingMachine.HasSelectedSubtractMode)
        {
            // Entering subtraction always starts in positive mode.
            stampingMachine.SetOperation(StampOperation.Subtract);
            stampingMachine.SetSubtractNegativeMode(false);
        }
        else
        {
            if (levelManager != null && !levelManager.IsNegativeSubtractUnlocked)
            {
                RefreshSubtractModeLabel();
                Debug.LogWarning("负数减法模式尚未通过奖励解锁。", this);
                return;
            }

            stampingMachine.ToggleSubtractMode();
        }

        RefreshSubtractModeLabel();
        CloseUI();
        Debug.Log(
            $"已选择冲压运算：减法，{GetSubtractModeName()}。",
            this);
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

        if (levelManager != null &&
            !levelManager.IsOperationAvailable(selectedOperation))
        {
            Debug.LogWarning(
                $"当前关卡还不能使用运算：{GetOperationName(selectedOperation)}。",
                this);
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

    /// <summary>
    /// Refreshes button interactability from the current permanent unlocks
    /// and the operation restrictions of the current level.
    /// </summary>
    public void RefreshButtons()
    {
        if (levelManager == null)
        {
            SetButtonInteractable(addButton, true);
            SetButtonInteractable(subtractButton, true);
            SetButtonInteractable(multiplyButton, true);
            SetButtonInteractable(divideButton, true);
            RefreshSubtractModeLabel();
            return;
        }

        SetButtonInteractable(
            addButton,
            levelManager.IsOperationAvailable(StampOperation.Add));
        SetButtonInteractable(
            subtractButton,
            levelManager.IsOperationAvailable(StampOperation.Subtract));
        SetButtonInteractable(
            multiplyButton,
            levelManager.IsOperationAvailable(StampOperation.Multiply));
        SetButtonInteractable(
            divideButton,
            levelManager.IsOperationAvailable(StampOperation.Divide));

        RefreshSubtractModeLabel();
    }

    private bool EnsureStampingMachine()
    {
        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        if (stampingMachine == null)
        {
            Debug.LogWarning("无法设置冲压运算：没有找到 StampingMachine。", this);
            return false;
        }

        return true;
    }

    private bool IsOperationAvailable(StampOperation selectedOperation)
    {
        if (levelManager != null &&
            !levelManager.IsOperationAvailable(selectedOperation))
        {
            Debug.LogWarning(
                $"当前关卡还不能使用运算：{GetOperationName(selectedOperation)}。",
                this);
            return false;
        }

        return true;
    }

    private void RefreshSubtractModeLabel()
    {
        if (subtractModeLabel == null)
        {
            return;
        }

        bool negativeMode = stampingMachine != null &&
                            stampingMachine.IsNegativeSubtractMode;
        string modeText = negativeMode
            ? "负数模式（小数 - 大数）"
            : "正数模式（大数 - 小数）";

        if (!negativeMode && levelManager != null &&
            !levelManager.IsNegativeSubtractUnlocked)
        {
            modeText += "\n负数模式未解锁";
        }

        subtractModeLabel.text = $"减法：{modeText}";
    }

    private string GetSubtractModeName()
    {
        return stampingMachine != null && stampingMachine.IsNegativeSubtractMode
            ? "负数模式（小数-大数）"
            : "正数模式（大数-小数）";
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
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
