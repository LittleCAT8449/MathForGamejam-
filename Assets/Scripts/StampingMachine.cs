using System.Collections;
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
/// How the press travels from its initial position down to the anvil.
/// </summary>
public enum StampMoveMode
{
    /// <summary>Original behaviour: constant moveSpeed, stops on the anvil collision.</summary>
    ConstantSpeed,

    /// <summary>a * x^b easing over a fixed duration, stops when the curve ends.</summary>
    Easing
}

/// <summary>
/// How consumed number tokens travel toward their convergence point.
/// </summary>
public enum StampConvergeMode
{
    /// <summary>Original behaviour: piecewise quadratic ease-in/ease-out.</summary>
    PiecewiseQuadratic,

    /// <summary>a * x^b easing over tokenConvergenceDuration.</summary>
    PowerEasing
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
    [SerializeField, Min(0.01f)] private float tokenConvergenceDuration = 0.35f;

    [Header("冲压音效")]
    [Tooltip("按下冲压后延迟多少秒才播放冲压工作音效。填 0 表示立即播放，与原有行为完全一致。")]
    [SerializeField, Min(0f)] private float stampingAudioDelay = 0f;

    [Header("冲压下行模式")]
    [SerializeField] private StampMoveMode descendMode = StampMoveMode.ConstantSpeed;
    [Header("缓动下行参数（仅缓动模式生效）")]
    [Tooltip("缓动模式下冲压锤从起点走完终点所用的时间，单位为秒。")]
    [SerializeField, Min(0.01f)] private float descendDuration = 0.5f;
    [Tooltip("缓动系数 a，进度 = a * x^b。通常填 1，此时进度终点正好为 1。")]
    [SerializeField] private float descendEasingA = 1f;
    [Tooltip("缓动系数 b，进度 = a * x^b。b=1 线性，b>1 先慢后快，b<1 先快后慢。")]
    [SerializeField] private float descendEasingB = 1f;

    [Header("数字合并模式")]
    [SerializeField] private StampConvergeMode convergeMode = StampConvergeMode.PiecewiseQuadratic;
    [Header("合并缓动参数（仅幂缓动模式生效）")]
    [Tooltip("缓动系数 a，进度 = a * x^b。通常填 1，此时进度终点正好为 1。")]
    [SerializeField] private float convergeEasingA = 1f;
    [Tooltip("缓动系数 b，进度 = a * x^b。b=1 线性，b>1 先慢后快，b<1 先快后慢。")]
    [SerializeField] private float convergeEasingB = 1f;

    private readonly HashSet<NumberToken> consumedTokens = new HashSet<NumberToken>();
    private readonly List<NumberToken> consumedTokenList = new List<NumberToken>();
    private Vector2 initialPosition;
    private decimal result;
    private bool hasOperand;
    private int operandCount;
    private bool calculationValid;
    private bool negativeSubtractMode;
    private bool subtractModeInitialized;
    private bool isMoving;
    private bool isReturning;
    private bool isConvergingTokens;
    private bool useEasingDescend;
    private float descendStartY;
    private float descendTargetY;
    private float descendElapsed;
    private Coroutine stampingWorkAudioCoroutine;
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
    public bool CanStartMove => !isMoving && !isReturning && !isConvergingTokens && anvilCollider != null;

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
            if (useEasingDescend)
            {
                UpdateEasingDescend();
            }

            // UpdateEasingDescend can finish the cycle on its own, so only look
            // for tokens while the press is still descending.
            if (isMoving)
            {
                ConsumeOverlappingTokens();
            }
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
        if (isMoving || isReturning || isConvergingTokens)
        {
            return;
        }

        if (anvilCollider == null)
        {
            Debug.LogWarning("无法冲压：没有指定砧板 Collider2D。", this);
            return;
        }

        consumedTokens.Clear();
        consumedTokenList.Clear();
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

        PrepareDescendMotion();

        // In easing mode the curve produces the velocity on the first physics
        // step, so the press starts from rest instead of a constant speed.
        pressBody.linearVelocity = useEasingDescend
            ? Vector2.zero
            : Vector2.down * moveSpeed;

