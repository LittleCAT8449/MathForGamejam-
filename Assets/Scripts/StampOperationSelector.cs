using System.Collections.Generic;
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

    private Sprite addButtonSprite;
    private Sprite subtractButtonSprite;
    private Sprite multiplyButtonSprite;
    private Sprite divideButtonSprite;
    private Color addButtonColor = Color.white;
    private Color subtractButtonColor = Color.white;
    private Color multiplyButtonColor = Color.white;
    private Color divideButtonColor = Color.white;
    private bool buttonSpritesCached;

    // The scene currently contains one selector component on each operation
    // button. A selector can clear another button before the next selector
    // caches it, so keep the original graphics shared by all selectors.
    private static readonly Dictionary<int, Sprite> OriginalButtonSprites =
        new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Color> OriginalButtonColors =
        new Dictionary<int, Color>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetButtonGraphicCache()
    {
        OriginalButtonSprites.Clear();
        OriginalButtonColors.Clear();
    }

    [Header("减法模式显示")]
    [Tooltip("显示当前减法模式，例如：正数模式（大数-小数）。")]
    [SerializeField] private TMP_Text subtractModeLabel;
    [Tooltip("选择加法、乘法或除法时隐藏减法模式文字。")]
    [SerializeField] private bool hideLabelWhenNotSubtract = true;
    [SerializeField] private string positiveSubtractModeText =
        "正数模式（大数 - 小数）";
    [SerializeField] private string negativeSubtractModeText =
        "负数模式（小数 - 大数）";
    [SerializeField] private string negativeModeLockedText =
        "\n负数模式未解锁";

    private StampOperation lastLabelOperation;
    private bool lastLabelNegativeMode;
    private bool lastLabelNegativeUnlocked;
    private bool hasRefreshedLabel;

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

        CacheButtonSprites();
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

    private void Update()
    {
        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        if (stampingMachine == null)
        {
            return;
        }

        bool negativeUnlocked = levelManager != null &&
                                levelManager.IsNegativeSubtractUnlocked;
        if (!hasRefreshedLabel ||
            lastLabelOperation != stampingMachine.Operation ||
            lastLabelNegativeMode != stampingMachine.IsNegativeSubtractMode ||
            lastLabelNegativeUnlocked != negativeUnlocked)
        {
            RefreshSubtractModeLabel();
        }
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
        CacheButtonSprites();

        if (levelManager == null)
        {
            RefreshButton(addButton, true, addButtonSprite, addButtonColor);
            RefreshButton(subtractButton, true, subtractButtonSprite, subtractButtonColor);
            RefreshButton(multiplyButton, true, multiplyButtonSprite, multiplyButtonColor);
            RefreshButton(divideButton, true, divideButtonSprite, divideButtonColor);
            RefreshSubtractModeLabel();
            return;
        }

        RefreshButton(
            addButton,
            levelManager.IsOperationAvailable(StampOperation.Add),
            addButtonSprite,
            addButtonColor);
        RefreshButton(
            subtractButton,
            levelManager.IsOperationAvailable(StampOperation.Subtract),
            subtractButtonSprite,
            subtractButtonColor);
        RefreshButton(
            multiplyButton,
            levelManager.IsOperationAvailable(StampOperation.Multiply),
            multiplyButtonSprite,
            multiplyButtonColor);
        RefreshButton(
            divideButton,
            levelManager.IsOperationAvailable(StampOperation.Divide),
            divideButtonSprite,
            divideButtonColor);

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

        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        if (stampingMachine == null)
        {
            subtractModeLabel.text = string.Empty;
            return;
        }

        if (hideLabelWhenNotSubtract &&
            stampingMachine.Operation != StampOperation.Subtract)
        {
            subtractModeLabel.text = string.Empty;
            CacheDisplayedLabelState();
            return;
        }

        bool negativeMode = stampingMachine != null &&
                            stampingMachine.IsNegativeSubtractMode;
        string modeText = negativeMode
            ? negativeSubtractModeText
            : positiveSubtractModeText;

        if (!negativeMode && levelManager != null &&
            !levelManager.IsNegativeSubtractUnlocked)
        {
            modeText += negativeModeLockedText;
        }

        subtractModeLabel.text = $"减法：{modeText}";
        CacheDisplayedLabelState();
    }

    private void CacheDisplayedLabelState()
    {
        if (stampingMachine == null)
        {
            return;
        }

        lastLabelOperation = stampingMachine.Operation;
        lastLabelNegativeMode = stampingMachine.IsNegativeSubtractMode;
        lastLabelNegativeUnlocked = levelManager != null &&
                                    levelManager.IsNegativeSubtractUnlocked;
        hasRefreshedLabel = true;
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

    private void CacheButtonSprites()
    {
        if (buttonSpritesCached)
        {
            return;
        }

        addButtonSprite = GetButtonSprite(addButton);
        subtractButtonSprite = GetButtonSprite(subtractButton);
        multiplyButtonSprite = GetButtonSprite(multiplyButton);
        divideButtonSprite = GetButtonSprite(divideButton);
        addButtonColor = GetButtonColor(addButton);
        subtractButtonColor = GetButtonColor(subtractButton);
        multiplyButtonColor = GetButtonColor(multiplyButton);
        divideButtonColor = GetButtonColor(divideButton);
        buttonSpritesCached = true;
    }

    private static Sprite GetButtonSprite(Button button)
    {
        if (button == null || button.image == null)
        {
            return null;
        }

        int instanceId = button.GetInstanceID();
        if (!OriginalButtonSprites.ContainsKey(instanceId))
        {
            OriginalButtonSprites.Add(instanceId, button.image.sprite);
        }

        return OriginalButtonSprites[instanceId];
    }

    private static Color GetButtonColor(Button button)
    {
        if (button == null || button.image == null)
        {
            return Color.white;
        }

        int instanceId = button.GetInstanceID();
        if (!OriginalButtonColors.ContainsKey(instanceId))
        {
            OriginalButtonColors.Add(instanceId, button.image.color);
        }

        return OriginalButtonColors[instanceId];
    }

    private void RefreshButton(
        Button button,
        bool operationAvailable,
        Sprite originalSprite,
        Color originalColor)
    {
        if (button == null)
        {
            return;
        }

        SetButtonInteractable(button, operationAvailable);

        if (button.image != null)
        {
            button.image.sprite = operationAvailable ? originalSprite : null;
            button.image.color = operationAvailable
                ? originalColor
                : new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
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
