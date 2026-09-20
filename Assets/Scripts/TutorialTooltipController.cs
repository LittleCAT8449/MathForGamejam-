using System.Collections;
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
    private bool modeRewardUnlocked;
    private bool initialMiningMachineHintShown;
    private bool stampingAreaHintShown;
    private bool operationHintShown;
    private Coroutine subtractModeLabelHideCoroutine;

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
    /// Hides the setup hints once the player presses the start-mining object.
    /// When no dedicated TMP fields are assigned, the fallback tooltip is the
    /// setup hint and is hidden as well.
    /// </summary>
    public void HidePowerAndMiningAreaTooltips()
    {
        ClearTooltip(powerTooltip);
        ClearTooltip(miningAreaTooltip);

        if (powerTooltip == null && miningAreaTooltip == null)
        {
            ClearTooltip(fallbackTooltip);
        }
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
            if (!stampingAreaHintShown)
            {
                ClearAllTooltips();
                ShowStampingHintOnce();
            }
        }
    }

    /// <summary>
    /// Shows the stamping-area hint when the camera first arrives at the
    /// number area after mining starts. This does not mark the stamping area
    /// as entered yet; the actual drag into the stamping collider still does
    /// that through OnEnteredStampingArea().
    /// </summary>
    public void ShowStampingAreaTooltip()
    {
        if (stampingAreaHintShown)
        {
            return;
        }

        ClearAllTooltips();
        ShowStampingHintOnce();
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
    /// Hides the delivery hint after a result has actually been placed in the
    /// settlement area. A failed drop leaves the hint untouched.
    /// </summary>
    public void HideSettlementTooltip()
    {
        ClearTooltip(settlementTooltip);
        if (settlementTooltip == null)
        {
            ClearTooltip(fallbackTooltip);
        }
    }

    /// <summary>
    /// Called after the first operation (symbol) reward is permanently
    /// unlocked. The message is shown immediately only when the player is in
    /// the stamping area; otherwise it appears upon entering that area.
    /// </summary>
    public void OnOperationRewardUnlocked(StampOperation operation)
    {
        // Kept as a callback for LevelManager. OperationTooltip is now shown
        // after BeginMining rather than when the reward is granted.
    }

    /// <summary>
    /// Shows the operation-selection hint after mining has started. The hint
    /// is shown only once for the current tutorial run.
    /// </summary>
    public void ShowOperationTooltip()
    {
        if (operationHintShown || !HasUnlockedAlternativeOperation())
        {
            return;
        }

        ClearAllTooltips();
        Show(operationTooltip, "点击锤头可以切换符号。");
        operationHintShown = true;
    }

    private bool HasUnlockedAlternativeOperation()
    {
        LevelManager levelManager = FindFirstObjectByType<LevelManager>();
        if (levelManager == null)
        {
            return false;
        }

        return levelManager.IsOperationAvailable(StampOperation.Subtract) ||
               levelManager.IsOperationAvailable(StampOperation.Multiply) ||
               levelManager.IsOperationAvailable(StampOperation.Divide);
    }

    /// <summary>
    /// Hides the operation-selection hint when its selection panel is opened
    /// or closed.
    /// </summary>
    public void HideOperationTooltip()
    {
        ClearTooltip(operationTooltip);
        if (operationTooltip == null)
        {
            ClearTooltip(fallbackTooltip);
        }
    }

    /// <summary>
    /// Runs the subtraction-mode label timer from this persistent controller.
    /// The operation-selection panel can be disabled immediately after a
    /// button click, so its own coroutine would otherwise be stopped.
    /// </summary>
    public void ScheduleSubtractModeLabelHide(
        StampOperationSelector selector,
        float duration)
    {
        if (subtractModeLabelHideCoroutine != null)
        {
            StopCoroutine(subtractModeLabelHideCoroutine);
            subtractModeLabelHideCoroutine = null;
        }

        if (selector == null || duration <= 0f)
        {
            selector?.HideSubtractModeLabelImmediately();
            return;
        }

        subtractModeLabelHideCoroutine = StartCoroutine(
            HideSubtractModeLabelAfterDelay(selector, duration));
    }

    private IEnumerator HideSubtractModeLabelAfterDelay(
        StampOperationSelector selector,
        float duration)
    {
        yield return new WaitForSeconds(duration);
        subtractModeLabelHideCoroutine = null;
        if (selector != null)
        {
            selector.HideSubtractModeLabelImmediately();
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
        modeRewardUnlocked = false;
        initialMiningMachineHintShown = false;
        stampingAreaHintShown = false;
        operationHintShown = false;

        ClearAllTooltips();
        if (showInitialHint)
        {
            ShowMiningMachineHint();
        }
    }

    private void ShowMiningMachineHint()
    {
        if (initialMiningMachineHintShown)
        {
            return;
        }

        ClearAllTooltips();
        Show(miningMachineTooltip, "这是数字采矿机。");
        initialMiningMachineHintShown = true;
    }

    private void ShowStampingHintOnce()
    {
        if (stampingAreaHintShown)
        {
            return;
        }

        Show(stampingAreaTooltip, "把数字放在凹槽内。");
        stampingAreaHintShown = true;
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