        PlayStampingWorkWithDelay();
        Debug.Log($"冲压机开始下压，运算方式：{operation}。", this);
    }

    /// <summary>
    /// Resolves the easing target for this press cycle. Falls back to constant
    /// speed when the anvil or the press colliders cannot provide a target.
    /// </summary>
    private void PrepareDescendMotion()
    {
        descendElapsed = 0f;
        descendStartY = pressBody.position.y;
        useEasingDescend = false;

        if (descendMode != StampMoveMode.Easing)
        {
            return;
        }

        // A target that is not below the start would make the curve drive the
        // press upwards. That happens when the press already rests on the anvil
        // or the scene is misconfigured, and constant speed handles it correctly
        // by colliding immediately, so treat it the same as a missing target.
        if (!TryGetDescendTargetY(out float targetY) || targetY >= descendStartY)
        {
            Debug.LogWarning(
                "缓动下行找不到有效终点（缺少砧板或冲压锤 Collider，或冲压锤已经贴在砧板上），本次回退为恒定速度下行。",
                this);
            return;
        }

        descendTargetY = targetY;
        useEasingDescend = true;
    }

    /// <summary>
    /// Computes the Y position at which the bottom of the press just touches
    /// the top of the anvil collider.
    /// </summary>
    private bool TryGetDescendTargetY(out float targetY)
    {
        targetY = 0f;

        if (anvilCollider == null)
        {
            return false;
        }

        if (pressColliders == null || pressColliders.Length == 0)
        {
            pressColliders = GetComponentsInChildren<Collider2D>(true);
        }

        float pressBottom = float.PositiveInfinity;
        bool hasSolidBottom = false;
        float anyBottom = float.PositiveInfinity;
        bool hasAnyBottom = false;

        foreach (Collider2D pressCollider in pressColliders)
        {
            if (pressCollider == null)
            {
                continue;
            }

            anyBottom = Mathf.Min(anyBottom, pressCollider.bounds.min.y);
            hasAnyBottom = true;

            // Trigger colliders never stop the press, so they must not decide
            // where the descent ends.
            if (pressCollider.isTrigger)
            {
                continue;
            }

            pressBottom = Mathf.Min(pressBottom, pressCollider.bounds.min.y);
            hasSolidBottom = true;
        }

        if (!hasSolidBottom)
        {
            if (!hasAnyBottom)
            {
                return false;
            }

            pressBottom = anyBottom;
        }

        float bottomOffset = pressBody.position.y - pressBottom;
        targetY = anvilCollider.bounds.max.y + bottomOffset;
        return true;
    }

    /// <summary>
    /// Advances the eased descent by one physics step. The curve drives the
    /// rigidbody through linearVelocity so the anvil collision callback,
    /// IsAnvil and StopAtAnvil keep working exactly as in constant speed mode.
    /// </summary>
    private void UpdateEasingDescend()
    {
        float duration = Mathf.Max(0.01f, descendDuration);
        descendElapsed += Time.fixedDeltaTime;

        float normalizedTime = Mathf.Clamp01(descendElapsed / duration);
        float progress = EvaluatePowerEasing(descendEasingA, descendEasingB, normalizedTime);
        float targetY = Mathf.Lerp(descendStartY, descendTargetY, progress);

        if (normalizedTime >= 1f)
        {
            // The curve has run out. Land exactly on the target and finish the
            // cycle through the same path the anvil collision would take.
            pressBody.position = new Vector2(pressBody.position.x, targetY);
            StopAtAnvil();
            return;
        }

        pressBody.linearVelocity =
            new Vector2(0f, (targetY - pressBody.position.y) / Time.fixedDeltaTime);
    }

    /// <summary>
    /// Starts the stamping sound effect, optionally after the delay configured
    /// in the Inspector. A delay of 0 plays it immediately, which is exactly
    /// the original behaviour.
    /// </summary>
    private void PlayStampingWorkWithDelay()
    {
        if (stampingAudioDelay <= 0f)
        {
            GameAudioManager.Instance?.PlayStampingWork();
            return;
        }

        if (stampingWorkAudioCoroutine != null)
        {
            StopCoroutine(stampingWorkAudioCoroutine);
        }

        stampingWorkAudioCoroutine = StartCoroutine(PlayStampingWorkAfterDelay());
    }

    private IEnumerator PlayStampingWorkAfterDelay()
    {
        yield return new WaitForSeconds(stampingAudioDelay);

        // A press cycle can be over before the delay elapses: an early anvil
        // contact, a descent shorter than the delay, or a round reset. Starting
        // the machine sound after the machine has already stopped would sound
        // detached, so it is skipped in that case.
        if (isMoving || isReturning || isConvergingTokens)
        {
            GameAudioManager.Instance?.PlayStampingWork();
        }

        stampingWorkAudioCoroutine = null;
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
        if (token == null || token.IsConsumed || !consumedTokens.Add(token))
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
            BeginReturning();
            return;
        }

        if (!calculationValid)
        {
            Debug.LogWarning("冲压计算无效，没有生成结果数字。", this);
        }

        if (resultPrefab == null)
        {
            Debug.LogError("冲压计算完成，但没有指定结果数字 Prefab。", this);
        }

        if (consumedTokenList.Count == 0)
        {
            Debug.LogWarning("冲压没有可收拢的数字对象。", this);
            BeginReturning();
            return;
        }

        isConvergingTokens = true;
        // Start the press return immediately. The number convergence runs in
        // parallel so it does not add extra waiting time to the lift motion.
        BeginReturning();
        StartCoroutine(ConvergeTokensThenFinish());
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
        if (breakEffect != null)
        {
            // This fades the original SpriteRenderer and creates the visual
            // fragments. The token itself remains alive until the group
            // convergence finishes below, so its TMP value is still visible.
            breakEffect.Play(token, tokenConvergenceDuration);
        }
        else
        {
            token.MarkConsumedAndHide();
        }

        consumedTokenList.Add(token);

        // Contact with a token can change a dynamic body's velocity. Keep the
        // press descending until the anvil is reached. In easing mode the
        // velocity belongs to the curve and must not be overwritten here, but
        // the semantic stays the same: touching a number never stops the press.
        if (isMoving && !useEasingDescend)
        {
            pressBody.linearVelocity = Vector2.down * moveSpeed;
        }
    }

    private IEnumerator ConvergeTokensThenFinish()
    {
        List<NumberToken> tokens = new List<NumberToken>();
        Vector3 convergencePoint = Vector3.zero;

        foreach (NumberToken token in consumedTokenList)
        {
            if (token == null)
            {
                continue;
            }

            tokens.Add(token);
            convergencePoint += token.transform.position;
        }

        if (tokens.Count > 0)
        {
            convergencePoint /= tokens.Count;
        }

        Vector3[] startPositions = new Vector3[tokens.Count];
        for (int i = 0; i < tokens.Count; i++)
        {
            startPositions[i] = tokens[i].transform.position;
        }

        float duration = Mathf.Max(0.01f, tokenConvergenceDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = EvaluateConvergeProgress(elapsed / duration);

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] != null)
                {
                    tokens[i].SetConvergencePosition(
                        Vector3.Lerp(startPositions[i], convergencePoint, progress));
                }
            }

            yield return null;
        }

        foreach (NumberToken token in tokens)
        {
            if (token != null)
            {
                token.SetConvergencePosition(convergencePoint);
                Destroy(token.gameObject);
            }
        }

        consumedTokenList.Clear();

        if (calculationValid && resultPrefab != null)
        {
            CreateResultToken();
        }

        isConvergingTokens = false;
    }

    /// <summary>
    /// Piecewise quadratic ease-in/ease-out. The first half accelerates toward
    /// the convergence point, and the second half decelerates into it.
    /// </summary>
    private static float EvaluatePiecewiseQuadratic(float normalizedTime)
    {
        float time = Mathf.Clamp01(normalizedTime);
        if (time < 0.5f)
        {
            float firstHalf = time * 2f;
            return 0.5f * firstHalf * firstHalf;
        }

        float secondHalf = (time - 0.5f) * 2f;
        float remaining = 1f - secondHalf;
        return 0.5f + 0.5f * (1f - remaining * remaining);
    }

    /// <summary>
    /// Shared power easing: a * x^b, where x is normalized time in 0..1 and the
    /// result is used directly as 0..1 progress. The press descent and the
    /// token convergence both go through here, each with its own a and b.
    /// Mathf.Lerp clamps the progress, so a value outside 0..1 cannot overshoot
    /// past the target.
    /// </summary>
    private static float EvaluatePowerEasing(float a, float b, float normalizedTime)
    {
        float time = Mathf.Clamp01(normalizedTime);
        float value = a * Mathf.Pow(time, b);

        // A negative b at an exact time of 0 produces NaN or Infinity, which
        // would otherwise be written straight into the rigidbody velocity.
        return float.IsNaN(value) || float.IsInfinity(value) ? time : value;
    }

    /// <summary>
    /// Picks the convergence curve for the token merge. The original piecewise
    /// quadratic stays available and remains the default.
    /// </summary>
    private float EvaluateConvergeProgress(float normalizedTime)
    {
        return convergeMode == StampConvergeMode.PowerEasing
            ? EvaluatePowerEasing(convergeEasingA, convergeEasingB, normalizedTime)
            : EvaluatePiecewiseQuadratic(normalizedTime);
    }

    private void CreateResultToken()
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

        TutorialTooltipController.FindOrCreate().OnFirstNumberCombined();

        // The result is intentionally left in the stamping area for the
        // player to drag away. It must not physically block the press while
        // the press returns to its initial position.
        IgnoreTokenCollision(resultToken);

        Debug.Log($"冲压完成，结果：{result}。", resultToken);
    }

    private void BeginReturning()
    {
        isReturning = true;
        pressBody.linearVelocity = Vector2.up * returnSpeed;
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
            if (token == null || token.IsConsumed || consumedTokens.Contains(token))
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
        StopAllCoroutines();
        isMoving = false;
        isReturning = false;
        isConvergingTokens = false;
        HasStartedStamping = false;
        HasStartedAnyStampingThisRound = false;
        hasOperand = false;
        operandCount = 0;
        calculationValid = false;
        negativeSubtractMode = false;
        subtractModeInitialized = false;
        consumedTokens.Clear();
        consumedTokenList.Clear();
        useEasingDescend = false;
        descendElapsed = 0f;
        descendStartY = initialPosition.y;
        descendTargetY = initialPosition.y;
        // StopAllCoroutines above already kills any pending sound; clear the
        // handle so the next cycle does not hold a dead reference.
        stampingWorkAudioCoroutine = null;
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
