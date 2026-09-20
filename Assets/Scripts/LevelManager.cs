using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class LevelProgressData
{
    public int highestUnlockedLevelIndex;
    public List<string> completedLevelIds = new List<string>();
    public List<string> unlockedRewardIds = new List<string>();
    public List<StampOperation> unlockedOperations =
        new List<StampOperation>();
    public bool negativeSubtractUnlocked;
}

/// <summary>
/// Loads level assets, checks delivered result numbers, grants permanent
/// rewards, and saves progression independently from the current scene.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [SerializeField] private List<LevelConfig> levels =
        new List<LevelConfig>();
    [SerializeField, Min(0)] private int startingLevelIndex;
    [SerializeField] private SettlementArea settlementArea;
    [SerializeField] private MiningMachineWaitingArea waitingArea;
    [SerializeField] private TMP_Text targetLabel;
    [SerializeField] private bool loadStartingLevelOnAwake = true;

    [Header("关卡目标提示")]
    [Tooltip("显示当前关卡需要提交的数字，可使用 TextMeshPro 或 TextMeshProUGUI。")]
    [SerializeField] private TMP_Text requiredNumbersLabel;
    [SerializeField] private string requiredNumbersFormat = "本关需要提交：{0}";

    [Header("通关提示")]
    [Tooltip("最后一关通关后显示的 TextMeshPro 文本。可以使用 TextMeshPro 或 TextMeshProUGUI。")]
    [SerializeField] private TMP_Text completionLabel;
    [SerializeField] private string completionMessage = "通关完成！按 Y 键重新开始";

    private const string ProgressFileName = "level_progress.json";

    private LevelProgressData progress;
    private LevelConfig currentLevel;
    private int currentLevelIndex = -1;
    private bool isAdvancingLevel;
    private bool gameCompleted;
    private readonly List<decimal> currentTargets = new List<decimal>();

    /// <summary>
    /// Raised when a reward changes which operation buttons are available.
    /// </summary>
    public event Action OperationAvailabilityChanged;

    public IReadOnlyList<LevelConfig> Levels => levels;
    public LevelConfig CurrentLevel => currentLevel;
    public int CurrentLevelIndex => currentLevelIndex;
    public LevelProgressData Progress => progress;
    public bool IsCurrentLevelCompleted =>
        currentLevel != null && progress != null &&
        progress.completedLevelIds.Contains(currentLevel.LevelId);
    public bool IsNegativeSubtractUnlocked =>
        progress != null && progress.negativeSubtractUnlocked;
    public bool IsGameCompleted => gameCompleted;

    private string ProgressPath =>
        Path.Combine(Application.persistentDataPath, ProgressFileName);

    private void Awake()
    {
        LoadProgress();
        HideCompletionMessage();
        UpdateTargetLabel();

        if (settlementArea == null)
        {
            settlementArea = FindFirstObjectByType<SettlementArea>();
        }

        if (waitingArea == null)
        {
            waitingArea = FindFirstObjectByType<MiningMachineWaitingArea>();
        }

        if (loadStartingLevelOnAwake)
        {
            // Continue from the highest level unlocked in the saved progress.
            // A fresh save still starts at startingLevelIndex (normally 0),
            // while a player who already completed level 1 opens level 2 and
            // can use the newly unlocked operations there.
            int savedLevelIndex = progress != null
                ? progress.highestUnlockedLevelIndex
                : startingLevelIndex;
            int levelToLoad = Mathf.Max(startingLevelIndex, savedLevelIndex);
            if (levels != null && levels.Count > 0)
            {
                levelToLoad = Mathf.Clamp(levelToLoad, 0, levels.Count - 1);
            }

            LoadLevel(levelToLoad);
        }
    }

    private void OnEnable()
    {
        if (settlementArea != null)
        {
            settlementArea.NumberPlaced += HandleNumberPlaced;
        }
    }

    private void Start()
    {
        // A SettlementArea may be assigned or found after OnEnable.
        if (settlementArea == null)
        {
            settlementArea = FindFirstObjectByType<SettlementArea>();
        }

        if (settlementArea != null)
        {
            settlementArea.NumberPlaced -= HandleNumberPlaced;
            settlementArea.NumberPlaced += HandleNumberPlaced;
        }

        // Re-apply rewards for levels that were completed before a reward was
        // added or changed in the LevelConfig asset. This also repairs saves
        // created by an older build that recorded completion without adding
        // the operation to unlockedOperations.
        SyncRewardsForCompletedLevels();
        SyncUnlockedMachineRewards();

        if (levels.Count == 0)
        {
            Debug.LogWarning(
                $"{name}：没有配置关卡，请在 Inspector 的 Levels 中添加 LevelConfig。",
                this);
        }

        if (HasCompletedAllLevels())
        {
            ShowCompletionMessage();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.f8Key.wasPressedThisFrame)
        {
            ClearCompletedLevelsForTesting();
            return;
        }

        if (gameCompleted && !GameResetClick.IsModalOpen &&
            Keyboard.current != null &&
            Keyboard.current.yKey.wasPressedThisFrame)
        {
            ResetGameToBeginning();
        }
    }

    private void OnDisable()
    {
        if (settlementArea != null)
        {
            settlementArea.NumberPlaced -= HandleNumberPlaced;
        }
    }

    /// <summary>
    /// Loads a level by its list index. Locked levels cannot be loaded.
    /// </summary>
    public bool LoadLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Count)
        {
            Debug.LogWarning($"无法加载关卡：索引 {index} 不存在。", this);
            return false;
        }

        if (progress != null && index > progress.highestUnlockedLevelIndex)
        {
            Debug.LogWarning($"关卡 {index + 1} 尚未解锁。", this);
            return false;
        }

        LevelConfig config = levels[index];
        if (config == null)
        {
            Debug.LogWarning($"无法加载关卡：索引 {index} 的配置为空。", this);
            return false;
        }

        if (!config.TryGetTargets(currentTargets))
        {
            Debug.LogWarning(
                $"关卡 {config.LevelId} 的目标数字格式无效：{config.TargetNumbersText}。",
                config);
            return false;
        }

        currentLevel = config;
        currentLevelIndex = index;
        gameCompleted = false;
        HideCompletionMessage();
        OperationAvailabilityChanged?.Invoke();
        UpdateTargetLabel();
        Debug.Log(
            $"已加载关卡 {config.DisplayName}，目标数字：{config.TargetNumbersText}。",
            config);
        return true;
    }

    public bool LoadNextLevel()
    {
        return LoadLevel(currentLevelIndex + 1);
    }

    public bool IsOperationAvailable(StampOperation operation)
    {
        // Operation availability is global and permanent. Once a reward has
        // unlocked an operation, every level can use it.
        return progress != null &&
               progress.unlockedOperations.Contains(operation);
    }

    public bool HasReward(string rewardId)
    {
        return progress != null &&
               !string.IsNullOrWhiteSpace(rewardId) &&
               progress.unlockedRewardIds.Contains(rewardId);
    }

    /// <summary>
    /// Ensures every permanently unlocked mining-machine reward is present in
    /// the waiting area for this scene.
    /// </summary>
    public void SyncUnlockedMachineRewards()
    {
        if (progress == null || levels == null)
        {
            return;
        }

        if (waitingArea == null)
        {
            waitingArea = FindFirstObjectByType<MiningMachineWaitingArea>();
        }

        if (waitingArea == null)
        {
            Debug.LogWarning(
                "无法生成永久解锁采矿机：没有找到 MiningMachineWaitingArea。",
                this);
            return;
        }

        waitingArea.EnsureInitialMachinesSpawned();

        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            LevelConfig level = levels[levelIndex];
            if (level == null || level.Rewards == null)
            {
                continue;
            }

            for (int rewardIndex = 0; rewardIndex < level.Rewards.Count; rewardIndex++)
            {
                LevelRewardConfig reward = level.Rewards[rewardIndex];
                if (reward == null ||
                    reward.RewardType != LevelRewardType.UnlockMiningMachine ||
                    reward.MachinePrefab == null)
                {
                    continue;
                }

                string rewardId = reward.GetPersistentId(level.LevelId, rewardIndex);
                if (progress.unlockedRewardIds.Contains(rewardId))
                {
                    waitingArea.SpawnRewardMachine(reward.MachinePrefab, rewardId);
                }
            }
        }
    }

    public void SaveProgress()
    {
        if (progress == null)
        {
            return;
        }

        try
        {
            string directory = Path.GetDirectoryName(ProgressPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(ProgressPath, JsonUtility.ToJson(progress, true));
        }
        catch (Exception exception)
        {
            Debug.LogError($"保存关卡进度失败：{exception.Message}", this);
        }
    }

    public void ReloadProgress()
    {
        LoadProgress();
        UpdateTargetLabel();
        SyncRewardsForCompletedLevels();
        SyncUnlockedMachineRewards();
    }

    /// <summary>
    /// Clears saved level completion and rewards so the level flow can be
    /// tested from the beginning. Bound to the F8 debug shortcut.
    /// </summary>
    public void ClearCompletedLevelsForTesting()
    {
        ResetGameProgress();
        Debug.Log("F8：已清除通关记录、奖励和运算解锁，回到初始关卡。", this);
    }

    /// <summary>
    /// Clears the complete game progress and returns to the configured
    /// starting level. This is used by the final-level Y-key restart.
    /// </summary>
    public void ResetGameToBeginning()
    {
        ResetGameProgress();
        Debug.Log("Y：已重置游戏，回到初始关卡。", this);
    }

    private void ResetGameProgress()
    {
        if (progress == null)
        {
            progress = new LevelProgressData();
        }

        progress.highestUnlockedLevelIndex = 0;
        progress.completedLevelIds.Clear();
        progress.unlockedRewardIds.Clear();
        progress.unlockedOperations.Clear();
        progress.unlockedOperations.Add(StampOperation.Add);
        progress.negativeSubtractUnlocked = false;

        StopAllCoroutines();
        isAdvancingLevel = false;
        gameCompleted = false;
        HideCompletionMessage();
        SaveProgress();

        GameResetClick resetClick = FindFirstObjectByType<GameResetClick>();
        if (resetClick != null)
        {
            resetClick.ResetRoundImmediately();
        }

        if (levels != null && levels.Count > 0)
        {
            int levelToLoad = Mathf.Clamp(startingLevelIndex, 0, levels.Count - 1);
            LoadLevel(levelToLoad);
        }

        SyncUnlockedMachineRewards();
        TutorialTooltipController.FindOrCreate().ResetTutorialState();
    }

    private void HandleNumberPlaced(NumberToken token)
    {
        if (token == null || currentLevel == null)
        {
            return;
        }

        if (IsCurrentLevelCompleted)
        {
            Debug.Log(
                $"关卡 {currentLevel.DisplayName} 已在存档中完成，本次交付不会重复触发通关。",
                this);
            return;
        }

        if (!currentLevel.TryGetTargets(currentTargets))
        {
            return;
        }

        if (!AreAllTargetsDelivered(currentTargets))
        {
            Debug.Log(
                $"关卡 {currentLevel.LevelId} 交付进度：{GetDeliveredCount()}/{currentTargets.Count}，最近交付数字：{token.Value}。",
                token);
            return;
        }

        CompleteCurrentLevel();
    }

    private bool AreAllTargetsDelivered(IReadOnlyList<decimal> targets)
    {
        if (settlementArea == null || targets == null ||
            settlementArea.PlacedNumbers == null ||
            settlementArea.PlacedNumbers.Count != targets.Count)
        {
            return false;
        }

        bool[] matched = new bool[settlementArea.PlacedNumbers.Count];
        foreach (decimal target in targets)
        {
            bool found = false;
            for (int i = 0; i < settlementArea.PlacedNumbers.Count; i++)
            {
                NumberToken token = settlementArea.PlacedNumbers[i];
                if (!matched[i] && token != null && token.Value == target)
                {
                    matched[i] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private int GetDeliveredCount()
    {
        if (settlementArea == null || settlementArea.PlacedNumbers == null)
        {
            return 0;
        }

        int count = 0;
        foreach (NumberToken token in settlementArea.PlacedNumbers)
        {
            if (token != null)
            {
                count++;
            }
        }

        return count;
    }

    private void CompleteCurrentLevel()
    {
        if (currentLevel == null || progress == null ||
            IsCurrentLevelCompleted || isAdvancingLevel)
        {
            return;
        }

        progress.completedLevelIds.Add(currentLevel.LevelId);
        progress.highestUnlockedLevelIndex = Mathf.Max(
            progress.highestUnlockedLevelIndex,
            currentLevelIndex + 1);

        if (currentLevel.Rewards != null)
        {
            for (int i = 0; i < currentLevel.Rewards.Count; i++)
            {
                GrantReward(currentLevel, currentLevel.Rewards[i], i);
            }
        }

        // A reward may have unlocked a new operation. Refresh any open
        // operation-selection UI immediately, before the next level loads.
        OperationAvailabilityChanged?.Invoke();

        SyncUnlockedMachineRewards();

        SaveProgress();
        Debug.Log($"关卡完成：{currentLevel.DisplayName}。奖励已永久保存。", this);
        isAdvancingLevel = true;
        StartCoroutine(ResetAndLoadNextLevel());
    }

    private IEnumerator ResetAndLoadNextLevel()
    {
        // PlaceNumber is still returning to NumberTokenDrag when this event is
        // raised. Wait one frame before destroying the delivered token.
        yield return null;

        GameResetClick resetClick = FindFirstObjectByType<GameResetClick>();
        if (resetClick == null)
        {
            Debug.LogError(
                "关卡完成后无法自动重置：场景中没有找到 GameResetClick。",
                this);
            isAdvancingLevel = false;
            yield break;
        }

        resetClick.ResetRoundImmediately();
        yield return null;

        int nextLevelIndex = currentLevelIndex + 1;
        if (nextLevelIndex < levels.Count && LoadNextLevel())
        {
            Debug.Log($"已重置本局并进入下一关：{currentLevel.DisplayName}。", this);
        }
        else
        {
            if (nextLevelIndex >= levels.Count)
            {
                ShowCompletionMessage();
                Debug.Log("所有关卡已完成，当前关卡是最后一关。按 Y 键可以重置游戏。", this);
            }
            else
            {
                Debug.LogError("关卡完成后无法加载下一关，请检查 Levels 列表和关卡配置。", this);
            }
        }

        isAdvancingLevel = false;
    }

    private bool HasCompletedAllLevels()
    {
        if (progress == null || levels == null || levels.Count == 0)
        {
            return false;
        }

        foreach (LevelConfig level in levels)
        {
            if (level == null ||
                !progress.completedLevelIds.Contains(level.LevelId))
            {
                return false;
            }
        }

        return true;
    }

    private void ShowCompletionMessage()
    {
        gameCompleted = true;

        if (completionLabel == null)
        {
            Debug.LogWarning(
                $"{name}：已完成最后一关，但没有设置 Completion Label。请绑定 TMP 文本以显示通关提示。",
                this);
            return;
        }

        completionLabel.text = completionMessage;
        completionLabel.gameObject.SetActive(true);
    }

    private void HideCompletionMessage()
    {
        if (completionLabel != null)
        {
            completionLabel.gameObject.SetActive(false);
        }
    }

    private bool SyncRewardsForCompletedLevels()
    {
        if (progress == null || levels == null)
        {
            return false;
        }

        bool changed = false;
        foreach (LevelConfig level in levels)
        {
            if (level == null ||
                !progress.completedLevelIds.Contains(level.LevelId) ||
                level.Rewards == null)
            {
                continue;
            }

            for (int rewardIndex = 0; rewardIndex < level.Rewards.Count; rewardIndex++)
            {
                changed |= GrantReward(level, level.Rewards[rewardIndex], rewardIndex);
            }
        }

        if (changed)
        {
            OperationAvailabilityChanged?.Invoke();
            SaveProgress();
            Debug.Log("已同步已通关关卡的奖励，运算解锁状态已更新。", this);
        }

        return changed;
    }

    private bool GrantReward(LevelConfig level, LevelRewardConfig reward, int rewardIndex)
    {
        if (level == null || reward == null || progress == null)
        {
            return false;
        }

        string rewardId = reward.GetPersistentId(
            level.LevelId,
            rewardIndex);
        bool changed = false;
        if (!progress.unlockedRewardIds.Contains(rewardId))
        {
            progress.unlockedRewardIds.Add(rewardId);
            changed = true;
        }

        switch (reward.RewardType)
        {
            case LevelRewardType.UnlockOperation:
                if (!progress.unlockedOperations.Contains(reward.Operation))
                {
                    progress.unlockedOperations.Add(reward.Operation);
                    changed = true;
                    Debug.Log($"永久解锁运算：{reward.Operation}。", this);
                    TutorialTooltipController.FindOrCreate()
                        .OnOperationRewardUnlocked(reward.Operation);
                }
                break;
            case LevelRewardType.UnlockNegativeSubtraction:
                if (!progress.negativeSubtractUnlocked)
                {
                    progress.negativeSubtractUnlocked = true;
                    changed = true;
                    Debug.Log("永久解锁减法负数模式。", this);
                    TutorialTooltipController.FindOrCreate()
                        .OnModeRewardUnlocked();
                }
                break;
            case LevelRewardType.UnlockPositiveSubtraction:
                if (!progress.unlockedOperations.Contains(StampOperation.Subtract))
                {
                    progress.unlockedOperations.Add(StampOperation.Subtract);
                    changed = true;
                    Debug.Log("永久解锁减法正数模式。", this);
                    TutorialTooltipController.FindOrCreate()
                        .OnModeRewardUnlocked();
                }
                break;
            case LevelRewardType.UnlockMiningMachine:
                if (changed)
                {
                    string machineName = reward.MachinePrefab != null
                        ? reward.MachinePrefab.name
                        : rewardId;
                    Debug.Log($"永久解锁采矿机：{machineName}。", this);
                }
                break;
        }

        return changed;
    }

    private void LoadProgress()
    {
        try
        {
            if (File.Exists(ProgressPath))
            {
                progress = JsonUtility.FromJson<LevelProgressData>(
                    File.ReadAllText(ProgressPath));
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取关卡进度失败，将使用新存档：{exception.Message}", this);
        }

        if (progress == null)
        {
            progress = new LevelProgressData();
        }

        if (progress.completedLevelIds == null)
        {
            progress.completedLevelIds = new List<string>();
        }

        if (progress.unlockedRewardIds == null)
        {
            progress.unlockedRewardIds = new List<string>();
        }

        if (progress.unlockedOperations == null)
        {
            progress.unlockedOperations = new List<StampOperation>();
        }

        // Addition is available in a new game by default.
        if (!progress.unlockedOperations.Contains(StampOperation.Add))
        {
            progress.unlockedOperations.Add(StampOperation.Add);
        }

        OperationAvailabilityChanged?.Invoke();
    }

    private void UpdateTargetLabel()
    {
        string targetText = currentLevel == null
            ? string.Empty
            : currentLevel.TargetNumbersText;

        if (targetLabel != null)
        {
            targetLabel.text = string.IsNullOrEmpty(targetText)
                ? string.Empty
                : $"目标：{targetText}";
        }

        if (requiredNumbersLabel != null)
        {
            requiredNumbersLabel.text = string.IsNullOrEmpty(targetText)
                ? string.Empty
                : string.Format(requiredNumbersFormat, targetText);
            requiredNumbersLabel.gameObject.SetActive(!string.IsNullOrEmpty(targetText));
        }
    }
}
