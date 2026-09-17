using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

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

    private const string ProgressFileName = "level_progress.json";

    private LevelProgressData progress;
    private LevelConfig currentLevel;
    private int currentLevelIndex = -1;
    private bool isAdvancingLevel;
    private readonly List<decimal> currentTargets = new List<decimal>();

    public IReadOnlyList<LevelConfig> Levels => levels;
    public LevelConfig CurrentLevel => currentLevel;
    public int CurrentLevelIndex => currentLevelIndex;
    public LevelProgressData Progress => progress;
    public bool IsCurrentLevelCompleted =>
        currentLevel != null && progress != null &&
        progress.completedLevelIds.Contains(currentLevel.LevelId);
    public bool IsNegativeSubtractUnlocked =>
        progress != null && progress.negativeSubtractUnlocked;

    private string ProgressPath =>
        Path.Combine(Application.persistentDataPath, ProgressFileName);

    private void Awake()
    {
        LoadProgress();

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
            LoadLevel(startingLevelIndex);
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

        SyncUnlockedMachineRewards();

        if (levels.Count == 0)
        {
            Debug.LogWarning(
                $"{name}：没有配置关卡，请在 Inspector 的 Levels 中添加 LevelConfig。",
                this);
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
        if (progress == null || !progress.unlockedOperations.Contains(operation))
        {
            return false;
        }

        if (currentLevel == null || currentLevel.AllowedOperations == null ||
            currentLevel.AllowedOperations.Count == 0)
        {
            return true;
        }

        return currentLevel.AllowedOperations.Contains(operation);
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
        SyncUnlockedMachineRewards();
    }

    private void HandleNumberPlaced(NumberToken token)
    {
        if (token == null || currentLevel == null || IsCurrentLevelCompleted)
        {
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
                GrantReward(currentLevel.Rewards[i], i);
            }
        }

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

        if (LoadNextLevel())
        {
            Debug.Log($"已重置本局并进入下一关：{currentLevel.DisplayName}。", this);
        }
        else
        {
            Debug.Log("所有关卡已完成，当前关卡是最后一关。", this);
        }

        isAdvancingLevel = false;
    }

    private void GrantReward(LevelRewardConfig reward, int rewardIndex)
    {
        if (reward == null)
        {
            return;
        }

        string rewardId = reward.GetPersistentId(
            currentLevel.LevelId,
            rewardIndex);
        if (!progress.unlockedRewardIds.Contains(rewardId))
        {
            progress.unlockedRewardIds.Add(rewardId);
        }

        switch (reward.RewardType)
        {
            case LevelRewardType.UnlockOperation:
                if (!progress.unlockedOperations.Contains(reward.Operation))
                {
                    progress.unlockedOperations.Add(reward.Operation);
                }

                Debug.Log($"永久解锁运算：{reward.Operation}。", this);
                break;
            case LevelRewardType.UnlockNegativeSubtraction:
                progress.negativeSubtractUnlocked = true;
                Debug.Log("永久解锁减法负数模式。", this);
                break;
            case LevelRewardType.UnlockMiningMachine:
                string machineName = reward.MachinePrefab != null
                    ? reward.MachinePrefab.name
                    : rewardId;
                Debug.Log($"永久解锁采矿机：{machineName}。", this);
                break;
        }
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
    }

    private void UpdateTargetLabel()
    {
        if (targetLabel == null)
        {
            return;
        }

        targetLabel.text = currentLevel == null
            ? string.Empty
            : $"目标：{currentLevel.TargetNumbersText}";
    }
}
