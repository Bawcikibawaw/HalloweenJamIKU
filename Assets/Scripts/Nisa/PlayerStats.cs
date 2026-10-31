using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float baseMoveSpeed = 4f;
    [SerializeField] [Range(0.1f, 2f)] private float carrySpeedMultiplier = 0.85f;
    [SerializeField] private float pickupDelay = 0.15f;

    private float speedMultiplier = 1f;
    private float slowTimer;
    private float stunTimer;

    public bool IsStunned => stunTimer > 0f;
    public float PickupDelay => pickupDelay;
    public float CurrentSpeed => IsStunned ? 0f : baseMoveSpeed * speedMultiplier;

    public float GetMoveSpeed(bool isCarrying) => CurrentSpeed * (isCarrying ? carrySpeedMultiplier : 1f);

    private void Update()
    {
        if (stunTimer > 0f)
            stunTimer = Mathf.Max(0f, stunTimer - Time.deltaTime);

        if (slowTimer > 0f && (slowTimer -= Time.deltaTime) <= 0f)
            speedMultiplier = 1f;
    }

    public void ApplySlow(float multiplier, float duration)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    public void ApplyStun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration);
    }

    public void ModifyBaseSpeed(float amount)
    {
        baseMoveSpeed = Mathf.Max(0.5f, baseMoveSpeed + amount);
    }

    [ContextMenu("Test: Clear Debuffs")]
    public void ClearDebuffs()
    {
        speedMultiplier = 1f;
        slowTimer = 0f;
        stunTimer = 0f;
    }

    [ContextMenu("Test: Slow 50% (3s)")]
    private void TestSlow() => ApplySlow(0.5f, 3f);

    [ContextMenu("Test: Stun (2s)")]
    private void TestStun() => ApplyStun(2f);
}
