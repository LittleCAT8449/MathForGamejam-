using System.Collections.Generic;
using UnityEngine;

public enum StampOperation
{
    Add,
    Subtract,
    Multiply,
    Divide
}

/// <summary>
/// Attach to the moving press. Call Move() to descend until it contacts the anvil.
/// Number tokens touched along the way are consumed in contact order.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class StampingMachine : MonoBehaviour
{
    [SerializeField] private Rigidbody2D pressBody;
    [SerializeField] private Collider2D anvilCollider;
    [SerializeField] private NumberToken resultPrefab;
    [SerializeField] private Transform resultSpawnPoint;
    [SerializeField] private SettlementArea settlementArea;
    [SerializeField] private NumberBreakEffect numberBreakEffect;
    [SerializeField] private StampOperation operation = StampOperation.Add;
    [SerializeField, Min(0.01f)] private float moveSpeed = 5f;
    [SerializeField, Min(0.01f)] private float returnSpeed = 5f;

    private readonly HashSet<NumberToken> consumedTokens = new HashSet<NumberToken>();
    private Vector2 initialPosition;
    private decimal result;
    private bool hasOperand;
    private int operandCount;
    private bool calculationValid;
    private bool negativeSubtractMode;
    private bool subtractModeInitialized;
    private bool isMoving;
    private bool isReturning;
    private Collider2D[] pressColliders;
    private readonly List<Collider2D> ignoredTokenColliders = new List<Collider2D>();

    /// <summary>
    /// True after Move has successfully started at least one press cycle in
    /// the current round. GameResetClick uses this to decide whether returning
    /// to the mining scene needs confirmation.
    /// </summary>
    public bool HasStartedStamping { get; private set; }

    /// <summary>
    /// Fallback round state used by the return button when a press object is
    /// temporarily inactive or not discoverable by FindObjectsByType.
    /// </summary>
    public static bool HasStartedAnyStampingThisRound { get; private set; }

    /// <summary>
    /// The operation that will be used by the next press cycle.
    /// </summary>
    public StampOperation Operation => operation;

    /// <summary>
    /// True when subtraction uses smaller minus larger, producing a negative
    /// result for the first two operands.
    /// </summary>
    public bool IsNegativeSubtractMode => negativeSubtractMode;

    /// <summary>
    /// Indicates whether the current round has already selected subtraction
    /// once. This lets the first click start in positive mode.
    /// </summary>
    public bool HasSelectedSubtractMode => subtractModeInitialized;

    /// <summary>
    /// True when a new press cycle can be started. This is used by the lever
    /// click component so it does not play a sound for a blocked click.
    /// </summary>
    public bool CanStartMove => !isMoving && !isReturning && anvilCollider != null;

    /// <summary>
    /// Changes the operation used when the press reads its number tokens.
    /// </summary>
    public void SetOperation(StampOperation selectedOperation)
    {
        operation = selectedOperation;
        if (selectedOperation != StampOperation.Subtract)
        {
            subtractModeInitialized = false;
            negativeSubtractMode = false;
        }

        Debug.Log($"冲压运算已设置为：{operation}。", this);
    }

    /// <summary>
    /// Sets the subtraction mode. Positive mode is larger minus smaller;
    /// negative mode is smaller minus larger.
    /// </summary>
    public void SetSubtractNegativeMode(bool enabled)
    {
        negativeSubtractMode = enabled;
        subtractModeInitialized = true;
        Debug.Log(
            $"减法模式已设置为：{(enabled ? "负数模式（小数-大数）" : "正数模式（大数-小数）")}。",
            this);
    }

    /// <summary>
    /// Switches between positive and negative subtraction mode.
    /// </summary>
    public bool ToggleSubtractMode()
    {
        SetSubtractNegativeMode(!negativeSubtractMode);
        return negativeSubtractMode;
    }

    private void Awake()
    {
        if (pressBody == null)
        {
            pressBody = GetComponent<Rigidbody2D>();
        }

        if (settlementArea == null)
        {
            settlementArea = FindFirstObjectByType<SettlementArea>();
        }

        if (numberBreakEffect == null)
        {
            numberBreakEffect = GetComponent<NumberBreakEffect>();
        }

        pressColliders = GetComponentsInChildren<Collider2D>(true);

        initialPosition = pressBody.position;
        // Keep the press still until Move() is called; a default Dynamic body
        // would otherwise fall as soon as the scene starts.
        pressBody.bodyType = RigidbodyType2D.Kinematic;
        pressBody.gravityScale = 0f;
        pressBody.linearVelocity = Vector2.zero;
    }

    private void Start()
    {
        if (GetComponent<Collider2D>() == null && GetComponentInChildren<Collider2D>() == null)
        {
            Debug.LogWarning($"{name}：冲压机需要 Collider2D 才能碰撞数字和砧板。", this);
        }

        if (anvilCollider == null)
        {
            Debug.LogWarning($"{name}：请在 StampingMachine 中指定砧板的 Collider2D。", this);
        }

        if (resultPrefab == null)
        {
            Debug.LogWarning($"{name}：请在 StampingMachine 中指定结果数字 Prefab。", this);
        }
    }

    private void FixedUpdate()
    {
        if (isMoving)
        {
            ConsumeOverlappingTokens();
        }

        if (!isReturning)
        {
            return;
        }

        if (pressBody.position.y >= initialPosition.y)
        {
            pressBody.position = initialPosition;
            pressBody.linearVelocity = Vector2.zero;
            pressBody.bodyType = RigidbodyType2D.Kinematic;
            isReturning = false;
            RestoreTokenCollisions();
            Debug.Log("冲压机已回到初始位置。", this);
            return;
        }

        pressBody.linearVelocity = Vector2.up * returnSpeed;
    }

    /// <summary>
    /// Starts one press cycle. The machine moves vertically down in world space.
    /// </summary>
    public void Move()
    {
        if (isMoving || isReturning)
        {
            return;
        }

        if (anvilCollider == null)
        {
            Debug.LogWarning("无法冲压：没有指定砧板 Collider2D。", this);
            return;
        }

        consumedTokens.Clear();
        result = 0m;
        hasOperand = false;
        operandCount = 0;
        calculationValid = true;
        isMoving = true;
        HasStartedStamping = true;
        HasStartedAnyStampingThisRound = true;

        // Input numbers can be resting on or intersecting the press body.
        // Ignore only their physical response while the press is moving;
        // ConsumeOverlappingTokens still reads and removes them explicitly.
        IgnoreNumberCollisions();

        pressBody.bodyType = RigidbodyType2D.Dynamic;
        pressBody.gravityScale = 0f;
        pressBody.constraints &= ~RigidbodyConstraints2D.FreezePositionY;
        pressBody.constraints |= RigidbodyConstraints2D.FreezePositionX |
                                 RigidbodyConstraints2D.FreezeRotation;
        pressBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        pressBody.linearVelocity = Vector2.down * moveSpeed;

        GameAudioManager.Instance?.PlayStampingWork();
        Debug.Log($"冲压机开始下压，运算方式：{operation}。", this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider2D other)
    {
        if (!isMoving || isReturning || other == null)
        {
            return;
        }

        if (IsAnvil(other))
        {
            StopAtAnvil();
            return;
        }

        NumberToken token = other.GetComponentInParent<NumberToken>();
        if (token == null || !consumedTokens.Add(token))
        {
            return;
        }

        ConsumeToken(token);
    }

    private void ApplyOperand(decimal value, NumberToken token)
    {
        if (!hasOperand)
        {
            result = value;
            hasOperand = true;
            operandCount = 1;
            Debug.Log($"冲压读取首个数字：{value}。", token);
            return;
        }

        if (!calculationValid)
        {
            return;
        }

        try
        {
            switch (operation)
            {
                case StampOperation.Add:
                    result += value;
                    break;
                case StampOperation.Subtract:
                    if (operandCount == 1)
                    {
                        decimal larger = result >= value ? result : value;
                        decimal smaller = result >= value ? value : result;
                        result = negativeSubtractMode
                            ? smaller - larger
                            : larger - smaller;
                    }
                    else
                    {
                        result -= value;
                    }
                    break;
                case StampOperation.Multiply:
                    result *= value;
                    break;
                case StampOperation.Divide:
                    if (value == 0m)
                    {
                        calculationValid = false;
                        Debug.LogError("冲压计算失败：不能除以 0。", token);
                        return;
                    }

                    result /= value;
                    break;
            }

            Debug.Log($"冲压读取数字 {value}，当前结果：{result}。", token);
            operandCount++;
        }
        catch (System.OverflowException)
        {
            calculationValid = false;
            Debug.LogError("冲压计算失败：结果超出 decimal 可表示范围。", token);
        }
    }

    private bool IsAnvil(Collider2D other)
    {
        return other == anvilCollider ||
               other.transform == anvilCollider.transform ||
               other.transform.IsChildOf(anvilCollider.transform);
    }

    private void StopAtAnvil()
    {
        isMoving = false;
        pressBody.linearVelocity = Vector2.zero;

        if (!hasOperand)
        {
            Debug.LogWarning("冲压机碰到砧板，但途中没有碰到数字，没有生成结果。", this);
        }
        else if (!calculationValid)
        {
            Debug.LogWarning("冲压计算无效，没有生成结果数字。", this);
        }
        else if (resultPrefab == null)
        {
            Debug.LogError("冲压计算完成，但没有指定结果数字 Prefab。", this);
        }
        else
        {
            Vector3 spawnPosition = resultSpawnPoint != null
                ? resultSpawnPoint.position
                : transform.position;
            NumberToken resultToken = Instantiate(resultPrefab, spawnPosition, resultPrefab.transform.rotation);
            resultToken.SetValue(result);

            if (settlementArea != null)
            {
                settlementArea.PrepareForDelivery(resultToken);
            }

            // The result is intentionally left in the stamping area for the
            // player to drag away. It must not physically block the press while
            // the press returns to its initial position.
            IgnoreTokenCollision(resultToken);

            Debug.Log($"冲压完成，结果：{result}。", resultToken);
        }

        isReturning = true;
        pressBody.linearVelocity = Vector2.up * returnSpeed;
    }

    private void ConsumeToken(NumberToken token)
    {
        if (token == null)
        {
            return;
        }

        ApplyOperand(token.Value, token);
        NumberBreakEffect breakEffect = numberBreakEffect != null
            ? numberBreakEffect
            : token.GetComponent<NumberBreakEffect>();
        breakEffect?.Play(token);
        Destroy(token.gameObject);

        // Contact with a token can change a dynamic body's velocity. Keep the
        // press descending until the anvil is reached.
        if (isMoving)
        {
            pressBody.linearVelocity = Vector2.down * moveSpeed;
        }
    }

    private void ConsumeOverlappingTokens()
    {
        if (pressColliders == null || pressColliders.Length == 0)
        {
            return;
        }

        NumberToken[] tokens = FindObjectsByType<NumberToken>(FindObjectsSortMode.None);
        foreach (NumberToken token in tokens)
        {
            if (token == null || consumedTokens.Contains(token))
            {
                continue;
            }

            Collider2D[] tokenColliders = token.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D tokenCollider in tokenColliders)
            {
                if (tokenCollider == null)
                {
                    continue;
                }

                foreach (Collider2D pressCollider in pressColliders)
                {
                    if (pressCollider != null && pressCollider.bounds.Intersects(tokenCollider.bounds))
                    {
                        if (consumedTokens.Add(token))
                        {
                            ConsumeToken(token);
                        }

                        break;
                    }
                }

                if (consumedTokens.Contains(token))
                {
                    break;
                }
            }
        }
    }

    private void IgnoreNumberCollisions()
    {
        RestoreTokenCollisions();

        if (pressColliders == null || pressColliders.Length == 0)
        {
            pressColliders = GetComponentsInChildren<Collider2D>(true);
        }

        NumberToken[] tokens = FindObjectsByType<NumberToken>(FindObjectsSortMode.None);
        foreach (NumberToken token in tokens)
        {
            if (token == null)
            {
                continue;
            }

            IgnoreTokenCollision(token);
        }
    }

    private void IgnoreTokenCollision(NumberToken token)
    {
        if (token == null)
        {
            return;
        }

        if (pressColliders == null || pressColliders.Length == 0)
        {
            pressColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Collider2D[] tokenColliders = token.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D tokenCollider in tokenColliders)
        {
            if (tokenCollider == null)
            {
                continue;
            }

            foreach (Collider2D pressCollider in pressColliders)
            {
                if (pressCollider != null)
                {
                    Physics2D.IgnoreCollision(pressCollider, tokenCollider, true);
                }
            }

            if (!ignoredTokenColliders.Contains(tokenCollider))
            {
                ignoredTokenColliders.Add(tokenCollider);
            }
        }
    }

    private void RestoreTokenCollisions()
    {
        if (pressColliders == null)
        {
            pressColliders = GetComponentsInChildren<Collider2D>(true);
        }

        foreach (Collider2D tokenCollider in ignoredTokenColliders)
        {
            if (tokenCollider == null)
            {
                continue;
            }

            foreach (Collider2D pressCollider in pressColliders)
            {
                if (pressCollider != null)
                {
                    Physics2D.IgnoreCollision(pressCollider, tokenCollider, false);
                }
            }
        }

        ignoredTokenColliders.Clear();
    }

    /// <summary>
    /// Immediately resets the press to its initial position, for round resets.
    /// </summary>
    public void ResetPress()
    {
        isMoving = false;
        isReturning = false;
        HasStartedStamping = false;
        HasStartedAnyStampingThisRound = false;
        hasOperand = false;
        operandCount = 0;
        calculationValid = false;
        negativeSubtractMode = false;
        subtractModeInitialized = false;
        consumedTokens.Clear();
        pressBody.linearVelocity = Vector2.zero;
        pressBody.position = initialPosition;
        pressBody.gravityScale = 0f;
        pressBody.bodyType = RigidbodyType2D.Kinematic;
        RestoreTokenCollisions();
    }

    private void OnDisable()
    {
        RestoreTokenCollisions();
    }
}
