using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class C_ATK : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerAttack attack;

    [Header("Timings")]
    [SerializeField] private float windup = 0.02f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.10f;
    [SerializeField] private bool lockMoveDuringCounter = true;

    [Header("Multipliers")]
    [SerializeField] private float damageMul = 2.0f;
    [SerializeField] private float knockMul = 2.0f;
    [SerializeField] private float rangeMul = 1.1f;
    [SerializeField] private float radiusMul = 1.0f;

    [Header("Hitbox")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;

    private const string LOCK_CATK = "C_ATK";

    // === TAG SYSTEM ===
    public const string TAG_COUNTER_TRIGGERED = "Tag.Counter.Triggered";
    public event System.Action<string> OnTag;

    // state
    private bool counterArmed = false;
    private float counterExpireTime = -999f;
    private bool isCountering = false;
    private Coroutine attackMoveLockCo;
    private Collider2D[] myCols;

    public void Bind(PlayerAttack atk, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        attack = atk; combat = c; moveRef = m; animator = a;
        myCols = GetComponents<Collider2D>();
    }

    public void ArmCounter(float windowSeconds)
    {
        counterArmed = windowSeconds > 0f;
        counterExpireTime = Time.time + Mathf.Max(0f, windowSeconds);
    }

    public bool TryTriggerCounterOnAttackPress() => TryFireOnAttackPress();

    public bool TryFireOnAttackPress()
    {
        if (!counterArmed || Time.time > counterExpireTime || isCountering)
            return false;

        StartCoroutine(DoCounter());
        return true;
    }

    private IEnumerator DoCounter()
    {
        isCountering = true;
        counterArmed = false;

        OnTag?.Invoke(TAG_COUNTER_TRIGGERED); // 태그 

        int idx = (attack != null) ? attack.GetWeavingIndexForCounter() : 1;
        idx = Mathf.Clamp(idx, 1, 3);
        animator?.SetTrigger($"Counter{idx}");

        if (lockMoveDuringCounter)
        {
            float lockTime = windup + active + recovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime, true));
        }

        yield return new WaitForSeconds(windup);

        float dmg = attack ? attack.baseStats.baseDamage * damageMul : 10f * damageMul;
        float range = attack ? attack.baseStats.baseRange * rangeMul : 0.9f * rangeMul;
        float radius = attack ? attack.baseStats.baseRadius * radiusMul : 0.6f * radiusMul;
        DoHitbox(dmg, range, radius);

        combat?.EnterCombat("Counter");

        yield return new WaitForSeconds(active + recovery);
        isCountering = false;
    }

    private IEnumerator LockMoveFor(float seconds, bool zeroVelocity = true)
    {
        moveRef?.AddMovementLock(LOCK_CATK, false, zeroVelocity);
        yield return new WaitForSeconds(seconds);
        moveRef?.RemoveMovementLock(LOCK_CATK, false);
        attackMoveLockCo = null;
    }

    private void DoHitbox(float dmg, float range, float radius)
    {
        Vector2 facing = moveRef ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMask);
        foreach (var h in hits)
        {
            var target = h.GetComponentInParent<IDamageable>();
            target?.ApplyHit(dmg, facing, gameObject);
        }
        combat?.EnterCombat("CounterHit");
    }
}
