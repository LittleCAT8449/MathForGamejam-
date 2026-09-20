using TMPro;
using UnityEngine;

/// <summary>
/// Controls the first-time tutorial messages used by the mining and stamping
/// flow. Assign one TMP object per location when different messages should be
/// visible at the same time. If a field is empty, the scene's ToolTip TMP is
/// used as a fallback so the tutorial still works without extra setup.
/// </summary>
public sealed class TutorialTooltipController : MonoBehaviour
{
    public static TutorialTooltipController Instance { get; private set; }

    [Header("提示文本")]
    [SerializeField] private TMP_Text fallbackTooltip;
    [SerializeField] private TMP_Text miningMachineTooltip;
    [SerializeField] private TMP_Text powerTooltip;
    [SerializeField] private TMP_Text miningAreaTooltip;
    [SerializeField] private TMP_Text stampingAreaTooltip;
    [SerializeField] private TMP_Text settlementTooltip;
    [SerializeField] private TMP_Text operationTooltip;
    [SerializeField] private TMP_Text modeTooltip;

    [Header("首次显示控制")]
    [SerializeField] private bool showInitialMiningMachineHint = true;

    private bool miningMachineClicked;
    private bool miningMachineDeployed;
    private bool enteredStampingArea;
    private bool numberClicked;
    private bool firstCombinationCompleted;
    private bool operationRewardUnlocked;
    private bool modeRewardUnlocked;

    /// <summary>
    /// Creates a controller automatically when the scene does not have one.
    /// This keeps the callbacks safe while still allowing the component to be
    /// added to Manager and configured in the Inspector.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAtSceneLoad()
    {
        FindOrCreate();
    }

    public static TutorialTooltipController FindOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindFirstObjectByType<TutorialTooltipController>();
        if (Instance != null)
        {
            return Instance;
        }

        GameObject controllerObject = new GameObject("TutorialTooltipController");
        Instance = controllerObject.AddComponent<TutorialTooltipController>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveFallbackTooltip();
    }

    private void Start()
    {
        ResolveFallbackTooltip();
        if (showInitialMiningMachineHint)
        {
            ShowMiningMachineHint();
        }
    }

    /// <summary>
    /// Called when a mining machine is selected by mouse or by the click
    /// deployment flow.
    /// </summary>
    public void OnMiningMachineSelected(MiningMachineItem machine)
    {
        if (miningMachineClicked)
        {
            return;
        }

        miningMachineClicked = true;
        ClearAllTooltips();
        Show(miningMachineTooltip, "把它放在矿场上。");
    }

    /// <summary>
    /// Called after a machine has been successfully placed in the mining area.
    /// </summary>
    public void OnMiningMachineDeployed(MiningMachineItem machine)
    {
        if (miningMachineDeployed)
        {
            return;
        }

        miningMachineDeployed = true;
        ClearAllTooltips();

        // With dedicated TMP fields both messages remain visible. When only
        // the fallback TMP is configured, combine them instead of letting the
        // second Show call overwrite the first one.
        if (powerTooltip == null && miningAreaTooltip == null &&
            fallbackTooltip != null)
        {
            Show(fallbackTooltip, "这是电源。\n采矿机需要连接电源才会开始工作。");
            return;
        }

        Show(powerTooltip, "这是电源。");
        Show(miningAreaTooltip, "采矿机需要连接电源才会开始工作。");
    }

    /// <summary>
    /// Called when a number is dragged into, or is being dragged over, the
    /// stamping area.
    /// </summary>
    public void OnEnteredStampingArea()
    {
        if (!enteredStampingArea)
        {
            enteredStampingArea = true;
            ClearAllTooltips();
            Show(stampingAreaTooltip, "把数字放在凹槽内。");
        }

        if (operationRewardUnlocked)
        {
            ClearAllTooltips();
            Show(operationTooltip, "点击锤头可以切换符号。");
        }
    }

    /// <summary>
    /// Called on the first click of a number token.
    /// </summary>
    public void OnNumberClicked(NumberToken token)
    {
        if (numberClicked || firstCombinationCompleted)
        {
            return;
        }

        numberClicked = true;
        ClearAllTooltips();
        Show(stampingAreaTooltip, "点击拉杆可以加工数字。");
    }

    /// <summary>
    /// Called after the first result token has been created by the press.
    /// </summary>
    public void OnFirstNumberCombined()
    {
        if (firstCombinationCompleted)
        {
            return;
        }

        firstCombinationCompleted = true;
        ClearAllTooltips();
        Show(settlementTooltip, "可以把数字拖入交付区域内。");
    }

    /// <summary>
    /// Called after the first operation (symbol) reward is permanently
    /// unlocked. The message is shown immediately only when the player is in
    /// the stamping area; otherwise it appears upon entering that area.
    /// </summary>
    public void OnOperationRewardUnlocked(StampOperation operation)
    {
        if (operationRewardUnlocked)
        {
            return;
        }

        operationRewardUnlocked = true;
        if (enteredStampingArea)
        {
            ClearAllTooltips();
            Show(operationTooltip, "点击锤头可以切换符号。");
        }
    }

    /// <summary>
    /// Called after a new subtraction mode is permanently unlocked.
    /// </summary>
    public void OnModeRewardUnlocked()
    {
        if (modeRewardUnlocked)
        {
            return;
        }

        modeRewardUnlocked = true;
        ClearAllTooltips();
        Show(modeTooltip, "点击改变运算模式。");
    }

    public void ResetTutorialState(bool showInitialHint = true)
    {
        miningMachineClicked = false;
        miningMachineDeployed = false;
        enteredStampingArea = false;
        numberClicked = false;
        firstCombinationCompleted = false;
        operationRewardUnlocked = false;
        modeRewardUnlocked = false;

        ClearAllTooltips();
        if (showInitialHint)
        {
            ShowMiningMachineHint();
        }
    }

    private void ShowMiningMachineHint()
    {
        ClearAllTooltips();
        Show(miningMachineTooltip, "这是数字采矿机。");
    }

    private void ClearAllTooltips()
    {
        ClearTooltip(fallbackTooltip);
        ClearTooltip(miningMachineTooltip);
        ClearTooltip(powerTooltip);
        ClearTooltip(miningAreaTooltip);
        ClearTooltip(stampingAreaTooltip);
        ClearTooltip(settlementTooltip);
        ClearTooltip(operationTooltip);
        ClearTooltip(modeTooltip);
    }

    private static void ClearTooltip(TMP_Text tooltip)
    {
        if (tooltip == null)
        {
            return;
        }

        tooltip.text = string.Empty;
        tooltip.gameObject.SetActive(false);
    }

    private void Show(TMP_Text preferred, string message)
    {
        TMP_Text target = preferred != null ? preferred : fallbackTooltip;
        if (target == null)
        {
            return;
        }

        target.text = message;
        target.gameObject.SetActive(true);
    }

    private void ResolveFallbackTooltip()
    {
        if (fallbackTooltip != null)
        {
            return;
        }

        GameObject namedTooltip = GameObject.Find("ToolTip");
        if (namedTooltip == null)
        {
            namedTooltip = GameObject.Find("ToolTip (1)");
        }

        if (namedTooltip != null)
        {
            fallbackTooltip = namedTooltip.GetComponent<TMP_Text>();
        }

        if (fallbackTooltip == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            if (texts.Length > 0)
            {
                fallbackTooltip = texts[0];
            }
        }
    }
}
