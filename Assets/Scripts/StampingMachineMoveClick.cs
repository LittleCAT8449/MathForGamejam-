using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Attach to a clickable Sprite/2D object to call StampingMachine.Move().
/// 同时驱动拉杆绕指定的轴心物体（Rotation Pivot）做缓动旋转。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StampingMachineMoveClick : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private StampingMachine stampingMachine;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;

    [Header("拉杆轴心")]
    [Tooltip("拉杆绕着它旋转的物体。留空时退化为绕自身旋转（旧行为）。\n轴心不能是拉杆自己的子物体，否则会自旋失控。\n轴心请使用均匀缩放（Scale 三轴一致）。")]
    [SerializeField] private Transform rotationPivot;

    [Tooltip("旋转轴，相对于轴心的本地坐标系。2D 场景保持默认的 Z 轴即可。")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [Header("缓动： 角度(x) = w × (x/t)^b")]
    [Tooltip("w —— 总旋转角度，单位为度。拉到底时转过的角度就是这个值，与 b、t 无关。")]
    [SerializeField] private float rotationAngle = 90f;

    [Tooltip("b —— 指数。b = 1 匀速；b > 1 先慢后快（ease in）；0 < b < 1 先快后慢（ease out）。\n必须大于 0，否则 0^b 会算出无穷大。")]
    [SerializeField] private float easingB = 1f;

    [Tooltip("t —— 单次缓动的总时长（秒），必须大于 0。")]
    [SerializeField] private float easingDuration = 0.3f;

    [Tooltip("勾选后回弹使用镜像曲线（先快后慢），视觉上更像弹簧归位。")]
    [SerializeField] private bool mirrorEasingOnReturn = true;

    [Header("底部停顿")]
    [Tooltip("拉杆完全到底之后，在底部停留多少秒再回弹。\n0 = 不停顿，缓动结束立刻回弹（默认）。\n这是绝对秒数，不随缓动时长 t 缩放：t 改小了这个停顿不会跟着变短。")]
    [FormerlySerializedAs("holdCoefficient")]
    [SerializeField] private float holdDuration = 0f;

    /// <summary>轴心本地空间下的静止旋转。</summary>
    private Quaternion restRelativeRotation;

    /// <summary>轴心本地空间下的静止位置。</summary>
    private Vector3 restRelativePosition;

    /// <summary>没有轴心时使用的自身静止旋转。</summary>
    private Quaternion restLocalRotation;

    private bool restPoseCached;
    private bool pivotWarningLogged;

    /// <summary>当前归一化进度：0 = 静止姿态，1 = 完全转过 w 度。</summary>
    private float currentProgress;

    private Coroutine leverTweenCoroutine;

    private void Awake()
    {
        // Keep the button clickable even when its GameObject is moved to a
        // custom layer and the serialized mask was not updated.
        clickableLayers |= 1 << gameObject.layer;

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }

        CacheRestPose();
    }

    private void Start()
    {
        if (inputCamera == null)
        {
            Debug.LogWarning($"{name}：没有找到输入摄像机，请设置 Input Camera 或将摄像机 Tag 设为 MainCamera。", this);
        }

        if (stampingMachine == null)
        {
            Debug.LogWarning($"{name}：没有找到 StampingMachine，请在 Inspector 中指定。", this);
        }

        if (rotationPivot == null)
        {
            Debug.LogWarning($"{name}：没有指定拉杆轴心 Rotation Pivot，将退化为绕自身旋转。", this);
        }
        else if (!IsPivotUsable())
        {
            Debug.LogWarning($"{name}：轴心 {rotationPivot.name} 是拉杆自己的子物体，会造成自旋失控，已忽略。", this);
        }
    }

    private void OnValidate()
    {
        // 防止 Pow(0, b) 或除以 0 产生无穷大 / NaN。
        easingB = Mathf.Max(0.0001f, easingB);
        easingDuration = Mathf.Max(0.0001f, easingDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        rotationAxis = GetRotationAxis();
    }

    private void Update()
    {
        if (GameResetClick.IsModalOpen || inputCamera == null || Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (WasClicked(Mouse.current.position.ReadValue()))
        {
            CallMove();
        }
    }

    /// <summary>
    /// Can also be connected to a Unity UI Button's OnClick event.
    /// </summary>
    public void CallMove()
    {
        if (GameResetClick.IsModalOpen)
        {
            return;
        }

        if (stampingMachine == null)
        {
            Debug.LogWarning("无法启动冲压：没有指定 StampingMachine。", this);
            return;
        }

        bool canStartMove = stampingMachine.CanStartMove;
        if (canStartMove)
        {
            GameAudioManager.Instance?.PlayLeverPull();
            StartLeverTween(1f, returning: false);
        }

        // 回弹由扳下缓动自己在结束那一刻触发，不再等待整轮冲压结束。
        stampingMachine.Move();
    }

    /// <summary>
    /// Rotates the lever using the configured easing. This can be
    /// connected to a separate button or called from another script.
    /// </summary>
    public void CallRotate()
    {
        if (GameResetClick.IsModalOpen || stampingMachine == null)
        {
            return;
        }

        if (!stampingMachine.CanStartMove)
        {
            return;
        }

        StartLeverTween(1f, returning: false);
    }

    /// <summary>
    /// 在运行时更换轴心或摆好新的静止姿态后调用，重新记录静止姿态并立刻归位。
    /// 请在拉杆处于静止状态时调用，否则会把当前姿态当成静止姿态记下来。
    /// </summary>
    [ContextMenu("重新记录拉杆静止姿态")]
    public void RecaptureRestPose()
    {
        StopLeverTween();
        CacheRestPose();
        ApplyLeverProgress(0f);
    }

    // ---------------------------------------------------------------- 缓动

    /// <summary>
    /// 记录"静止姿态"。有轴心时记录的是相对轴心的位姿，
    /// 这样轴心（乃至整台机器）之后如何移动，拉杆都会跟着走。
    /// </summary>
    private void CacheRestPose()
    {
        restLocalRotation = transform.localRotation;

        if (IsPivotUsable())
        {
            restRelativeRotation = Quaternion.Inverse(rotationPivot.rotation) * transform.rotation;
            restRelativePosition = rotationPivot.InverseTransformPoint(transform.position);
        }

        restPoseCached = true;
        currentProgress = 0f;
    }

    /// <summary>
    /// 启动一次缓动。targetProgress 传 1 表示扳下，传 0 表示归位。
    /// returning 必须由调用方显式给出，不能用 targetProgress 的大小推断。
    /// </summary>
    private void StartLeverTween(float targetProgress, bool returning)
    {
        if (!restPoseCached)
        {
            CacheRestPose();
        }

        StopLeverTween();
        leverTweenCoroutine = StartCoroutine(LeverTweenRoutine(targetProgress, returning));
    }

    private void StopLeverTween()
    {
        if (leverTweenCoroutine != null)
        {
            StopCoroutine(leverTweenCoroutine);
            leverTweenCoroutine = null;
        }
    }

    private IEnumerator LeverTweenRoutine(float targetProgress, bool returning)
    {
        float startProgress = currentProgress;

        // 进度只可能是 0 或 1，所以行程就是两者的差值。
        // 用它缩放时长，被打断时也能接着走完剩下的部分，不会跳变。
        float travel = Mathf.Clamp01(Mathf.Abs(targetProgress - startProgress));
        float duration = easingDuration * travel;

        if (duration > 0f)
        {
            // 立刻对齐起点，避免上一次被打断时留下偏差。
            ApplyLeverProgress(startProgress);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float shape = EvaluateShape(normalizedTime, returning);
                ApplyLeverProgress(Mathf.LerpUnclamped(startProgress, targetProgress, shape));
                yield return null;
            }
        }

        ApplyLeverProgress(targetProgress);

        if (!returning)
        {
            // 拉杆已经完全到底（上面刚 ApplyLeverProgress(targetProgress)），
            // 在这里停住不动，等停顿结束再走回弹。
            // 停顿期间 leverTweenCoroutine 仍指向本协程，
            // 这样 OnDisable 里的 StopLeverTween 还能正常把它停掉、拉杆不会卡在底部。
            // 这里再 clamp 一次：OnValidate 只在编辑器里跑，打包后不会执行。
            float holdSeconds = Mathf.Max(0f, holdDuration);
            if (holdSeconds > 0f)
            {
                yield return new WaitForSeconds(holdSeconds);
            }

            // 必须先置空再启动回弹协程，否则回弹协程的句柄会被本协程的收尾覆盖掉，
            // 之后 StopLeverTween 就停不掉它了。
            leverTweenCoroutine = null;
            StartLeverTween(0f, returning: true);
            yield break;
        }

        leverTweenCoroutine = null;
    }

    /// <summary>
    /// 缓动曲线 x^b，x 是归一化到 [0,1] 的时间。
    /// 返回值同样是 [0,1]，代表本次缓动走了多少比例。
    /// </summary>
    private float EvaluateShape(float normalizedTime, bool returning)
    {
        float time01 = Mathf.Clamp01(normalizedTime);
        float exponent = Mathf.Max(0.0001f, easingB);

        if (returning && mirrorEasingOnReturn)
        {
            // 镜像曲线：0 → 1，先快后慢，归位时不会突然一顿。
            return 1f - Mathf.Pow(1f - time01, exponent);
        }

        return Mathf.Pow(time01, exponent);
    }

    /// <summary>
    /// 按归一化进度摆放拉杆。progress 为 0 时回到静止姿态。
    /// </summary>
    private void ApplyLeverProgress(float progress)
    {
        currentProgress = progress;
        float angle = rotationAngle * progress;

        if (IsPivotUsable())
        {
            Quaternion swing = Quaternion.AngleAxis(angle, GetRotationAxis());
            transform.rotation = rotationPivot.rotation * swing * restRelativeRotation;
            transform.position = rotationPivot.TransformPoint(swing * restRelativePosition);
        }
        else
        {
            transform.localRotation = restLocalRotation * Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void RestoreLeverRotation()
    {
        StopLeverTween();
        ApplyLeverProgress(0f);
    }

    private void OnDisable()
    {
        RestoreLeverRotation();
    }

    // ---------------------------------------------------------------- 工具

    /// <summary>
    /// 轴心可用性检查：轴心不能为空、不能是自己、也不能是自己的子物体
    /// （否则拉杆的运动会反过来带动轴心，形成自旋）。
    /// </summary>
    private bool IsPivotUsable()
    {
        if (rotationPivot == null || rotationPivot == transform)
        {
            return false;
        }

        if (rotationPivot.IsChildOf(transform))
        {
            if (!pivotWarningLogged)
            {
                pivotWarningLogged = true;
                Debug.LogWarning($"{name}：Rotation Pivot 是自身子物体，已忽略并退化为绕自身旋转。", this);
            }

            return false;
        }

        return true;
    }

    private Vector3 GetRotationAxis()
    {
        return rotationAxis.sqrMagnitude < 0.000001f ? Vector3.forward : rotationAxis.normalized;
    }

    private bool WasClicked(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, clickableLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<StampingMachineMoveClick>() == this)
            {
                return true;
            }
        }

        return false;
    }
}
