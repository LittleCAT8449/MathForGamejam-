using System;
using UnityEngine;

/// <summary>
/// Marks an off-grid battery object as a power source. Its Collider2D bounds define
/// where it can physically connect to a deployed mining machine.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BatteryPowerSource : MonoBehaviour
{
    [SerializeField] private bool batteryActive = true;

    private Collider2D sourceCollider;

    public bool IsBatteryActive => batteryActive && isActiveAndEnabled;
    public Collider2D SourceCollider
    {
        get
        {
            if (sourceCollider == null)
            {
                sourceCollider = GetComponent<Collider2D>();
            }

            return sourceCollider;
        }
    }

    public event Action StateChanged;

    public void SetBatteryActive(bool active)
    {
        if (batteryActive == active)
        {
            return;
        }

        batteryActive = active;
        StateChanged?.Invoke();
    }

    private void Awake()
    {
        sourceCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        StateChanged?.Invoke();
    }

    private void OnDisable()
    {
        StateChanged?.Invoke();
    }
}
