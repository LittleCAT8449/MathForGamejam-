using TMPro;
using UnityEngine;

/// <summary>
/// Displays the operation currently selected on a world-space Sprite2D.
/// Attach this component to a SpriteRenderer object. You can use a child TMP
/// text, operation sprites, or both.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class StampOperationDisplay : MonoBehaviour
{
    [Header("数据来源")]
    [SerializeField] private StampingMachine stampingMachine;

    [Header("文本显示")]
    [Tooltip("可选。留空时自动查找子物体中的 TMP 文本。")]
    [SerializeField] private TMP_Text operationLabel;

    [Header("Sprite 显示")]
    [Tooltip("可选。绑定后，宿主 SpriteRenderer 会根据运算模式切换贴图。")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite addSprite;
    [SerializeField] private Sprite subtractSprite;
    [SerializeField] private Sprite multiplySprite;
    [SerializeField] private Sprite divideSprite;

    [Header("运算符文本")]
    [SerializeField] private string addSymbol = "+";
    [SerializeField] private string subtractSymbol = "−";
    [SerializeField] private string multiplySymbol = "×";
    [SerializeField] private string divideSymbol = "÷";

    private StampOperation lastOperation;
    private bool lastNegativeSubtractMode;
    private bool hasCachedOperation;

    private void Awake()
    {
        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (operationLabel == null)
        {
            operationLabel = GetComponentInChildren<TMP_Text>(true);
        }

        RefreshDisplay();
    }

    private void OnEnable()
    {
        RefreshDisplay();
    }

    private void Update()
    {
        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
            if (stampingMachine == null)
            {
                return;
            }
        }

        if (!hasCachedOperation ||
            lastOperation != stampingMachine.Operation ||
            lastNegativeSubtractMode != stampingMachine.IsNegativeSubtractMode)
        {
            RefreshDisplay();
        }
    }

    /// <summary>
    /// Refreshes the symbol immediately. Useful after changing the operation
    /// from another script in the same frame.
    /// </summary>
    public void RefreshDisplay()
    {
        if (stampingMachine == null)
        {
            if (operationLabel != null)
            {
                operationLabel.text = string.Empty;
            }

            return;
        }

        StampOperation operation = stampingMachine.Operation;
        string symbol = GetSymbol(operation);
        Sprite operationSprite = GetSprite(operation);

        if (operationLabel != null)
        {
            operationLabel.text = symbol;
        }

        if (targetRenderer != null && operationSprite != null)
        {
            targetRenderer.sprite = operationSprite;
        }

        lastOperation = operation;
        lastNegativeSubtractMode = stampingMachine.IsNegativeSubtractMode;
        hasCachedOperation = true;
    }

    private string GetSymbol(StampOperation operation)
    {
        switch (operation)
        {
            case StampOperation.Add:
                return addSymbol;
            case StampOperation.Subtract:
                return subtractSymbol;
            case StampOperation.Multiply:
                return multiplySymbol;
            case StampOperation.Divide:
                return divideSymbol;
            default:
                return operation.ToString();
        }
    }

    private Sprite GetSprite(StampOperation operation)
    {
        switch (operation)
        {
            case StampOperation.Add:
                return addSprite;
            case StampOperation.Subtract:
                return subtractSprite;
            case StampOperation.Multiply:
                return multiplySprite;
            case StampOperation.Divide:
                return divideSprite;
            default:
                return null;
        }
    }
}
