using System;
using System.Collections.Generic;
using UnityEngine;

public enum LevelRewardType
{
    UnlockOperation,
    /// <summary>永久解锁小数减大数的负数减法模式。</summary>
    UnlockNegativeSubtraction,
    UnlockMiningMachine,
    /// <summary>永久解锁大数减小数的正数减法模式。</summary>
    UnlockPositiveSubtraction
}

[Serializable]
public class LevelRewardConfig
{
    [Tooltip("用于永久存档的唯一 ID。留空时会根据奖励内容自动生成。")]
    [SerializeField] private string rewardId;
    [Tooltip("奖励类型。可直接选择正数减法或负数减法模式。")]
    [SerializeField] private LevelRewardType rewardType;
    [SerializeField] private StampOperation operation;
    [SerializeField] private MiningMachineItem machinePrefab;

    public string RewardId => rewardId;
    public LevelRewardType RewardType => rewardType;
    public StampOperation Operation => operation;
    public MiningMachineItem MachinePrefab => machinePrefab;

    public string GetPersistentId(string levelId, int rewardIndex)
    {
        if (!string.IsNullOrWhiteSpace(rewardId))
        {
            return rewardId.Trim();
        }

        switch (rewardType)
        {
            case LevelRewardType.UnlockOperation:
                return $"{levelId}:operation:{operation}";
            case LevelRewardType.UnlockNegativeSubtraction:
                return $"{levelId}:subtract-negative";
            case LevelRewardType.UnlockPositiveSubtraction:
                return $"{levelId}:subtract-positive";
            case LevelRewardType.UnlockMiningMachine:
                string machineName = machinePrefab != null
                    ? machinePrefab.name
                    : "unknown-machine";
                return $"{levelId}:machine:{machineName}:{rewardIndex}";
            default:
                return $"{levelId}:reward:{rewardIndex}";
        }
    }
}

/// <summary>
/// Static data for one playable level. Create assets through
/// Assets/Create/Game/Level Config.
/// </summary>
[CreateAssetMenu(fileName = "Level_01", menuName = "Game/Level Config")]
public class LevelConfig : ScriptableObject
{
    [SerializeField] private string levelId = "level_01";
    [SerializeField] private string displayName = "第 1 关";
    [HideInInspector]
    [SerializeField] private string targetNumber = "0";
    [Tooltip("按顺序添加本关需要交付的所有数字，支持整数和小数。旧关卡留空时会使用原来的单目标字段。")]
    [SerializeField] private List<string> targetNumbers =
        new List<string>();
    [SerializeField] private List<LevelRewardConfig> rewards =
        new List<LevelRewardConfig>();

    public string LevelId => string.IsNullOrWhiteSpace(levelId)
        ? name
        : levelId.Trim();
    public string DisplayName => displayName;
    public string TargetNumberText => GetTargetNumberTexts()[0];
    public string TargetNumbersText => string.Join("、", GetTargetNumberTexts());
    public IReadOnlyList<LevelRewardConfig> Rewards => rewards;

    public bool TryGetTargets(List<decimal> targets)
    {
        if (targets == null)
        {
            return false;
        }

        targets.Clear();
        List<string> numberTexts = GetTargetNumberTexts();
        foreach (string numberText in numberTexts)
        {
            if (!decimal.TryParse(
                    numberText,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal value))
            {
                targets.Clear();
                return false;
            }

            targets.Add(value);
        }

        return targets.Count > 0;
    }

    public bool TryGetTarget(out decimal target)
    {
        List<decimal> targets = new List<decimal>();
        bool valid = TryGetTargets(targets);
        target = valid ? targets[0] : 0m;
        return valid;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(levelId))
        {
            levelId = name;
        }

        if (targetNumbers == null)
        {
            targetNumbers = new List<string>();
        }

        if (rewards == null)
        {
            rewards = new List<LevelRewardConfig>();
        }
    }

    private List<string> GetTargetNumberTexts()
    {
        if (targetNumbers != null && targetNumbers.Count > 0)
        {
            List<string> values = new List<string>(targetNumbers.Count);
            foreach (string target in targetNumbers)
            {
                values.Add(string.IsNullOrWhiteSpace(target) ? "0" : target.Trim());
            }

            return values;
        }

        return new List<string>
        {
            string.IsNullOrWhiteSpace(targetNumber) ? "0" : targetNumber.Trim()
        };
    }
}
